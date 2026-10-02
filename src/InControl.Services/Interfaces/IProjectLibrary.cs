using InControl.Core.Models;

namespace InControl.Services.Interfaces;

/// <summary>
/// Projects stored on this PC, beside the session files.
/// </summary>
public interface IProjectLibrary
{
    /// <summary>
    /// Loads projects, creating the General project if the file is missing.
    /// </summary>
    Task EnsureAsync(CancellationToken ct = default);

    /// <summary>
    /// All projects. General is always present after <see cref="EnsureAsync"/>.
    /// </summary>
    Task<IReadOnlyList<ChatProject>> AllAsync(CancellationToken ct = default);

    /// <summary>
    /// Finds one project by id.
    /// </summary>
    Task<ChatProject?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a project with the given name.
    /// </summary>
    Task<ChatProject> CreateAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Renames a project. The id stays the same.
    /// </summary>
    Task<ChatProject> RenameAsync(Guid id, string name, CancellationToken ct = default);

    /// <summary>
    /// Replaces standing instructions. Blank text clears them.
    /// </summary>
    Task<ChatProject> UpdateInstructionsAsync(Guid id, string? instructions, CancellationToken ct = default);
}
