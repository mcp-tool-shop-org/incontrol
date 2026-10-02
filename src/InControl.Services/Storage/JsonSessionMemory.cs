using Microsoft.Extensions.Logging;
using InControl.Core.Assistant;
using InControl.Core.State;
using InControl.Services.Interfaces;

namespace InControl.Services.Storage;

/// <summary>
/// Stores remembered notes in a single JSON file under the app data root.
/// </summary>
public sealed class JsonSessionMemory : ISessionMemory, IDisposable
{
    private const string FileName = "memories.json";
    private readonly IFileStore _files;
    private readonly ILogger<JsonSessionMemory> _logger;
    private readonly List<AssistantMemoryItem> _items = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _loaded;

    public JsonSessionMemory(IFileStore files, ILogger<JsonSessionMemory> logger)
    {
        _files = files;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_loaded)
                return;

            await LoadUnlockedAsync(ct);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AssistantMemoryItem> RememberAsync(AssistantMemoryItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var stored = Clean(item);
            var index = _items.FindIndex(existing => existing.Id == stored.Id);
            if (index >= 0)
                _items[index] = stored;
            else
                _items.Add(stored);

            await SaveUnlockedAsync(ct);
            return stored;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ForgetAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            if (_items.RemoveAll(item => item.Id == id) > 0)
                await SaveUnlockedAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ForgetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            if (_items.RemoveAll(item => item.SessionId == sessionId) > 0)
                await SaveUnlockedAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AssistantMemoryItem>> ListAsync(
        Guid projectId,
        Guid? sessionId,
        CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            return Visible(projectId, sessionId)
                .OrderByDescending(item => item.LastAccessedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> BuildRecallAsync(
        Guid projectId,
        Guid? sessionId,
        string? instructions,
        string? query,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureAsync(ct);
            await _gate.WaitAsync(ct);
            try
            {
                var selected = MemoryRecall.Select(_items, projectId, sessionId, query);
                if (selected.Count > 0)
                {
                    var now = DateTimeOffset.UtcNow;
                    var chosen = selected.Select(note => note.Id).ToHashSet();
                    for (var i = 0; i < _items.Count; i++)
                    {
                        if (chosen.Contains(_items[i].Id))
                            _items[i] = _items[i] with { LastAccessedAt = now };
                    }

                    try
                    {
                        await SaveUnlockedAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not save recalled notes");
                    }
                }

                return MemoryRecall.Format(instructions, selected);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Recall failed");
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private IEnumerable<AssistantMemoryItem> Visible(Guid projectId, Guid? sessionId)
    {
        return _items.Where(item =>
            item.ProjectId == projectId
            && (item.SessionId is null || item.SessionId == sessionId));
    }

    private async Task LoadUnlockedAsync(CancellationToken ct)
    {
        if (!await _files.ExistsAsync(FileName, ct))
        {
            _items.Clear();
            return;
        }

        var read = await _files.ReadTextAsync(FileName, ct);
        if (read.IsFailure)
        {
            _logger.LogWarning("Could not read notes: {Error}", read.Error.Message);
            throw new InvalidOperationException($"Failed to read notes: {read.Error.Message}");
        }

        var parsed = StateSerializer.Deserialize<List<AssistantMemoryItem>>(read.Value);
        var list = parsed.Value;
        if (parsed.IsFailure || list is null)
        {
            var detail = parsed.IsFailure ? parsed.Error.Message : "empty";
            _logger.LogWarning("Could not read notes: {Error}", detail);
            throw new InvalidOperationException($"Failed to read notes: {detail}");
        }

        _items.Clear();
        _items.AddRange(list.Where(item => !string.IsNullOrWhiteSpace(item.Value)));
    }

    private async Task SaveUnlockedAsync(CancellationToken ct)
    {
        var json = StateSerializer.Serialize(_items.ToList());
        var write = await _files.WriteTextAsync(FileName, json, ct);
        if (write.IsFailure)
            throw new InvalidOperationException($"Failed to save notes: {write.Error.Message}");
    }

    private static AssistantMemoryItem Clean(AssistantMemoryItem item)
    {
        var value = item.Value.Trim();
        if (value.Length == 0)
            throw new ArgumentException("A note needs text.");

        if (value.Length > 4000)
            value = value[..4000];

        var key = item.Key.Trim();
        if (key.Length == 0)
            key = value.Length <= 48 ? value : value[..48];
        else if (key.Length > 80)
            key = key[..80];

        return item with { Key = key, Value = value };
    }
}
