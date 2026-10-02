using System.Text;
using Microsoft.Extensions.Logging;
using InControl.Core.Errors;

namespace InControl.Services.Storage;

/// <summary>
/// File store implementation with path boundary enforcement.
/// All paths are validated against the allowed root before any I/O operation.
/// </summary>
public sealed class FileStore : IFileStore
{
    private readonly ILogger<FileStore> _logger;
    private readonly string _rootPath;
    private readonly HashSet<string> _allowedRoots;

    public FileStore(ILogger<FileStore> logger, string? rootPath = null)
    {
        _logger = logger;
        _rootPath = rootPath ?? GetDefaultAppDataPath();
        _allowedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(_rootPath)
        };

        EnsureDirectoryExists(_rootPath);
    }

    public string AppDataPath => _rootPath;

    public bool IsPathAllowed(string path)
    {
        try
        {
            var fullPath = NormalizePath(Path.GetFullPath(path));
            return _allowedRoots.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public string GetFullPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(_rootPath, relativePath));
    }

    public async Task<Result<string>> ReadTextAsync(string relativePath, CancellationToken ct = default)
    {
        var pathResult = ValidateAndResolvePath(relativePath);
        if (pathResult.IsFailure)
            return Result<string>.Failure(pathResult.Error);

        var fullPath = pathResult.Value!;

        try
        {
            if (!File.Exists(fullPath))
            {
                return InControlError.Create(ErrorCode.FileNotFound, $"File not found: {relativePath}");
            }

            var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8, ct);
            _logger.LogDebug("Read {Bytes} bytes from {Path}", content.Length, relativePath);
            return content;
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("File read");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied reading {Path}", relativePath);
            return InControlError.Create(ErrorCode.PermissionDenied, "Permission denied.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading {Path}", relativePath);
            return InControlError.Create(ErrorCode.StorageFailed, "Failed to read file.");
        }
    }

    public async Task<Result<byte[]>> ReadBytesAsync(string relativePath, CancellationToken ct = default)
    {
        var pathResult = ValidateAndResolvePath(relativePath);
        if (pathResult.IsFailure)
            return Result<byte[]>.Failure(pathResult.Error);

        var fullPath = pathResult.Value!;

        try
        {
            if (!File.Exists(fullPath))
            {
                return InControlError.Create(ErrorCode.FileNotFound, $"File not found: {relativePath}");
            }

            var content = await File.ReadAllBytesAsync(fullPath, ct);
            _logger.LogDebug("Read {Bytes} bytes from {Path}", content.Length, relativePath);
            return content;
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("File read");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied reading {Path}", relativePath);
            return InControlError.Create(ErrorCode.PermissionDenied, "Permission denied.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading {Path}", relativePath);
            return InControlError.Create(ErrorCode.StorageFailed, "Failed to read file.");
        }
    }

    public async Task<Result> WriteTextAsync(string relativePath, string content, CancellationToken ct = default)
    {
        var pathResult = ValidateAndResolvePath(relativePath);
        if (pathResult.IsFailure)
            return Result.Failure(pathResult.Error);

        var fullPath = pathResult.Value!;

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, ct);
            _logger.LogDebug("Wrote {Bytes} bytes to {Path}", content.Length, relativePath);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("File write");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied writing {Path}", relativePath);
            return InControlError.Create(ErrorCode.PermissionDenied, "Permission denied.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error writing {Path}", relativePath);
            return InControlError.Create(ErrorCode.StorageFailed, "Failed to write file.");
        }
    }

    public async Task<Result> WriteBytesAsync(string relativePath, byte[] content, CancellationToken ct = default)
    {
        var pathResult = ValidateAndResolvePath(relativePath);
        if (pathResult.IsFailure)
            return Result.Failure(pathResult.Error);

        var fullPath = pathResult.Value!;

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, content, ct);
            _logger.LogDebug("Wrote {Bytes} bytes to {Path}", content.Length, relativePath);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("File write");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied writing {Path}", relativePath);
            return InControlError.Create(ErrorCode.PermissionDenied, "Permission denied.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error writing {Path}", relativePath);
            return InControlError.Create(ErrorCode.StorageFailed, "Failed to write file.");
        }
    }

    public Task<Result> DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var pathResult = ValidateAndResolvePath(relativePath);
        if (pathResult.IsFailure)
            return Task.FromResult(Result.Failure(pathResult.Error));

        var fullPath = pathResult.Value!;

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogDebug("Deleted {Path}", relativePath);
            }
            return Task.FromResult(Result.Success());
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied deleting {Path}", relativePath);
            return Task.FromResult<Result>(InControlError.Create(ErrorCode.PermissionDenied, "Permission denied."));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error deleting {Path}", relativePath);
            return Task.FromResult<Result>(InControlError.Create(ErrorCode.StorageFailed, "Failed to delete file."));
        }
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
    {
        var pathResult = ValidateAndResolvePath(relativePath);
        if (pathResult.IsFailure)
            return Task.FromResult(false);

        return Task.FromResult(File.Exists(pathResult.Value));
    }

    public Task<Result<IReadOnlyList<string>>> ListFilesAsync(
        string relativeDirectory,
        string pattern = "*",
        CancellationToken ct = default)
    {
        var pathResult = ValidateAndResolvePath(relativeDirectory);
        if (pathResult.IsFailure)
            return Task.FromResult(Result<IReadOnlyList<string>>.Failure(pathResult.Error));

        var fullPath = pathResult.Value!;

        try
        {
            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult<Result<IReadOnlyList<string>>>(Array.Empty<string>());
            }

            var files = Directory.GetFiles(fullPath, pattern)
                .Select(f => Path.GetRelativePath(_rootPath, f))
                .ToList();

            return Task.FromResult<Result<IReadOnlyList<string>>>(files);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied listing {Path}", relativeDirectory);
            return Task.FromResult(Result<IReadOnlyList<string>>.Failure(
                InControlError.Create(ErrorCode.PermissionDenied, "Permission denied.")));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error listing {Path}", relativeDirectory);
            return Task.FromResult(Result<IReadOnlyList<string>>.Failure(
                InControlError.Create(ErrorCode.StorageFailed, "Failed to list files.")));
        }
    }

    private Result<string> ValidateAndResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return InControlError.Create(ErrorCode.InvalidArgument, "Path cannot be empty.");
        }

        // Prevent path traversal attacks
        if (relativePath.Contains("..") || Path.IsPathRooted(relativePath))
        {
            _logger.LogWarning("Blocked path traversal attempt: {Path}", relativePath);
            return InControlError.PathNotAllowed(relativePath);
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));

        if (!IsPathAllowed(fullPath))
        {
            _logger.LogWarning("Blocked access to path outside allowed roots: {Path}", fullPath);
            return InControlError.PathNotAllowed(relativePath);
        }

        return fullPath;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static string GetDefaultAppDataPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "InControl");
    }
}
