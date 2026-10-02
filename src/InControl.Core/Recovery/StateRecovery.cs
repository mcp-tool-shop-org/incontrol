using InControl.Core.Errors;
using InControl.Core.State;
using InControl.Core.Storage;

namespace InControl.Core.Recovery;

/// <summary>
/// Provides state recovery and corruption handling capabilities.
/// </summary>
public sealed class StateRecovery
{
    private const string QuarantineFolder = "quarantine";
    private const string BackupFolder = "backup";

    /// <summary>
    /// Validates state integrity and returns any issues found.
    /// </summary>
    public static async Task<StateHealthReport> CheckHealthAsync(CancellationToken ct = default)
    {
        var issues = new List<StateIssue>();
        var sessionsPath = DataPaths.Sessions;

        // Check if sessions directory exists
        if (!Directory.Exists(sessionsPath))
        {
            return new StateHealthReport(
                IsHealthy: true,
                Issues: [],
                CheckedAt: DateTimeOffset.UtcNow,
                TotalFiles: 0,
                CorruptFiles: 0
            );
        }

        var sessionFiles = Directory.GetFiles(sessionsPath, "*.json");
        var corruptCount = 0;

        foreach (var file in sessionFiles)
        {
            ct.ThrowIfCancellationRequested();

            var issue = await ValidateSessionFileAsync(file, ct).ConfigureAwait(false);
            if (issue != null)
            {
                issues.Add(issue);
                corruptCount++;
            }
        }

        return new StateHealthReport(
            IsHealthy: corruptCount == 0,
            Issues: issues,
            CheckedAt: DateTimeOffset.UtcNow,
            TotalFiles: sessionFiles.Length,
            CorruptFiles: corruptCount
        );
    }

    /// <summary>
    /// Validates a single session file and returns any issue found.
    /// </summary>
    private static async Task<StateIssue?> ValidateSessionFileAsync(string filePath, CancellationToken ct)
    {
        var fileName = Path.GetFileName(filePath);

        try
        {
            // Check file can be opened
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            // Check file is not empty
            if (stream.Length == 0)
            {
                return new StateIssue(
                    FilePath: filePath,
                    IssueType: StateIssueType.EmptyFile,
                    Description: $"Session file '{fileName}' is empty",
                    RecoveryOptions: [RecoveryAction.Quarantine, RecoveryAction.Delete]
                );
            }

            // Try to deserialize
            var result = await StateSerializer.DeserializeAsync<AppState>(stream, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return new StateIssue(
                    FilePath: filePath,
                    IssueType: StateIssueType.InvalidJson,
                    Description: $"Session file '{fileName}' contains invalid JSON: {result.Error.Message}",
                    RecoveryOptions: [RecoveryAction.Quarantine, RecoveryAction.Delete, RecoveryAction.RestoreBackup]
                );
            }

            // Validate state integrity
            var state = result.Value;
            if (state.Version < 1)
            {
                return new StateIssue(
                    FilePath: filePath,
                    IssueType: StateIssueType.InvalidVersion,
                    Description: $"Session file '{fileName}' has invalid version: {state.Version}",
                    RecoveryOptions: [RecoveryAction.Quarantine, RecoveryAction.Delete]
                );
            }

            return null; // File is healthy
        }
        catch (IOException ex)
        {
            return new StateIssue(
                FilePath: filePath,
                IssueType: StateIssueType.AccessError,
                Description: $"Cannot access session file '{fileName}': {ex.Message}",
                RecoveryOptions: [RecoveryAction.Retry, RecoveryAction.Delete]
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StateIssue(
                FilePath: filePath,
                IssueType: StateIssueType.UnknownError,
                Description: $"Error validating session file '{fileName}': {ex.Message}",
                RecoveryOptions: [RecoveryAction.Quarantine, RecoveryAction.Delete]
            );
        }
    }

    /// <summary>
    /// Quarantines a corrupt file by moving it to a quarantine folder.
    /// </summary>
    public static Result<string> QuarantineFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return InControlError.Create(ErrorCode.FileNotFound, $"File not found: {filePath}");
        }

        try
        {
            var quarantinePath = Path.Combine(DataPaths.AppDataRoot, QuarantineFolder);
            Directory.CreateDirectory(quarantinePath);

            var fileName = Path.GetFileName(filePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var quarantinedName = $"{timestamp}_{fileName}";
            var destinationPath = Path.Combine(quarantinePath, quarantinedName);

            File.Move(filePath, destinationPath);

            return destinationPath;
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Failed to quarantine file: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a backup of the current state.
    /// </summary>
    public static async Task<Result<string>> CreateBackupAsync(CancellationToken ct = default)
    {
        try
        {
            var backupPath = Path.Combine(DataPaths.AppDataRoot, BackupFolder);
            Directory.CreateDirectory(backupPath);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var backupName = $"backup-{timestamp}.zip";
            var destinationPath = Path.Combine(backupPath, backupName);

            // Create backup of sessions
            var sessionsPath = DataPaths.Sessions;
            if (Directory.Exists(sessionsPath))
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(sessionsPath, destinationPath);
            }
            else
            {
                // Create empty backup
                await using var fs = File.Create(destinationPath);
                using var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create);
                // Empty archive
            }

            return destinationPath;
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Failed to create backup: {ex.Message}");
        }
    }

