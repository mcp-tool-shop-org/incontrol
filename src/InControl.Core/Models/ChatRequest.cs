namespace InControl.Core.Models;

/// <summary>
/// A request to generate a chat completion.
/// </summary>
public sealed record ChatRequest
{
    /// <summary>
    /// The model to use for generation.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// The messages to send to the model.
    /// </summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>
    /// Optional system prompt to prepend.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Temperature for response randomness (0.0 - 2.0).
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Maximum tokens to generate.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Top-p (nucleus) sampling threshold.
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// Stop sequences to end generation.
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// Creates a simple chat request with a single user message.
    /// </summary>
    public static ChatRequest Simple(string model, string userMessage) => new()
    {
        Model = model,
        Messages = [Message.User(userMessage)]
    };

    /// <summary>
    /// Creates a chat request from an existing conversation.
    /// </summary>
    public static ChatRequest FromConversation(Conversation conversation, string? modelOverride = null) => new()
    {
        Model = modelOverride ?? conversation.Model ?? throw new ArgumentException("No model specified"),
        Messages = conversation.Messages,
        SystemPrompt = conversation.SystemPrompt
    };
}
