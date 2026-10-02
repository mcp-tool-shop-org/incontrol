namespace InControl.Core.Models;

/// <summary>
/// A complete response from a chat completion request.
/// </summary>
public sealed record ChatResponse
{
    /// <summary>
    /// The generated message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The model that generated the response.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// When the response was completed.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Number of tokens in the prompt.
    /// </summary>
    public int? PromptTokens { get; init; }

    /// <summary>
    /// Number of tokens generated.
    /// </summary>
    public int? CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens used (prompt + completion).
    /// </summary>
    public int? TotalTokens => (PromptTokens, CompletionTokens) switch
    {
        (int p, int c) => p + c,
        _ => null
    };

    /// <summary>
    /// Time taken to generate the response.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Tokens per second during generation.
    /// </summary>
    public double? TokensPerSecond => (CompletionTokens, Duration) switch
    {
        (int tokens, TimeSpan duration) when duration.TotalSeconds > 0
            => tokens / duration.TotalSeconds,
        _ => null
    };

    /// <summary>
    /// Why generation stopped (e.g., "stop", "length", "error").
    /// </summary>
    public string? FinishReason { get; init; }
}
