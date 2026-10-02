namespace InControl.Core.Models;

/// <summary>
/// Represents a single message in a conversation.
/// </summary>
public sealed record Message
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The role of the message sender.
    /// </summary>
    public required MessageRole Role { get; init; }

    /// <summary>
    /// The text content of the message.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The model that generated this message (for assistant messages).
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Token count for this message (if available).
    /// </summary>
    public int? TokenCount { get; init; }

    /// <summary>
    /// Creates a new user message.
    /// </summary>
    public static Message User(string content) => new()
    {
        Id = Guid.NewGuid(),
        Role = MessageRole.User,
        Content = content,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a new assistant message.
    /// </summary>
    public static Message Assistant(string content, string? model = null) => new()
    {
        Id = Guid.NewGuid(),
        Role = MessageRole.Assistant,
        Content = content,
        CreatedAt = DateTimeOffset.UtcNow,
        Model = model
    };

    /// <summary>
    /// Creates a new system message.
    /// </summary>
    public static Message System(string content) => new()
    {
        Id = Guid.NewGuid(),
        Role = MessageRole.System,
        Content = content,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
