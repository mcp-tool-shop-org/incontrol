using Microsoft.Extensions.Logging;
using OllamaSharp;
using Microsoft.Extensions.Options;
using InControl.Core.Compute;
using InControl.Core.Configuration;
using InControl.Core.Models;
using InControl.Inference.Interfaces;

namespace InControl.Inference.Ollama;

/// <summary>
/// Model manager implementation backed by Ollama.
/// Handles listing, pulling, deleting, and preloading models.
/// </summary>
public sealed class OllamaModelManager : IModelManager
{
    private readonly ILogger<OllamaModelManager> _logger;
    private readonly OllamaClientCache _clients;
    private readonly TimeSpan _timeout;

    public event EventHandler<ModelListChangedEventArgs>? ModelsChanged;
    public event EventHandler<ModelDownloadProgressEventArgs>? DownloadProgress;

    public OllamaModelManager(
        IOllamaEndpoint endpoint,
        ILogger<OllamaModelManager> logger,
        IOptions<InferenceOptions>? inference = null)
    {
        _logger = logger;
        _clients = new OllamaClientCache(endpoint);
        _timeout = OllamaTimeouts.FromSeconds((inference?.Value ?? new InferenceOptions()).TimeoutSeconds);
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        using var lease = _clients.Acquire();
        var models = await OllamaTimeouts.RunAsync(token => lease.Client.ListLocalModelsAsync(token), _timeout, ct);

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
        using var lease = _clients.Acquire();
        var client = lease.Client;
        _logger.LogInformation("Pulling model {ModelId}", modelId);

        // A pull can run for many minutes. Only a long silence ends it.
        var progress = OllamaTimeouts.StreamAsync(token => client.PullModelAsync(modelId, token), _timeout, ct);
        await foreach (var status in progress.WithCancellation(ct))
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
        using var lease = _clients.Acquire();
        _logger.LogInformation("Deleting model {ModelId}", modelId);

        await OllamaTimeouts.RunAsync(token => lease.Client.DeleteModelAsync(modelId, token), _timeout, ct);

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
