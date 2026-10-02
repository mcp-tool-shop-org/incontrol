using System.IO.Compression;
using InControl.Core.Storage;

namespace InControl.Core.Diagnostics;

/// <summary>
/// Creates support bundles containing logs, diagnostics, and optional session metadata.
/// </summary>
public sealed class SupportBundle
{
    /// <summary>
    /// Creates a support bundle zip file.
    /// </summary>
    /// <param name="outputPath">Path where the bundle will be saved.</param>
    /// <param name="options">Options for what to include in the bundle.</param>
    /// <returns>The path to the created bundle.</returns>
    public static async Task<SupportBundleResult> CreateAsync(
        string? outputPath = null,
        SupportBundleOptions? options = null)
    {
        options ??= SupportBundleOptions.Default;

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"incontrol-support-{timestamp}.zip";

        outputPath ??= Path.Combine(DataPaths.Support, fileName);

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var includedFiles = new List<string>();
        var errors = new List<string>();

        try
        {
            using var zipStream = new FileStream(outputPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Always include diagnostics report
            await AddDiagnosticsAsync(archive);
            includedFiles.Add("diagnostics.json");

            // Include health report
            if (options.IncludeHealthReport)
            {
                await AddHealthReportAsync(archive);
                includedFiles.Add("health.json");
            }

            // Include logs
            if (options.IncludeLogs)
            {
                var logFiles = await AddLogsAsync(archive, options.MaxLogFiles);
                includedFiles.AddRange(logFiles.Select(f => $"logs/{f}"));
            }

            // Include sanitized config
            if (options.IncludeConfig)
            {
                await AddConfigAsync(archive);
                includedFiles.Add("config.json");
            }

            // Include session metadata (not content)
            if (options.IncludeSessionMetadata)
            {
                await AddSessionMetadataAsync(archive);
                includedFiles.Add("sessions.json");
            }

            return new SupportBundleResult(
                Success: true,
                BundlePath: outputPath,
                IncludedFiles: includedFiles,
                Errors: errors,
                CreatedAt: DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to create bundle: {ex.Message}");

            return new SupportBundleResult(
                Success: false,
                BundlePath: outputPath,
                IncludedFiles: includedFiles,
                Errors: errors,
                CreatedAt: DateTimeOffset.UtcNow
            );
        }
    }

    private static async Task AddDiagnosticsAsync(ZipArchive archive)
    {
        var report = DiagnosticsInfo.GetReport();
        var json = report.ToJson();

        var entry = archive.CreateEntry("diagnostics.json");
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync(json);
    }

    private static async Task AddHealthReportAsync(ZipArchive archive)
    {
        // Simple health summary - real implementation would use HealthService
        var health = new
        {
            status = "unknown",
            timestamp = DateTimeOffset.UtcNow,
            checks = new List<object>()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(health, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var entry = archive.CreateEntry("health.json");
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync(json);
    }

    private static async Task<List<string>> AddLogsAsync(ZipArchive archive, int maxFiles)
    {
        var addedFiles = new List<string>();
        var logsPath = DataPaths.Logs;

        if (!Directory.Exists(logsPath))
            return addedFiles;

        var logFiles = Directory.GetFiles(logsPath, "*.log")
            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
            .Take(maxFiles);

        foreach (var logFile in logFiles)
        {
            var fileName = Path.GetFileName(logFile);
            var entry = archive.CreateEntry($"logs/{fileName}");

            try
            {
                // Read file with sharing to allow log writes
                await using var fileStream = new FileStream(
                    logFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                await using var entryStream = entry.Open();
                await fileStream.CopyToAsync(entryStream);
                addedFiles.Add(fileName);
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        return addedFiles;
    }

    private static async Task AddConfigAsync(ZipArchive archive)
    {
        // Sanitized config - only includes non-sensitive settings
        var sanitizedConfig = new
        {
            dataRoot = DataPaths.AppDataRoot,
            logsPath = DataPaths.Logs,
            sessionsPath = DataPaths.Sessions,
            // Never include API keys, tokens, or credentials
            note = "Sensitive settings are not included in support bundles"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(sanitizedConfig, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var entry = archive.CreateEntry("config.json");
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync(json);
    }

    private static async Task AddSessionMetadataAsync(ZipArchive archive)
    {
        var sessionsPath = DataPaths.Sessions;
        var sessions = new List<object>();

        if (Directory.Exists(sessionsPath))
        {
            var sessionFiles = Directory.GetFiles(sessionsPath, "*.json");
            foreach (var file in sessionFiles)
            {
                var fileInfo = new FileInfo(file);
                sessions.Add(new
                {
                    id = Path.GetFileNameWithoutExtension(file),
                    size = fileInfo.Length,
                    modified = fileInfo.LastWriteTimeUtc
                    // Never include session content
                });
            }
        }

        var metadata = new
        {
            count = sessions.Count,
            sessions,
            note = "Session content is not included. Only metadata for troubleshooting."
        };

        var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var entry = archive.CreateEntry("sessions.json");
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync(json);
    }
}

/// <summary>
/// Options for creating a support bundle.
/// </summary>
public sealed record SupportBundleOptions(
    bool IncludeLogs = true,
    bool IncludeHealthReport = true,
    bool IncludeConfig = true,
    bool IncludeSessionMetadata = false,
    int MaxLogFiles = 5
)
{
    /// <summary>
    /// Default options (logs, health, config - no session data).
    /// </summary>
    public static SupportBundleOptions Default { get; } = new();

    /// <summary>
    /// Full bundle including session metadata.
    /// </summary>
    public static SupportBundleOptions Full { get; } = new(IncludeSessionMetadata: true);

    /// <summary>
    /// Minimal bundle (just diagnostics).
    /// </summary>
    public static SupportBundleOptions Minimal { get; } = new(
        IncludeLogs: false,
        IncludeHealthReport: false,
        IncludeConfig: false,
        IncludeSessionMetadata: false
    );
}

/// <summary>
/// Result of creating a support bundle.
/// </summary>
public sealed record SupportBundleResult(
    bool Success,
    string BundlePath,
    IReadOnlyList<string> IncludedFiles,
    IReadOnlyList<string> Errors,
    DateTimeOffset CreatedAt
);
