using Microsoft.Extensions.Logging;
using InControl.Core.Models;
using InControl.Core.State;
using InControl.Services.Interfaces;

namespace InControl.Services.Storage;

/// <summary>
/// JSON file-based implementation of conversation persistence.
/// Each conversation is stored as a separate JSON file under the sessions directory.
/// </summary>
public sealed class JsonConversationStorage : IConversationStorage
{
    private readonly IFileStore _fileStore;
    private readonly ILogger<JsonConversationStorage> _logger;
    private const string SessionsDir = "sessions";

    public JsonConversationStorage(IFileStore fileStore, ILogger<JsonConversationStorage> logger)
    {
        _fileStore = fileStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Conversation conversation, CancellationToken ct = default)
    {
        var json = StateSerializer.Serialize(conversation);
        var path = GetPath(conversation.Id);

        var result = await _fileStore.WriteTextAsync(path, json, ct);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to save conversation {Id}: {Error}", conversation.Id, result.Error.Message);
            throw new InvalidOperationException($"Failed to save conversation: {result.Error.Message}");
        }

        _logger.LogDebug("Saved conversation {Id}: {Title}", conversation.Id, conversation.Title);
    }

    /// <inheritdoc />
    public async Task<Conversation?> LoadAsync(Guid id, CancellationToken ct = default)
    {
        var path = GetPath(id);

        if (!await _fileStore.ExistsAsync(path, ct))
            return null;

        var result = await _fileStore.ReadTextAsync(path, ct);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to load conversation {Id}: {Error}", id, result.Error.Message);
            return null;
        }

        var deserialized = StateSerializer.Deserialize<Conversation>(result.Value);
        if (deserialized.IsFailure)
        {
            _logger.LogError("Failed to deserialize conversation {Id}: {Error}", id, deserialized.Error.Message);
            return null;
        }

        return deserialized.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Conversation>> LoadAllAsync(CancellationToken ct = default)
    {
        var conversations = new List<Conversation>();

        var filesResult = await _fileStore.ListFilesAsync(SessionsDir, "*.json", ct);
        if (filesResult.IsFailure)
        {
            _logger.LogWarning("Failed to list session files: {Error}", filesResult.Error.Message);
            return conversations;
        }

        foreach (var filePath in filesResult.Value)
        {
            try
            {
                var textResult = await _fileStore.ReadTextAsync(filePath, ct);
                if (textResult.IsFailure) continue;

                var deserialized = StateSerializer.Deserialize<Conversation>(textResult.Value);
                if (deserialized.IsSuccess)
                {
                    conversations.Add(deserialized.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping corrupt session file: {Path}", filePath);
            }
        }

        // Return sorted by most recently modified
        return conversations.OrderByDescending(c => c.ModifiedAt).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var path = GetPath(id);

        if (!await _fileStore.ExistsAsync(path, ct))
            return false;

        var result = await _fileStore.DeleteAsync(path, ct);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to delete conversation {Id}: {Error}", id, result.Error.Message);
            return false;
        }

        _logger.LogInformation("Deleted conversation {Id}", id);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _fileStore.ExistsAsync(GetPath(id), ct);
    }

    /// <inheritdoc />
    public async Task<string> ExportAsync(Guid id, CancellationToken ct = default)
    {
        var conversation = await LoadAsync(id, ct)
            ?? throw new KeyNotFoundException($"Conversation {id} not found");

        return StateSerializer.Serialize(conversation);
    }

    /// <inheritdoc />
    public Task<Conversation> ImportAsync(string json, CancellationToken ct = default)
    {
        var result = StateSerializer.Deserialize<Conversation>(json);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Invalid conversation JSON: {result.Error.Message}");
        }

        // Assign a new ID to avoid collisions
        var imported = result.Value with { Id = Guid.NewGuid() };
        return Task.FromResult(imported);
    }

    private static string GetPath(Guid id) => Path.Combine(SessionsDir, $"{id}.json");
}