    /// <summary>
    /// Lists available backups.
    /// </summary>
    public static IReadOnlyList<BackupInfo> ListBackups()
    {
        var backupPath = Path.Combine(DataPaths.AppDataRoot, BackupFolder);

        if (!Directory.Exists(backupPath))
            return [];

        return Directory.GetFiles(backupPath, "backup-*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupInfo(
                FilePath: f.FullName,
                FileName: f.Name,
                CreatedAt: f.CreationTimeUtc,
                SizeBytes: f.Length
            ))
            .ToList();
    }

    /// <summary>
    /// Restores state from a backup.
    /// </summary>
    public static async Task<Result<Unit>> RestoreBackupAsync(string backupPath, CancellationToken ct = default)
    {
        if (!File.Exists(backupPath))
        {
            return InControlError.Create(ErrorCode.FileNotFound, $"Backup not found: {backupPath}");
        }

        try
        {
            // Create backup of current state first
            var currentBackupResult = await CreateBackupAsync(ct).ConfigureAwait(false);
            if (!currentBackupResult.IsSuccess)
            {
                return InControlError.Create(
                    ErrorCode.FileOperationFailed,
                    $"Failed to backup current state before restore: {currentBackupResult.Error.Message}");
            }

            // Clear current sessions
            var sessionsPath = DataPaths.Sessions;
            if (Directory.Exists(sessionsPath))
            {
                Directory.Delete(sessionsPath, recursive: true);
            }
            Directory.CreateDirectory(sessionsPath);

            // Extract backup
            System.IO.Compression.ZipFile.ExtractToDirectory(backupPath, sessionsPath);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Failed to restore backup: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs a full application reset with optional export.
    /// </summary>
    public static async Task<Result<ResetResult>> ResetApplicationAsync(
        bool exportFirst = true,
        CancellationToken ct = default)
    {
        string? exportPath = null;

        try
        {
            // Export before reset if requested
            if (exportFirst)
            {
                var backupResult = await CreateBackupAsync(ct).ConfigureAwait(false);
                if (backupResult.IsSuccess)
                {
                    exportPath = backupResult.Value;
                }
            }

            // Clear all application data
            var pathsToClear = new[]
            {
                DataPaths.Sessions,
                DataPaths.Cache,
                DataPaths.Temp
            };

            foreach (var path in pathsToClear)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                    Directory.CreateDirectory(path);
                }
            }

            // Clear config (but keep logs for troubleshooting)
            var configPath = DataPaths.Config;
            if (Directory.Exists(configPath))
            {
                foreach (var file in Directory.GetFiles(configPath))
                {
                    File.Delete(file);
                }
            }

            return new ResetResult(
                Success: true,
                ExportPath: exportPath,
                ClearedPaths: pathsToClear.ToList()
            );
        }
        catch (Exception ex)
        {
            return InControlError.Create(
                ErrorCode.FileOperationFailed,
                $"Failed to reset application: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies a recovery action to a specific issue.
    /// </summary>
    public static async Task<Result<Unit>> ApplyRecoveryAsync(
        StateIssue issue,
        RecoveryAction action,
        CancellationToken ct = default)
    {
        return action switch
        {
            RecoveryAction.Quarantine => QuarantineFile(issue.FilePath).Map(_ => Unit.Value),
            RecoveryAction.Delete => DeleteFile(issue.FilePath),
            RecoveryAction.RestoreBackup => await RestoreLatestBackupAsync(ct).ConfigureAwait(false),
            RecoveryAction.Retry => Result<Unit>.Success(Unit.Value), // Just signal retry
            RecoveryAction.Ignore => Result<Unit>.Success(Unit.Value),
            _ => InControlError.Create(ErrorCode.InvalidOperation, $"Unknown recovery action: {action}")
        };
    }

    private static Result<Unit> DeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Unit.Value;
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Failed to delete file: {ex.Message}");
        }
    }

    private static async Task<Result<Unit>> RestoreLatestBackupAsync(CancellationToken ct)
    {
        var backups = ListBackups();
        if (backups.Count == 0)
        {
            return InControlError.Create(ErrorCode.FileNotFound, "No backups available to restore");
        }

        return await RestoreBackupAsync(backups[0].FilePath, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Report of state health check.
/// </summary>
public sealed record StateHealthReport(
    bool IsHealthy,
    IReadOnlyList<StateIssue> Issues,
    DateTimeOffset CheckedAt,
    int TotalFiles,
    int CorruptFiles
);

/// <summary>
/// A single issue found during state validation.
/// </summary>
public sealed record StateIssue(
    string FilePath,
    StateIssueType IssueType,
    string Description,
    IReadOnlyList<RecoveryAction> RecoveryOptions
);

/// <summary>
/// Types of state issues.
/// </summary>
public enum StateIssueType
{
    EmptyFile,
    InvalidJson,
    InvalidVersion,
    AccessError,
    UnknownError
}

/// <summary>
/// Recovery actions available for state issues.
/// </summary>
public enum RecoveryAction
{
    Quarantine,
    Delete,
    RestoreBackup,
    Retry,
    Ignore
}

/// <summary>
/// Information about a backup.
/// </summary>
public sealed record BackupInfo(
    string FilePath,
    string FileName,
    DateTime CreatedAt,
    long SizeBytes
);

/// <summary>
/// Result of an application reset.
/// </summary>
public sealed record ResetResult(
    bool Success,
    string? ExportPath,
    IReadOnlyList<string> ClearedPaths
);
