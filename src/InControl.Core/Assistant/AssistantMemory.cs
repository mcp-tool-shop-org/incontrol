namespace InControl.Core.Assistant;

/// <summary>
/// Represents a single memory item stored by the assistant.
/// All memory is local, scoped, and attributable.
/// </summary>
public sealed record AssistantMemoryItem
{
    /// <summary>
    /// Unique identifier for this memory item.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The type of information being remembered.
    /// </summary>
    public required MemoryType Type { get; init; }

    /// <summary>
    /// The scope of this memory (how long it persists).
    /// </summary>
    public required MemoryScope Scope { get; init; }

    /// <summary>
    /// How this memory was acquired.
    /// </summary>
    public required MemorySource Source { get; init; }

    /// <summary>
    /// Human-readable key/label for this memory.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The actual content being remembered.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Confidence level for inferred memories.
    /// Always 1.0 for explicit user memories.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// When this memory was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When this memory was last accessed.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; init; }

    /// <summary>
    /// Optional context about why this was remembered.
    /// </summary>
    public string? Justification { get; init; }

    /// <summary>
    /// Creates a new memory item with defaults.
    /// </summary>
    public static AssistantMemoryItem Create(
        MemoryType type,
        MemoryScope scope,
        MemorySource source,
        string key,
        string value,
        string? justification = null,
        double confidence = 1.0) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Scope = scope,
        Source = source,
        Key = key,
        Value = value,
        Confidence = confidence,
        CreatedAt = DateTimeOffset.UtcNow,
        LastAccessedAt = DateTimeOffset.UtcNow,
        Justification = justification
    };
}

/// <summary>
/// Types of information the assistant can remember.
/// </summary>
public enum MemoryType
{
    /// <summary>
    /// User preference (e.g., "prefers concise responses").
    /// </summary>
    Preference,

    /// <summary>
    /// Factual information about the user or context.
    /// </summary>
    Fact,

    /// <summary>
    /// Explicit instruction from the user.
    /// </summary>
    Instruction,

    /// <summary>
    /// Named entity (person, project, etc.).
    /// </summary>
    Entity,

    /// <summary>
    /// Previous decision or outcome.
    /// </summary>
    Decision
}

/// <summary>
/// How long a memory persists.
/// </summary>
public enum MemoryScope
{
    /// <summary>
    /// Only valid for current session.
    /// </summary>
    Session,

    /// <summary>
    /// Persists across sessions for current user.
    /// </summary>
    User,

    /// <summary>
    /// Global setting (rarely used).
    /// </summary>
    Global
}

/// <summary>
/// How the memory was acquired.
/// </summary>
public enum MemorySource
{
    /// <summary>
    /// User explicitly told the assistant to remember this.
    /// </summary>
    ExplicitUser,

    /// <summary>
    /// Inferred from conversation context (requires confirmation).
    /// </summary>
    Inferred,

    /// <summary>
    /// System-generated memory (e.g., error patterns).
    /// </summary>
    System
}

/// <summary>
/// Retention policy for memories.
/// </summary>
public enum RetentionPolicy
{
    /// <summary>
    /// Keep until explicitly deleted.
    /// </summary>
    Permanent,

    /// <summary>
    /// Delete after a period of inactivity.
    /// </summary>
    ExpireOnInactivity,

    /// <summary>
    /// Delete after specific date.
    /// </summary>
    ExpireOnDate,

    /// <summary>
    /// Delete when session ends.
    /// </summary>
    SessionOnly
}

/// <summary>
/// Manages the assistant's memory store.
/// All operations are local and auditable.
/// </summary>
public sealed class AssistantMemoryStore
{
    private readonly Dictionary<Guid, AssistantMemoryItem> _memories = [];
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when memory is added.
    /// </summary>
    public event EventHandler<MemoryChangedEventArgs>? MemoryAdded;

    /// <summary>
    /// Event raised when memory is removed.
    /// </summary>
    public event EventHandler<MemoryChangedEventArgs>? MemoryRemoved;

