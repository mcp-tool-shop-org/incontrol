namespace InControl.ViewModels.Sessions;

/// <summary>
/// One remembered note in the sidebar.
/// </summary>
public sealed class MemoryNoteItem
{
    /// <summary>
    /// Note id.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Short label, including whether the note is for the project or this session.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The remembered text.
    /// </summary>
    public required string Detail { get; init; }
}
