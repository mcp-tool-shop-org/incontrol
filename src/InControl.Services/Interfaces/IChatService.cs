using InControl.Core.Models;

namespace InControl.Services.Interfaces;

/// <summary>
/// Service for managing chat conversations.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Event raised when a conversation is created.
    /// </summary>
    event EventHandler<ConversationEventArgs>? ConversationCreated;

    /// <summary>
    /// Event raised when a conversation is updated.
    /// </summary>
    event EventHandler<ConversationEventArgs>? ConversationUpdated;

    /// <summary>
    /// Event raised when a conversation is deleted.
    /// </summary>
    event EventHandler<ConversationEventArgs>? ConversationDeleted;

    /// <summary>
    /// Event raised when a session could not be written to or deleted from disk.
    /// The chat on screen may then differ from what is stored.
    /// </summary>
    event EventHandler<ConversationPersistenceFailedEventArgs>? PersistenceFailed;

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    /// <param name="title">Optional title.</param>
    /// <param name="model">Model to use.</param>
    /// <param name="systemPrompt">Optional system prompt.</param>
    /// <param name="projectId">Project to file the session in. Null uses General.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created conversation.</returns>
    Task<Conversation> CreateConversationAsync(
        string? title = null,
        string? model = null,
        string? systemPrompt = null,
        Guid? projectId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Stores a copy of a conversation, with its messages and project, titled "(copy)".
    /// </summary>
    /// <param name="conversationId">The conversation to copy.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored copy, or null if the source is missing or the copy could not be saved.</returns>
    Task<Conversation?> DuplicateConversationAsync(
        Guid conversationId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a conversation by ID.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The conversation, or null if not found.</returns>
    Task<Conversation?> GetConversationAsync(
        Guid conversationId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all conversations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all conversations, ordered by most recent first.</returns>
    Task<IReadOnlyList<Conversation>> GetConversationsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Updates a conversation's metadata.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="title">New title (null to keep existing).</param>
    /// <param name="model">New model (null to keep existing).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated conversation.</returns>
    Task<Conversation> UpdateConversationAsync(
        Guid conversationId,
        string? title = null,
        string? model = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// True when the session is gone (or was not there). False when the file could not be
    /// deleted: the session stays, and <see cref="PersistenceFailed"/> is raised.
    /// </returns>
    Task<bool> DeleteConversationAsync(
        Guid conversationId,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a message and streams the response.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="message">The user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of response tokens.</returns>
    IAsyncEnumerable<string> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a message with images, tools and progress callbacks, and streams the response.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="message">The user message, with any attached text files already in it.</param>
    /// <param name="options">Images, tools and callbacks for this reply, or null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of response tokens.</returns>
    IAsyncEnumerable<string> SendMessageAsync(
        Guid conversationId,
        string message,
        SendOptions? options,
        CancellationToken ct = default);

    /// <summary>
    /// Regenerates the last assistant response.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of response tokens.</returns>
    IAsyncEnumerable<string> RegenerateLastResponseAsync(
        Guid conversationId,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a message from a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="messageId">The message ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated conversation.</returns>
    Task<Conversation> RemoveMessageAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken ct = default);

    /// <summary>
    /// Stops the current generation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    void StopGeneration(Guid conversationId);
}

/// <summary>
/// Which disk change failed.
/// </summary>
public enum ConversationPersistenceOperation
{
    Save,
    Delete
}

/// <summary>
/// Event args for a failed save or delete.
/// </summary>
/// <summary>
/// What goes with one message besides its text.
/// </summary>
public sealed record SendOptions
{
    /// <summary>Base64 images, for vision models.</summary>
    public IReadOnlyList<string>? Images { get; init; }

    /// <summary>Tools the model may call during the reply, such as web search.</summary>
    public IReadOnlyList<ChatTool>? Tools { get; init; }

    /// <summary>Receives a reasoning model's thinking as it streams.</summary>
    public Action<string>? OnThinking { get; init; }

    /// <summary>Receives one line per tool the model uses.</summary>
    public Action<string>? OnActivity { get; init; }
}

public sealed class ConversationPersistenceFailedEventArgs : EventArgs
{
    /// <summary>
    /// The session whose file was not written or deleted.
    /// </summary>
    public required Guid ConversationId { get; init; }

    /// <summary>
    /// Whether the save or the delete failed.
    /// </summary>
    public required ConversationPersistenceOperation Operation { get; init; }
}

/// <summary>
/// Event args for conversation events.
/// </summary>
public sealed class ConversationEventArgs : EventArgs
{
    /// <summary>
    /// The affected conversation.
    /// </summary>
    public required Conversation Conversation { get; init; }
}
