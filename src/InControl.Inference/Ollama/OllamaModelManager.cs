using Microsoft.Extensions.Logging;
using OllamaSharp;
using InControl.Core.Compute;
using InControl.Core.Models;
using InControl.Inference.Interfaces;

namespace InControl.Inference.Ollama;

/// <summary>
/// Model manager implementation backed by Ollama.
/// Handles listing, pulling, deleting, and preloading models.
/// </summary>
public sealed class OllamaModelManager : IModelManager
{
    private readonly IOllamaEndpoint _endpoint;
    private readonly ILogger<OllamaModelManager> _logger;
    private readonly object _clientGate = new();
    private OllamaApiClient? _client;
    private HttpClient? _http;
    private string? _clientUrl;

    public event EventHandler<ModelListChangedEventArgs>? ModelsChanged;
    public event EventHandler<ModelDownloadProgressEventArgs>? DownloadProgress;

    public OllamaModelManager(
        IOllamaEndpoint endpoint,
        ILogger<OllamaModelManager> logger)
    {
        _endpoint = endpoint;
        _logger = logger;
        _endpoint.Changed += (_, _) => DropClient();
    }

    private OllamaApiClient GetClient()
    {
        var url = _endpoint.BaseUrl;
        lock (_clientGate)
        {
            if (_client is not null && string.Equals(_clientUrl, url, StringComparison.Ordinal))
                return _client;

            DisposeClientLocked();
            (_client, _http) = CreateNonRedirectingClient(url);
            _clientUrl = url;
            return _client;
        }
    }

    private void DropClient()
    {
        lock (_clientGate)
        {
            DisposeClientLocked();
        }
    }

    /// <summary>
    /// Drops the cached client. The <see cref="HttpClient"/> overload does not dispose the handler,
    /// so the previous client and its handler are both disposed when the endpoint URL changes.
    /// </summary>
    private void DisposeClientLocked()
    {
        var client = _client;
        var http = _http;
        _client = null;
        _http = null;
        _clientUrl = null;
        try
        {
            client?.Dispose();
        }
        finally
        {
            http?.Dispose();
        }
    }

    /// <summary>
    /// Same endpoint URL as <see cref="IOllamaEndpoint.BaseUrl"/>. Redirects are off so a 307 or 308
    /// cannot replay a pull or delete onto another host.
    /// </summary>
    private static (OllamaApiClient Client, HttpClient Http) CreateNonRedirectingClient(string baseUrl)
    {
        HttpClientHandler? handler = null;
        HttpClient? http = null;
        try
        {
            handler = new HttpClientHandler { AllowAutoRedirect = false };
            http = new HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = new Uri(baseUrl)
            };
            handler = null;
            var client = new OllamaApiClient(http);
            var ownedHttp = http;
            http = null;
            return (client, ownedHttp);
        }
        finally
        {
            http?.Dispose();
            handler?.Dispose();
        }
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        var client = GetClient();
        var models = await client.ListLocalModelsAsync(ct);

        return models.Select(m => new ModelInfo
        {
            Id = m.Name,
            Name = m.Name,
            SizeBytes = m.Size,
            ParameterCount = m.Details?.ParameterSize,
            Quantization = m.Details?.QuantizationLevel,
            Family = m.Details?.Family,
            ModifiedAt = m.ModifiedAt
        }).ToList();
    }

    public async Task<ModelInfo> PullModelAsync(string modelId, CancellationToken ct = default)
    {
        var client = GetClient();
        _logger.LogInformation("Pulling model {ModelId}", modelId);

        await foreach (var status in client.PullModelAsync(modelId, ct))
        {
            if (status != null)
            {
                DownloadProgress?.Invoke(this, new ModelDownloadProgressEventArgs
                {
                    ModelId = modelId,
                    Status = status.Status ?? "Downloading",
                    BytesDownloaded = status.Completed,
                    TotalBytes = status.Total > 0 ? status.Total : null
                });
            }
        }

        // Refresh to get the pulled model info
        var models = await ListModelsAsync(ct);
        var pulled = models.FirstOrDefault(m =>
            m.Id.StartsWith(modelId, StringComparison.OrdinalIgnoreCase));

        var result = pulled ?? new ModelInfo
        {
            Id = modelId,
            Name = modelId,
            ModifiedAt = DateTimeOffset.UtcNow
        };

        ModelsChanged?.Invoke(this, new ModelListChangedEventArgs
        {
            ChangeType = ModelListChangeType.Added,
            Model = result
        });

        return result;
    }

    public async Task DeleteModelAsync(string modelId, CancellationToken ct = default)
    {
        var client = GetClient();
        _logger.LogInformation("Deleting model {ModelId}", modelId);

        await client.DeleteModelAsync(modelId, ct);

        ModelsChanged?.Invoke(this, new ModelListChangedEventArgs
        {
            ChangeType = ModelListChangeType.Removed,
            Model = new ModelInfo { Id = modelId, Name = modelId }
        });
    }

    public Task PreloadModelAsync(string modelId, CancellationToken ct = default)
    {
        // Ollama preloads on first inference request
        _logger.LogDebug("Preload requested for {ModelId} (handled by Ollama on first use)", modelId);
        return Task.CompletedTask;
    }

    public Task UnloadModelAsync(string modelId, CancellationToken ct = default)
    {
        _logger.LogDebug("Unload requested for {ModelId}", modelId);
        return Task.CompletedTask;
    }
}
