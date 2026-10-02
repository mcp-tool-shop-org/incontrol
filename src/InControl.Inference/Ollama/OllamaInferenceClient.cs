using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using InControl.Core.Compute;
using InControl.Core.Configuration;
using InControl.Core.Models;
using InControl.Inference.Interfaces;

namespace InControl.Inference.Ollama;

/// <summary>
/// Inference client implementation backed by Ollama.
/// Handles model listing and streaming chat completion.
/// </summary>
public sealed class OllamaInferenceClient : IInferenceClient
{
    private readonly IOptions<OllamaOptions> _options;
    private readonly IOllamaEndpoint _endpoint;
    private readonly ILogger<OllamaInferenceClient> _logger;
    private readonly object _clientGate = new();
    private OllamaApiClient? _client;
    private string? _clientUrl;

    public OllamaInferenceClient(
        IOptions<OllamaOptions> options,
        IOllamaEndpoint endpoint,
        ILogger<OllamaInferenceClient> logger)
    {
        _options = options;
        _endpoint = endpoint;
        _logger = logger;
        _endpoint.Changed += (_, _) => DropClient();
    }

    public string BackendName => "Ollama";

    private OllamaApiClient GetClient()
    {
        var url = _endpoint.BaseUrl;
        lock (_clientGate)
        {
            if (_client is null || !string.Equals(_clientUrl, url, StringComparison.Ordinal))
            {
                _client = new OllamaApiClient(url);
                _clientUrl = url;
                _logger.LogDebug("Created Ollama client at {BaseUrl}", url);
            }

            return _client;
        }
    }

    private void DropClient()
    {
        lock (_clientGate)
        {
            _client = null;
            _clientUrl = null;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var client = GetClient();
            await client.GetVersionAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama availability check failed");
            return false;
        }
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var client = GetClient();
            var version = await client.GetVersionAsync(ct);
            sw.Stop();

            var models = await client.ListLocalModelsAsync(ct);
            var modelCount = models.Count();

            return new HealthCheckResult
            {
                IsHealthy = true,
                Status = "Connected",
                ResponseTime = sw.Elapsed,
                Version = version?.ToString(),
                LoadedModels = modelCount
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Ollama health check failed");
            return HealthCheckResult.Unhealthy($"Connection failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Ollama models");
            throw;
        }
    }

    public async Task<ModelInfo?> GetModelAsync(string modelId, CancellationToken ct = default)
    {
        var models = await ListModelsAsync(ct);
        return models.FirstOrDefault(m =>
            string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = GetClient();
        client.SelectedModel = request.Model;

        // Build messages list for OllamaSharp
        var messages = new List<OllamaSharp.Models.Chat.Message>();

        // Add system prompt if provided
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new OllamaSharp.Models.Chat.Message
            {
                Role = OllamaSharp.Models.Chat.ChatRole.System,
                Content = request.SystemPrompt
            });
        }

        // Convert our messages to OllamaSharp messages
        foreach (var msg in request.Messages)
        {
            var role = msg.Role switch
            {
                MessageRole.User => OllamaSharp.Models.Chat.ChatRole.User,
                MessageRole.Assistant => OllamaSharp.Models.Chat.ChatRole.Assistant,
                MessageRole.System => OllamaSharp.Models.Chat.ChatRole.System,
                _ => OllamaSharp.Models.Chat.ChatRole.User
            };

            messages.Add(new OllamaSharp.Models.Chat.Message
            {
                Role = role,
                Content = msg.Content
            });
        }

        var chatRequest = new OllamaSharp.Models.Chat.ChatRequest
        {
            Model = request.Model,
            Messages = messages,
            Stream = true
        };

        // Set options if provided
        if (request.Temperature.HasValue || request.MaxTokens.HasValue || request.TopP.HasValue)
        {
            chatRequest.Options = new OllamaSharp.Models.RequestOptions
            {
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : null,
                NumPredict = request.MaxTokens,
                TopP = request.TopP.HasValue ? (float)request.TopP.Value : null,
                NumCtx = _options.Value.ContextSize,
                NumGpu = _options.Value.NumGpuLayers
            };
        }
        else
        {
            chatRequest.Options = new OllamaSharp.Models.RequestOptions
            {
                NumCtx = _options.Value.ContextSize,
                NumGpu = _options.Value.NumGpuLayers
            };
        }

        _logger.LogDebug("Starting streaming chat with model {Model}", request.Model);

        await foreach (var response in client.ChatAsync(chatRequest, ct))
        {
            if (response?.Message?.Content is { } content && content.Length > 0)
            {
                yield return content;
            }
        }
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tokens = new List<string>();

        await foreach (var token in StreamChatAsync(request, ct))
        {
            tokens.Add(token);
        }

        sw.Stop();
        var content = string.Join("", tokens);

        return new ChatResponse
        {
            Content = content,
            Model = request.Model,
            CompletedAt = DateTimeOffset.UtcNow,
            Duration = sw.Elapsed,
            CompletionTokens = EstimateTokens(content.Length),
            PromptTokens = EstimateTokens(request.Messages.Sum(m => m.Content.Length))
        };
    }

    private static int EstimateTokens(int charCount) => Math.Max(1, charCount / 4);
}
