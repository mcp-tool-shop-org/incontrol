namespace InControl.Core.Models;

/// <summary>
/// Represents a chat conversation containing multiple messages.
/// </summary>
public sealed record Conversation
{
    /// <summary>
    /// Unique identifier for this conversation.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// User-defined title for the conversation.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// When the conversation was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the conversation was last modified.
    /// </summary>
    public required DateTimeOffset ModifiedAt { get; init; }

    /// <summary>
    /// The model used for this conversation.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Optional system prompt for this conversation.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Messages in this conversation, ordered chronologically.
    /// </summary>
    public IReadOnlyList<Message> Messages { get; init; } = [];

    /// <summary>
    /// Creates a new empty conversation.
    /// </summary>
    public static Conversation Create(string? title = null, string? model = null, string? systemPrompt = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Title = title ?? "New Conversation",
            CreatedAt = now,
            ModifiedAt = now,
            Model = model,
            SystemPrompt = systemPrompt,
            Messages = []
        };
    }

    /// <summary>
    /// Returns a new conversation with the message appended.
    /// </summary>
    public Conversation WithMessage(Message message) => this with
    {
        Messages = [.. Messages, message],
        ModifiedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Returns a new conversation with the specified message removed.
    /// </summary>
    public Conversation WithoutMessage(Guid messageId) => this with
    {
        Messages = Messages.Where(m => m.Id != messageId).ToList(),
        ModifiedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Returns a new conversation with the title updated.
    /// </summary>
    public Conversation WithTitle(string title) => this with
    {
        Title = title,
        ModifiedAt = DateTimeOffset.UtcNow
    };
}
