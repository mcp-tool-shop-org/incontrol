using System.Runtime.CompilerServices;
using InControl.Core.Models;
using InControl.Inference.Interfaces;

namespace InControl.Inference.Fakes;

/// <summary>
/// Fake inference client for testing without network dependencies.
/// Simulates all inference operations with configurable behavior.
/// </summary>
public sealed class FakeInferenceClient : IInferenceClient
{
    private readonly List<ModelInfo> _models = new();
    private readonly Queue<string> _responses = new();
    private readonly Queue<Exception> _errors = new();

    private bool _isAvailable = true;
    private TimeSpan _latency = TimeSpan.Zero;
    private int _tokensPerResponse = 10;
    private TimeSpan _tokenDelay = TimeSpan.FromMilliseconds(10);

    public string BackendName => "Fake";

    /// <summary>
    /// Gets the number of chat requests made.
    /// </summary>
    public int ChatRequestCount { get; private set; }

    /// <summary>
    /// Gets the last request made.
    /// </summary>
    public ChatRequest? LastRequest { get; private set; }

    /// <summary>
    /// Configures the client to be available or unavailable.
    /// </summary>
    public FakeInferenceClient SetAvailable(bool available)
    {
        _isAvailable = available;
        return this;
    }

    /// <summary>
    /// Adds a model to the available models list.
    /// </summary>
    public FakeInferenceClient AddModel(string name, long? size = null)
    {
        _models.Add(new ModelInfo
        {
            Id = name,
            Name = name,
            SizeBytes = size,
            ModifiedAt = DateTimeOffset.UtcNow
        });
        return this;
    }

    /// <summary>
    /// Queues a response to be returned by the next chat request.
    /// </summary>
    public FakeInferenceClient QueueResponse(string response)
    {
        _responses.Enqueue(response);
        return this;
    }

    /// <summary>
    /// Queues an exception to be thrown by the next chat request.
    /// </summary>
    public FakeInferenceClient QueueError(Exception error)
    {
        _errors.Enqueue(error);
        return this;
    }

    /// <summary>
    /// Sets the simulated latency for operations.
    /// </summary>
    public FakeInferenceClient SetLatency(TimeSpan latency)
    {
        _latency = latency;
        return this;
    }

    /// <summary>
    /// Sets the number of tokens per response for streaming.
    /// </summary>
    public FakeInferenceClient SetTokensPerResponse(int tokens)
    {
        _tokensPerResponse = tokens;
        return this;
    }

    /// <summary>
    /// Sets the delay between tokens when streaming.
    /// </summary>
    public FakeInferenceClient SetTokenDelay(TimeSpan delay)
    {
        _tokenDelay = delay;
        return this;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        await SimulateLatency(ct);
        return _isAvailable;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        await SimulateLatency(ct);

        if (!_isAvailable)
        {
            return HealthCheckResult.Unhealthy("Fake backend is unavailable.");
        }

        return new HealthCheckResult
        {
            IsHealthy = true,
            Status = "OK",
            Version = "fake-1.0",
            LoadedModels = _models.Count,
            GpuMemoryUsed = 1024 * 1024 * 1024,
            GpuMemoryTotal = 16L * 1024 * 1024 * 1024
        };
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        await SimulateLatency(ct);

        if (!_isAvailable)
        {
            throw new InvalidOperationException("Fake backend is unavailable.");
        }

        return _models.ToList();
    }

    public async Task<ModelInfo?> GetModelAsync(string modelId, CancellationToken ct = default)
    {
        await SimulateLatency(ct);

        if (!_isAvailable)
        {
            throw new InvalidOperationException("Fake backend is unavailable.");
        }

        return _models.FirstOrDefault(m => m.Id == modelId);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await SimulateLatency(ct);
        ChatRequestCount++;
        LastRequest = request;

        if (_errors.Count > 0)
        {
            throw _errors.Dequeue();
        }

        if (!_isAvailable)
        {
            throw new InvalidOperationException("Fake backend is unavailable.");
        }

        var response = _responses.Count > 0
            ? _responses.Dequeue()
            : GenerateDefaultResponse(request);

        // Split response into tokens
        var words = response.Split(' ');
        var tokensPerChunk = Math.Max(1, words.Length / _tokensPerResponse);

        for (var i = 0; i < words.Length; i += tokensPerChunk)
        {
            ct.ThrowIfCancellationRequested();

            var chunk = string.Join(" ", words.Skip(i).Take(tokensPerChunk));
            if (i > 0) chunk = " " + chunk;

            if (_tokenDelay > TimeSpan.Zero)
            {
                await Task.Delay(_tokenDelay, ct);
            }

            yield return chunk;
        }
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var tokens = new List<string>();
        await foreach (var token in StreamChatAsync(request, ct))
        {
            tokens.Add(token);
        }

        var content = string.Join("", tokens);
        return new ChatResponse
        {
            Content = content,
            Model = request.Model,
            CompletedAt = DateTimeOffset.UtcNow,
            PromptTokens = EstimateTokens(request.Messages.Sum(m => m.Content.Length)),
            CompletionTokens = EstimateTokens(content.Length)
        };
    }

    private async Task SimulateLatency(CancellationToken ct)
    {
        if (_latency > TimeSpan.Zero)
        {
            await Task.Delay(_latency, ct);
        }
    }

    private static string GenerateDefaultResponse(ChatRequest request)
    {
        var lastMessage = request.Messages.LastOrDefault();
        if (lastMessage is null)
        {
            return "I don't have any messages to respond to.";
        }

        return $"This is a fake response to: {lastMessage.Content.Substring(0, Math.Min(50, lastMessage.Content.Length))}...";
    }

    private static int EstimateTokens(int charCount) => charCount / 4;
}
