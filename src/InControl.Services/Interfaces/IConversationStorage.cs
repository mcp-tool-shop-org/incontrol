using InControl.Core.Models;

namespace InControl.Services.Interfaces;

/// <summary>
/// Persistence layer for conversations.
/// </summary>
public interface IConversationStorage
{
    /// <summary>
    /// Saves a conversation.
    /// </summary>
    /// <param name="conversation">The conversation to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(Conversation conversation, CancellationToken ct = default);

    /// <summary>
    /// Loads a conversation by ID.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The conversation, or null if not found.</returns>
    Task<Conversation?> LoadAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads all conversations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All saved conversations.</returns>
    Task<IReadOnlyList<Conversation>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a conversation.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Checks if a conversation exists.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if exists.</returns>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Exports a conversation to JSON.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON representation.</returns>
    Task<string> ExportAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Imports a conversation from JSON.
    /// </summary>
    /// <param name="json">The JSON to import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The imported conversation.</returns>
    Task<Conversation> ImportAsync(string json, CancellationToken ct = default);
}
