using System.Text;
using System.Text.Json;
using InControl.Core.Errors;
using InControl.Core.Models;
using InControl.Core.State;
using InControl.Core.Storage;

namespace InControl.Core.Export;

/// <summary>
/// Exports conversations to various formats.
/// </summary>
public static class SessionExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Exports a conversation to Markdown format.
    /// </summary>
    public static string ToMarkdown(Conversation conversation)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# {conversation.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Created:** {conversation.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Modified:** {conversation.ModifiedAt:yyyy-MM-dd HH:mm:ss} UTC");

        if (!string.IsNullOrEmpty(conversation.Model))
        {
            sb.AppendLine($"**Model:** {conversation.Model}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // System prompt if present
        if (!string.IsNullOrEmpty(conversation.SystemPrompt))
        {
            sb.AppendLine("## System Prompt");
            sb.AppendLine();
            sb.AppendLine("> " + conversation.SystemPrompt.Replace("\n", "\n> "));
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Messages
        sb.AppendLine("## Conversation");
        sb.AppendLine();

        foreach (var message in conversation.Messages)
        {
            var roleLabel = message.Role switch
            {
                MessageRole.User => "**User**",
                MessageRole.Assistant => "**Assistant**",
                MessageRole.System => "**System**",
                _ => $"**{message.Role}**"
            };

            sb.AppendLine($"### {roleLabel}");
            sb.AppendLine($"*{message.CreatedAt:HH:mm:ss}*");
            sb.AppendLine();
            sb.AppendLine(message.Content);
            sb.AppendLine();
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"*Exported from InControl on {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Exports a conversation to JSON format.
    /// </summary>
    public static string ToJson(Conversation conversation)
    {
        var export = new ConversationExport
        {
            Version = 1,
            ExportedAt = DateTimeOffset.UtcNow,
            Conversation = conversation
        };

        return JsonSerializer.Serialize(export, JsonOptions);
    }

    /// <summary>
    /// Exports a conversation to a file.
    /// </summary>
    public static async Task<Result<string>> ExportToFileAsync(
        Conversation conversation,
        ExportFormat format,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            var extension = format switch
            {
                ExportFormat.Markdown => ".md",
                ExportFormat.Json => ".json",
                _ => ".txt"
            };

            // Generate filename if not provided
            if (string.IsNullOrEmpty(outputPath))
            {
                var safeName = SanitizeFileName(conversation.Title);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var fileName = $"{safeName}_{timestamp}{extension}";
                outputPath = Path.Combine(DataPaths.Exports, fileName);
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Generate content
            var content = format switch
            {
                ExportFormat.Markdown => ToMarkdown(conversation),
                ExportFormat.Json => ToJson(conversation),
                _ => ToMarkdown(conversation)
            };

            // Write file
            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, ct).ConfigureAwait(false);

            return outputPath;
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Export failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports multiple conversations to a single archive.
    /// </summary>
    public static async Task<Result<string>> ExportAllAsync(
        IEnumerable<Conversation> conversations,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            outputPath ??= Path.Combine(DataPaths.Exports, $"incontrol-export-{timestamp}.zip");

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var zipStream = new FileStream(outputPath, FileMode.Create);
            using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create);

            // Add manifest
            var manifest = new ExportManifest
            {
                Version = 1,
                ExportedAt = DateTimeOffset.UtcNow,
                ConversationCount = conversations.Count()
            };
            var manifestEntry = archive.CreateEntry("manifest.json");
            await using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                await writer.WriteAsync(JsonSerializer.Serialize(manifest, JsonOptions)).ConfigureAwait(false);
            }

            // Add each conversation
            foreach (var conversation in conversations)
            {
                ct.ThrowIfCancellationRequested();

                var safeName = SanitizeFileName(conversation.Title);
                var entryName = $"conversations/{conversation.Id}_{safeName}.json";

                var entry = archive.CreateEntry(entryName);
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync(ToJson(conversation)).ConfigureAwait(false);
            }

            return outputPath;
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("Export");
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Export failed: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(fileName.Length);

        foreach (var c in fileName)
        {
            sanitized.Append(invalidChars.Contains(c) ? '_' : c);
        }

        var result = sanitized.ToString().Trim();
        if (string.IsNullOrEmpty(result))
        {
            result = "conversation";
        }

        // Limit length
        if (result.Length > 50)
        {
            result = result[..50];
        }

        return result;
    }
}

/// <summary>
/// Imports conversations from exported files.
/// </summary>
public static class SessionImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Imports a conversation from a JSON file.
    /// </summary>
    public static async Task<Result<Conversation>> ImportFromJsonAsync(
        string filePath,
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return InControlError.Create(ErrorCode.FileNotFound, $"File not found: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

            // Try to deserialize as ConversationExport first
            try
            {
                var export = JsonSerializer.Deserialize<ConversationExport>(json, JsonOptions);
                if (export?.Conversation != null)
                {
                    // Assign new ID to avoid conflicts
                    var imported = export.Conversation with
                    {
                        Id = Guid.NewGuid(),
                        Title = $"{export.Conversation.Title} (Imported)"
                    };
                    return imported;
                }
            }
            catch (JsonException)
            {
                // Fall through to try direct conversation deserialization
            }

            // Try to deserialize as Conversation directly
            var conversation = JsonSerializer.Deserialize<Conversation>(json, JsonOptions);
            if (conversation != null)
            {
                var imported = conversation with
                {
                    Id = Guid.NewGuid(),
                    Title = $"{conversation.Title} (Imported)"
                };
                return imported;
            }

            return InControlError.Create(ErrorCode.DeserializationFailed, "Failed to parse conversation from JSON");
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("Import");
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports conversations from an archive.
    /// </summary>
    public static async Task<Result<IReadOnlyList<Conversation>>> ImportFromArchiveAsync(
        string archivePath,
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(archivePath))
            {
                return InControlError.Create(ErrorCode.FileNotFound, $"Archive not found: {archivePath}");
            }

            var conversations = new List<Conversation>();

            using var archive = System.IO.Compression.ZipFile.OpenRead(archivePath);

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

                try
                {
                    var export = JsonSerializer.Deserialize<ConversationExport>(json, JsonOptions);
                    if (export?.Conversation != null)
                    {
                        var imported = export.Conversation with
                        {
                            Id = Guid.NewGuid(),
                            Title = $"{export.Conversation.Title} (Imported)"
                        };
                        conversations.Add(imported);
                    }
                }
                catch (JsonException)
                {
                    // Skip invalid entries
                }
            }

            return conversations;
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("Import");
        }
        catch (Exception ex)
        {
            return InControlError.Create(ErrorCode.FileOperationFailed, $"Import failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Export format options.
/// </summary>
public enum ExportFormat
{
    Markdown,
    Json
}

/// <summary>
/// Wrapper for exported conversations.
/// </summary>
public sealed record ConversationExport
{
    public required int Version { get; init; }
    public required DateTimeOffset ExportedAt { get; init; }
    public required Conversation Conversation { get; init; }
}

/// <summary>
/// Manifest for archive exports.
/// </summary>
public sealed record ExportManifest
{
    public required int Version { get; init; }
    public required DateTimeOffset ExportedAt { get; init; }
    public required int ConversationCount { get; init; }
}
