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
    private readonly ILogger<OllamaInferenceClient> _logger;
    private readonly OllamaClientCache _clients;
    private readonly TimeSpan _timeout;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlySet<string>> _capabilities = new();

    public OllamaInferenceClient(
        IOptions<OllamaOptions> options,
        IOllamaEndpoint endpoint,
        ILogger<OllamaInferenceClient> logger,
        IOptions<InferenceOptions>? inference = null)
    {
        _options = options;
        _logger = logger;
        _clients = new OllamaClientCache(endpoint);
        _timeout = OllamaTimeouts.FromSeconds((inference?.Value ?? new InferenceOptions()).TimeoutSeconds);
    }

    public string BackendName => "Ollama";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var lease = _clients.Acquire();
            await OllamaTimeouts.RunAsync(token => lease.Client.GetVersionAsync(token), _timeout, ct);
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
            using var lease = _clients.Acquire();
            var version = await OllamaTimeouts.RunAsync(token => lease.Client.GetVersionAsync(token), _timeout, ct);
            sw.Stop();

            var models = await OllamaTimeouts.RunAsync(token => lease.Client.ListLocalModelsAsync(token), _timeout, ct);
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

    public async Task<bool?> SupportsImagesAsync(string modelId, CancellationToken ct = default)
    {
        var capabilities = await CapabilitiesAsync(modelId, ct);
        return capabilities is null ? null : capabilities.Contains("vision");
    }

    /// <summary>
    /// What Ollama says the model can do ("vision", "tools", "thinking"...), or null when it will not say.
    /// Answers are kept per endpoint, since a rental may hold a different build of the same name.
    /// </summary>
    private async Task<IReadOnlySet<string>?> CapabilitiesAsync(string modelId, CancellationToken ct)
    {
        try
        {
            using var lease = _clients.Acquire();
            var key = lease.Client.Uri + "|" + modelId;
            if (_capabilities.TryGetValue(key, out var cached))
                return cached;

            var info = await lease.Client.ShowModelAsync(modelId, ct);
            if (info?.Capabilities is not { } list)
                return null;

            var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            _capabilities[key] = set;
            return set;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // An older Ollama has no capabilities list. Unknown is not the same as no.
            _logger.LogDebug(ex, "Could not read capabilities for {Model}", modelId);
            return null;
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // The lease keeps this client alive if the endpoint changes while the reply streams.
        using var lease = _clients.Acquire();
        var client = lease.Client;
        client.SelectedModel = request.Model;

        // A text-only model refuses the whole request if any message carries an image.
        // Older images stay in the saved chat but are left out; a new one is refused plainly.
        var includeImages = true;
        if (request.Messages.Any(m => m.Images is { Count: > 0 })
            && await SupportsImagesAsync(request.Model, ct) == false)
        {
            if (request.Messages.LastOrDefault(m => m.Role == MessageRole.User)?.Images is { Count: > 0 })
            {
                throw new InvalidOperationException(
                    $"{request.Model} can't read images. Pick a vision model, such as gemma3 or llama3.2-vision, or send without the image.");
            }

            includeImages = false;
        }

        var messages = ToOllamaMessages(request, includeImages);

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

        // Tools only go to a model that says it can call them. Others answer without.
        Dictionary<string, ChatTool>? tools = null;
        if (request.Tools is { Count: > 0 })
        {
            var capabilities = await CapabilitiesAsync(request.Model, ct);
            if (capabilities is null || capabilities.Contains("tools"))
            {
                tools = request.Tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
                chatRequest.Tools = ToOllamaTools(request.Tools);
            }
            else
            {
                request.OnActivity?.Invoke($"{request.Model} can't use tools, so it answers without searching the web.");
            }
        }

        _logger.LogDebug("Starting streaming chat with model {Model}", request.Model);

        var received = false;
        for (var round = 0; ; round++)
        {
            var calls = new List<OllamaSharp.Models.Chat.Message.ToolCall>();
            var said = new System.Text.StringBuilder();

            var replies = OllamaTimeouts.StreamAsync(token => client.ChatAsync(chatRequest, token), _timeout, ct);
            await foreach (var response in replies.WithCancellation(ct))
            {
                // Reasoning models stream their thinking before the answer, in its own field.
                if (response?.Message?.Thinking is { Length: > 0 } thinking)
                    request.OnThinking?.Invoke(thinking);

                if (response?.Message?.ToolCalls is { } toolCalls)
                    calls.AddRange(toolCalls);

                if (response?.Message?.Content is { } content && content.Length > 0)
                {
                    received = true;
                    said.Append(content);
                    yield return content;
                }
            }

            if (calls.Count == 0 || tools is null)
                break;

            // The model asked for tools. Run them, hand back the results, and let it continue.
            messages.Add(new OllamaSharp.Models.Chat.Message
            {
                Role = OllamaSharp.Models.Chat.ChatRole.Assistant,
                Content = said.ToString(),
                ToolCalls = calls
            });

            foreach (var call in calls)
            {
                var name = call.Function?.Name ?? string.Empty;
                var args = ToStringArguments(call.Function?.Arguments);
                string result;
                if (!tools.TryGetValue(name, out var tool))
                {
                    result = $"There is no tool named {name}.";
                }
                else
                {
                    request.OnActivity?.Invoke(tool.Describe(args));
                    try
                    {
                        result = await tool.Run(args, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        result = $"The {name} tool failed: {ex.Message}";
                    }
                }

                messages.Add(new OllamaSharp.Models.Chat.Message
                {
                    Role = OllamaSharp.Models.Chat.ChatRole.Tool,
                    Content = result,
                    ToolName = name
                });
            }

            // The last round leaves the tools off so the model has to answer.
            if (round + 1 >= MaxToolRounds)
                chatRequest.Tools = null;
        }

        // Ollama reports some refusals, such as images sent to a text-only model, as a
        // stream with no tokens. An empty reply must not be saved as if it were an answer.
        if (!received)
        {
            throw new InvalidOperationException(
                request.Messages.Any(m => m.Images is { Count: > 0 })
                    ? $"{request.Model} sent back no reply. It may not read images. Pick a vision model, or remove the image."
                    : $"{request.Model} sent back no reply.");
        }
    }

    /// <summary>
    /// Most rounds of tool calls in one reply before the model must answer.
    /// </summary>
    internal const int MaxToolRounds = 4;

    /// <summary>
    /// Maps app tools to Ollama's function-tool shape. Every parameter is a string.
    /// </summary>
    internal static List<OllamaSharp.Models.Chat.Tool> ToOllamaTools(IEnumerable<ChatTool> tools) =>
        tools.Select(t => new OllamaSharp.Models.Chat.Tool
        {
            Function = new OllamaSharp.Models.Chat.Function
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = new OllamaSharp.Models.Chat.Parameters
                {
                    Properties = t.Parameters.ToDictionary(
                        p => p.Name,
                        p => new OllamaSharp.Models.Chat.Property { Type = "string", Description = p.Description }),
                    Required = t.Parameters.Where(p => p.Required).Select(p => p.Name).ToList()
                }
            }
        }).ToList();

    /// <summary>
    /// The model's arguments as plain strings. Numbers and booleans become their text.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ToStringArguments(IDictionary<string, object?>? arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (arguments is null)
            return result;

        foreach (var (key, value) in arguments)
        {
            result[key] = value switch
            {
                null => string.Empty,
                string text => text,
                System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element => element.GetString() ?? string.Empty,
                System.Text.Json.JsonElement element => element.GetRawText(),
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        return result;
    }

    /// <summary>
    /// Maps a chat request to OllamaSharp messages: the system prompt first, then the transcript with any images.
    /// </summary>
    internal static List<OllamaSharp.Models.Chat.Message> ToOllamaMessages(ChatRequest request, bool includeImages = true)
    {
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
                Content = msg.Content,
                // Images go to vision models through Ollama's images field.
                Images = includeImages && msg.Images is { Count: > 0 } images ? images.ToArray() : null
            });
        }

        return messages;
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