    /// <summary>
    /// Event raised when memory is updated.
    /// </summary>
    public event EventHandler<MemoryChangedEventArgs>? MemoryUpdated;

    /// <summary>
    /// All memories in the store.
    /// </summary>
    public IReadOnlyList<AssistantMemoryItem> All
    {
        get
        {
            lock (_lock)
            {
                return _memories.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Count of memories.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _memories.Count;
            }
        }
    }

    /// <summary>
    /// Adds a memory item.
    /// </summary>
    public void Add(AssistantMemoryItem item)
    {
        lock (_lock)
        {
            _memories[item.Id] = item;
        }
        MemoryAdded?.Invoke(this, new MemoryChangedEventArgs(item, MemoryChangeType.Added));
    }

    /// <summary>
    /// Gets a memory by ID.
    /// </summary>
    public AssistantMemoryItem? Get(Guid id)
    {
        lock (_lock)
        {
            return _memories.TryGetValue(id, out var item) ? item : null;
        }
    }

    /// <summary>
    /// Updates a memory item.
    /// </summary>
    public bool Update(AssistantMemoryItem item)
    {
        lock (_lock)
        {
            if (!_memories.ContainsKey(item.Id))
                return false;

            _memories[item.Id] = item;
        }
        MemoryUpdated?.Invoke(this, new MemoryChangedEventArgs(item, MemoryChangeType.Updated));
        return true;
    }

    /// <summary>
    /// Removes a memory by ID.
    /// </summary>
    public bool Remove(Guid id)
    {
        AssistantMemoryItem? removed;
        lock (_lock)
        {
            if (!_memories.TryGetValue(id, out removed))
                return false;

            _memories.Remove(id);
        }
        MemoryRemoved?.Invoke(this, new MemoryChangedEventArgs(removed, MemoryChangeType.Removed));
        return true;
    }

    /// <summary>
    /// Clears all memories.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _memories.Clear();
        }
    }

    /// <summary>
    /// Finds memories by type.
    /// </summary>
    public IReadOnlyList<AssistantMemoryItem> FindByType(MemoryType type)
    {
        lock (_lock)
        {
            return _memories.Values.Where(m => m.Type == type).ToList();
        }
    }

    /// <summary>
    /// Finds memories by scope.
    /// </summary>
    public IReadOnlyList<AssistantMemoryItem> FindByScope(MemoryScope scope)
    {
        lock (_lock)
        {
            return _memories.Values.Where(m => m.Scope == scope).ToList();
        }
    }

    /// <summary>
    /// Finds memories by key (partial match).
    /// </summary>
    public IReadOnlyList<AssistantMemoryItem> FindByKey(string keyPattern)
    {
        lock (_lock)
        {
            return _memories.Values
                .Where(m => m.Key.Contains(keyPattern, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Removes session-scoped memories.
    /// </summary>
    public int ClearSessionMemories()
    {
        List<Guid> toRemove;
        lock (_lock)
        {
            toRemove = _memories.Values
                .Where(m => m.Scope == MemoryScope.Session)
                .Select(m => m.Id)
                .ToList();

            foreach (var id in toRemove)
            {
                _memories.Remove(id);
            }
        }
        return toRemove.Count;
    }

    /// <summary>
    /// Exports all memories to JSON.
    /// </summary>
    public string ExportToJson()
    {
        lock (_lock)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                _memories.Values.ToList(),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
            );
        }
    }
}

/// <summary>
/// Event args for memory changes.
/// </summary>
public sealed class MemoryChangedEventArgs : EventArgs
{
    public AssistantMemoryItem Memory { get; }
    public MemoryChangeType ChangeType { get; }

    public MemoryChangedEventArgs(AssistantMemoryItem memory, MemoryChangeType changeType)
    {
        Memory = memory;
        ChangeType = changeType;
    }
}

/// <summary>
/// Types of memory changes.
/// </summary>
public enum MemoryChangeType
{
    Added,
    Updated,
    Removed
}
