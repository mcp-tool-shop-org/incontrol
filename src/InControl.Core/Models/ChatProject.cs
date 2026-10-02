namespace InControl.Core.Models;

/// <summary>
/// A named group of sessions that share instructions and project notes.
/// </summary>
public sealed record ChatProject
{
    /// <summary>
    /// Stable id for the project that holds sessions saved before projects existed.
    /// </summary>
    public static readonly Guid GeneralId = new("a7c3e1b0-5d24-4f8a-9c61-0b2e4d6f8a10");

    /// <summary>
    /// Unique identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Name shown in the sidebar.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When the project was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Standing instructions included in every session in this project.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// The default project. Its id does not change.
    /// </summary>
    public static ChatProject General() => new()
    {
        Id = GeneralId,
        Name = "General",
        CreatedAt = DateTimeOffset.UnixEpoch
    };

    /// <summary>
    /// Returns a copy with a new name.
    /// </summary>
    public ChatProject WithName(string name) => this with { Name = name };

    /// <summary>
    /// Returns a copy with new standing instructions.
    /// </summary>
    public ChatProject WithInstructions(string? instructions) => this with { Instructions = instructions };
}
