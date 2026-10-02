using InControl.Core.Errors;

namespace InControl.Services.Storage;

/// <summary>
/// Abstraction for file system operations with path validation.
/// All file I/O must go through this interface to enforce boundaries.
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Gets the root path for application data.
    /// </summary>
    string AppDataPath { get; }

    /// <summary>
    /// Checks if a path is within allowed write boundaries.
    /// </summary>
    bool IsPathAllowed(string path);

    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    Task<Result<string>> ReadTextAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Reads all bytes from a file.
    /// </summary>
    Task<Result<byte[]>> ReadBytesAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Writes text to a file (creates directories if needed).
    /// </summary>
    Task<Result> WriteTextAsync(string relativePath, string content, CancellationToken ct = default);

    /// <summary>
    /// Writes bytes to a file (creates directories if needed).
    /// </summary>
    Task<Result> WriteBytesAsync(string relativePath, byte[] content, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file if it exists.
    /// </summary>
    Task<Result> DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Lists files matching a pattern in a directory.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> ListFilesAsync(
        string relativeDirectory,
        string pattern = "*",
        CancellationToken ct = default);

    /// <summary>
    /// Gets the full path for a relative path (for display/logging only).
    /// </summary>
    string GetFullPath(string relativePath);
}
