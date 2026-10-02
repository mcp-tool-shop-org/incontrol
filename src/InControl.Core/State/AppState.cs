using InControl.Core.Models;

namespace InControl.Core.State;

/// <summary>
/// Root state container for the entire application.
/// This is the single source of truth for all application state.
/// </summary>
public sealed record AppState
{
    /// <summary>
    /// Version of the state schema for migration support.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// All conversations in the application.
    /// </summary>
    public IReadOnlyList<Conversation> Conversations { get; init; } = [];

    /// <summary>
    /// The ID of the currently active conversation, if any.
    /// </summary>
    public Guid? ActiveConversationId { get; init; }

    /// <summary>
    /// Current model selection state.
    /// </summary>
    public required ModelSelectionState ModelSelection { get; init; }

    /// <summary>
    /// When this state was last modified.
    /// </summary>
    public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates an empty initial state.
    /// </summary>
    public static AppState Initial(string backend = "Ollama") => new()
    {
        ModelSelection = ModelSelectionState.Default(backend)
    };

    /// <summary>
    /// Gets the currently active conversation, if any.
    /// </summary>
    public Conversation? ActiveConversation =>
        ActiveConversationId.HasValue
            ? Conversations.FirstOrDefault(c => c.Id == ActiveConversationId.Value)
            : null;

    /// <summary>
    /// Returns state with a new conversation added.
    /// </summary>
    public AppState WithConversation(Conversation conversation) => this with
    {
        Conversations = [.. Conversations, conversation],
        LastModified = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Returns state with an updated conversation.
    /// </summary>
    public AppState WithUpdatedConversation(Conversation conversation) => this with
    {
        Conversations = Conversations
            .Select(c => c.Id == conversation.Id ? conversation : c)
            .ToList(),
        LastModified = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Returns state with a conversation removed.
    /// </summary>
    public AppState WithoutConversation(Guid conversationId) => this with
    {
        Conversations = Conversations.Where(c => c.Id != conversationId).ToList(),
        ActiveConversationId = ActiveConversationId == conversationId ? null : ActiveConversationId,
        LastModified = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Returns state with the active conversation changed.
    /// </summary>
    public AppState WithActiveConversation(Guid? conversationId) => this with
    {
        ActiveConversationId = conversationId,
        LastModified = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Returns state with updated model selection.
    /// </summary>
    public AppState WithModelSelection(ModelSelectionState selection) => this with
    {
        ModelSelection = selection,
        LastModified = DateTimeOffset.UtcNow
    };
}
