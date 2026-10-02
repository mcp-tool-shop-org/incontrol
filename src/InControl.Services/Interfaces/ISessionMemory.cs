using InControl.Core.Assistant;

namespace InControl.Services.Interfaces;

/// <summary>
/// Notes the user asked to remember. Writes are explicit. Recall is a short list,
/// not the other sessions' transcripts.
/// </summary>
public interface ISessionMemory
{
    /// <summary>
    /// Loads the note file.
    /// </summary>
    Task EnsureAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores a note the user asked to remember.
    /// </summary>
    Task<AssistantMemoryItem> RememberAsync(AssistantMemoryItem item, CancellationToken ct = default);

    /// <summary>
    /// Removes one note.
    /// </summary>
    Task ForgetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Removes every note that belongs only to this session.
    /// </summary>
    Task ForgetSessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Notes the sidebar can show: project notes, plus this session's notes when a session is open.
    /// </summary>
    Task<IReadOnlyList<AssistantMemoryItem>> ListAsync(
        Guid projectId,
        Guid? sessionId,
        CancellationToken ct = default);

    /// <summary>
    /// Builds the prompt block for this turn and marks the chosen notes as touched.
    /// Returns an empty string when there is nothing to add. Failures return empty
    /// so a note file cannot stop the chat.
    /// </summary>
    Task<string> BuildRecallAsync(
        Guid projectId,
        Guid? sessionId,
        string? instructions,
        string? query,
        CancellationToken ct = default);
}
