using InControl.Core.Assistant;

namespace InControl.Core.Policy;

/// <summary>
/// Wraps a MemoryConsentManager with policy enforcement.
/// Memory operations are governed by retention, limits, and category policies.
/// </summary>
public sealed class PolicyGovernedMemoryManager
{
    private readonly MemoryConsentManager _consentManager;
    private readonly AssistantMemoryStore _store;
    private readonly PolicyEngine _policyEngine;
    private readonly object _lock = new();
    private DateTimeOffset _lastRetentionCheck = DateTimeOffset.MinValue;

    /// <summary>
    /// Event raised when memory is blocked by policy.
    /// </summary>
    public event EventHandler<MemoryBlockedEventArgs>? MemoryBlocked;

    /// <summary>
    /// Event raised when memories are purged by retention policy.
    /// </summary>
    public event EventHandler<MemoriesPurgedEventArgs>? MemoriesPurged;

    public PolicyGovernedMemoryManager(
        MemoryConsentManager consentManager,
        AssistantMemoryStore store,
        PolicyEngine policyEngine)
    {
        _consentManager = consentManager ?? throw new ArgumentNullException(nameof(consentManager));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
    }

    /// <summary>
    /// Gets the underlying consent manager.
    /// </summary>
    public MemoryConsentManager ConsentManager => _consentManager;

    /// <summary>
    /// Gets the underlying memory store.
    /// </summary>
    public AssistantMemoryStore Store => _store;

    /// <summary>
    /// Gets the policy engine.
    /// </summary>
    public PolicyEngine PolicyEngine => _policyEngine;

    /// <summary>
    /// Gets the current memory policy evaluation.
    /// </summary>
    public MemoryPolicyEvaluation GetCurrentPolicy()
    {
        return _policyEngine.EvaluateMemoryPolicy();
    }

    /// <summary>
    /// Checks if memory operations are allowed.
    /// </summary>
    public MemoryPolicyStatus CheckMemoryPolicy()
    {
        var policy = GetCurrentPolicy();

        if (!policy.Enabled)
        {
            return new MemoryPolicyStatus(
                CanRemember: false,
                CanExport: false,
                CanImport: false,
                AutoFormationAllowed: false,
                Reason: "Memory is disabled by policy",
                MemoryCount: _store.Count,
                MaxMemories: policy.MaxMemories,
                RetentionDays: policy.MaxRetentionDays);
        }

        var atCapacity = policy.MaxMemories > 0 && _store.Count >= policy.MaxMemories;

        return new MemoryPolicyStatus(
            CanRemember: !atCapacity,
            CanExport: policy.AllowExport,
            CanImport: policy.AllowImport,
            AutoFormationAllowed: policy.AutoFormation,
            Reason: atCapacity ? $"Memory limit reached ({policy.MaxMemories})" : null,
            MemoryCount: _store.Count,
            MaxMemories: policy.MaxMemories,
            RetentionDays: policy.MaxRetentionDays);
    }

    /// <summary>
    /// Checks if a specific memory category is allowed.
    /// </summary>
    public bool IsCategoryAllowed(string category)
    {
        var policy = GetCurrentPolicy();
        return !policy.ExcludeCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Requests to remember something with policy checks.
    /// </summary>
    public MemoryRequestResult RequestRemember(
        MemoryType type,
        string key,
        string value,
        string justification,
        string? category = null,
        MemorySource source = MemorySource.Inferred,
        double confidence = 0.8)
    {
        var policyStatus = CheckMemoryPolicy();

        // Check if memory is enabled
        if (!policyStatus.CanRemember)
        {
            MemoryBlocked?.Invoke(this, new MemoryBlockedEventArgs(
                key, value, policyStatus.Reason ?? "Memory disabled", MemoryBlockReason.PolicyDisabled));

            return MemoryRequestResult.Blocked(policyStatus.Reason ?? "Memory is disabled or at capacity");
        }

        // Check auto-formation policy
        if (source == MemorySource.Inferred && !policyStatus.AutoFormationAllowed)
        {
            MemoryBlocked?.Invoke(this, new MemoryBlockedEventArgs(
                key, value, "Auto-formation disabled", MemoryBlockReason.AutoFormationDisabled));

            return MemoryRequestResult.Blocked("Automatic memory formation is disabled by policy");
        }

        // Check category exclusion
        if (category != null && !IsCategoryAllowed(category))
        {
            MemoryBlocked?.Invoke(this, new MemoryBlockedEventArgs(
                key, value, $"Category '{category}' is excluded", MemoryBlockReason.CategoryExcluded));

            return MemoryRequestResult.Blocked($"Memory category '{category}' is excluded by policy");
        }

        // Forward to consent manager
        var request = _consentManager.RequestRemember(type, key, value, justification, source, confidence);

        return MemoryRequestResult.Pending(request);
    }

    /// <summary>
    /// Remembers something explicitly with policy checks.
    /// </summary>
    public MemoryRequestResult RememberExplicit(
        MemoryType type,
        string key,
        string value,
        string? category = null,
        MemoryScope scope = MemoryScope.User)
    {
        var policyStatus = CheckMemoryPolicy();

        if (!policyStatus.CanRemember)
        {
            MemoryBlocked?.Invoke(this, new MemoryBlockedEventArgs(
                key, value, policyStatus.Reason ?? "Memory disabled", MemoryBlockReason.PolicyDisabled));

            return MemoryRequestResult.Blocked(policyStatus.Reason ?? "Memory is disabled or at capacity");
        }

        if (category != null && !IsCategoryAllowed(category))
        {
            MemoryBlocked?.Invoke(this, new MemoryBlockedEventArgs(
                key, value, $"Category '{category}' is excluded", MemoryBlockReason.CategoryExcluded));

            return MemoryRequestResult.Blocked($"Memory category '{category}' is excluded by policy");
        }

        var memory = _consentManager.RememberExplicit(type, key, value, scope);

        return MemoryRequestResult.Created(memory);
    }

    /// <summary>
    /// Exports memories if policy allows.
    /// </summary>
    public MemoryExportResult Export()
    {
        var policy = GetCurrentPolicy();

        if (!policy.AllowExport)
        {
            return MemoryExportResult.Blocked("Memory export is disabled by policy");
        }

        var json = _store.ExportToJson();
        return MemoryExportResult.Success(json, _store.Count);
    }

    /// <summary>
    /// Checks if import is allowed.
    /// </summary>
    public bool CanImport()
    {
        return GetCurrentPolicy().AllowImport;
    }

    /// <summary>
    /// Applies retention policy, removing expired memories.
    /// Returns the number of memories purged.
    /// </summary>
    public int ApplyRetentionPolicy()
    {
        var policy = GetCurrentPolicy();

        // No retention limit set
        if (policy.MaxRetentionDays <= 0)
        {
            _lastRetentionCheck = DateTimeOffset.UtcNow;
            return 0;
        }

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-policy.MaxRetentionDays);
        var toPurge = new List<Guid>();

        foreach (var memory in _store.All)
        {
            // Check if memory is older than retention period
            if (memory.CreatedAt < cutoffDate)
            {
                toPurge.Add(memory.Id);
            }
        }

        foreach (var id in toPurge)
        {
            _store.Remove(id);
        }

        _lastRetentionCheck = DateTimeOffset.UtcNow;

        if (toPurge.Count > 0)
        {
            MemoriesPurged?.Invoke(this, new MemoriesPurgedEventArgs(
                toPurge.Count,
                policy.MaxRetentionDays,
                cutoffDate));
        }

        return toPurge.Count;
    }

    /// <summary>
    /// Enforces the memory count limit by removing oldest memories.
    /// </summary>
    public int EnforceCountLimit()
    {
        var policy = GetCurrentPolicy();

        if (policy.MaxMemories <= 0)
            return 0;

        var excess = _store.Count - policy.MaxMemories;
        if (excess <= 0)
            return 0;

        // Remove oldest memories first
        var toRemove = _store.All
            .OrderBy(m => m.CreatedAt)
            .Take(excess)
            .Select(m => m.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _store.Remove(id);
        }

        if (toRemove.Count > 0)
        {
            MemoriesPurged?.Invoke(this, new MemoriesPurgedEventArgs(
                toRemove.Count,
                0,
                null,
                policy.MaxMemories));
        }

        return toRemove.Count;
    }

    /// <summary>
    /// Gets memory statistics with policy context.
    /// </summary>
    public MemoryPolicyStatistics GetStatistics()
    {
        var policy = GetCurrentPolicy();
        var memories = _store.All;

        var oldestMemory = memories.MinBy(m => m.CreatedAt);
        var newestMemory = memories.MaxBy(m => m.CreatedAt);

        DateTimeOffset? expirationDate = null;
        if (policy.MaxRetentionDays > 0 && oldestMemory != null)
        {
            expirationDate = oldestMemory.CreatedAt.AddDays(policy.MaxRetentionDays);
        }

        return new MemoryPolicyStatistics(
            TotalMemories: memories.Count,
            MaxMemories: policy.MaxMemories,
            CapacityUsedPercent: policy.MaxMemories > 0 ? (memories.Count * 100.0 / policy.MaxMemories) : 0,
            RetentionDays: policy.MaxRetentionDays,
            OldestMemoryDate: oldestMemory?.CreatedAt,
            NewestMemoryDate: newestMemory?.CreatedAt,
            NextExpirationDate: expirationDate,
            MemoriesAtRiskOfExpiration: policy.MaxRetentionDays > 0
                ? memories.Count(m => m.CreatedAt < DateTimeOffset.UtcNow.AddDays(-policy.MaxRetentionDays + 7))
                : 0,
            ByType: memories.GroupBy(m => m.Type).ToDictionary(g => g.Key, g => g.Count()),
            ByScope: memories.GroupBy(m => m.Scope).ToDictionary(g => g.Key, g => g.Count()),
            BySource: memories.GroupBy(m => m.Source).ToDictionary(g => g.Key, g => g.Count()),
            ExcludedCategories: policy.ExcludeCategories.ToList(),
            EncryptionEnabled: policy.EncryptAtRest,
            AutoFormationEnabled: policy.AutoFormation,
            ExportAllowed: policy.AllowExport,
            ImportAllowed: policy.AllowImport);
    }
}

#region Result Types

/// <summary>
/// Status of memory policy.
/// </summary>
public sealed record MemoryPolicyStatus(
    bool CanRemember,
    bool CanExport,
    bool CanImport,
    bool AutoFormationAllowed,
    string? Reason,
    int MemoryCount,
    int MaxMemories,
    int RetentionDays);

/// <summary>
/// Result of a memory request.
/// </summary>
public sealed record MemoryRequestResult
{
    public bool Success { get; init; }
    public bool WasBlocked { get; init; }
    public bool IsPending { get; init; }
    public string? BlockReason { get; init; }
    public MemoryConsentRequest? ConsentRequest { get; init; }
    public AssistantMemoryItem? CreatedMemory { get; init; }

    public static MemoryRequestResult Blocked(string reason) => new()
    {
        Success = false,
        WasBlocked = true,
        BlockReason = reason
    };

    public static MemoryRequestResult Pending(MemoryConsentRequest request) => new()
    {
        Success = false,
        IsPending = true,
        ConsentRequest = request
    };

    public static MemoryRequestResult Created(AssistantMemoryItem memory) => new()
    {
        Success = true,
        CreatedMemory = memory
    };
}

/// <summary>
/// Result of memory export.
/// </summary>
public sealed record MemoryExportResult
{
    public bool IsSuccess { get; init; }
    public string? Json { get; init; }
    public int MemoryCount { get; init; }
    public string? BlockReason { get; init; }

    public static MemoryExportResult Success(string json, int count) => new()
    {
        IsSuccess = true,
        Json = json,
        MemoryCount = count
    };

    public static MemoryExportResult Blocked(string reason) => new()
    {
        IsSuccess = false,
        BlockReason = reason
    };
}

/// <summary>
/// Memory statistics with policy context.
/// </summary>
public sealed record MemoryPolicyStatistics(
    int TotalMemories,
    int MaxMemories,
    double CapacityUsedPercent,
    int RetentionDays,
    DateTimeOffset? OldestMemoryDate,
    DateTimeOffset? NewestMemoryDate,
    DateTimeOffset? NextExpirationDate,
    int MemoriesAtRiskOfExpiration,
    IReadOnlyDictionary<MemoryType, int> ByType,
    IReadOnlyDictionary<MemoryScope, int> ByScope,
    IReadOnlyDictionary<MemorySource, int> BySource,
    IReadOnlyList<string> ExcludedCategories,
    bool EncryptionEnabled,
    bool AutoFormationEnabled,
    bool ExportAllowed,
    bool ImportAllowed);

/// <summary>
/// Reasons for memory being blocked.
/// </summary>
public enum MemoryBlockReason
{
    PolicyDisabled,
    CapacityReached,
    AutoFormationDisabled,
    CategoryExcluded
}

#endregion

#region Event Args

/// <summary>
/// Event args for blocked memory.
/// </summary>
public sealed class MemoryBlockedEventArgs : EventArgs
{
    public string Key { get; }
    public string Value { get; }
    public string Reason { get; }
    public MemoryBlockReason BlockReason { get; }

    public MemoryBlockedEventArgs(string key, string value, string reason, MemoryBlockReason blockReason)
    {
        Key = key;
        Value = value;
        Reason = reason;
        BlockReason = blockReason;
    }
}

/// <summary>
/// Event args for purged memories.
/// </summary>
public sealed class MemoriesPurgedEventArgs : EventArgs
{
    public int Count { get; }
    public int RetentionDays { get; }
    public DateTimeOffset? CutoffDate { get; }
    public int? CountLimit { get; }

    public MemoriesPurgedEventArgs(int count, int retentionDays, DateTimeOffset? cutoffDate, int? countLimit = null)
    {
        Count = count;
        RetentionDays = retentionDays;
        CutoffDate = cutoffDate;
        CountLimit = countLimit;
    }
}

#endregion

/// <summary>
/// Extensions for easy policy integration with memory.
/// </summary>
public static class MemoryPolicyExtensions
{
    /// <summary>
    /// Creates a policy-governed wrapper around consent manager and store.
    /// </summary>
    public static PolicyGovernedMemoryManager WithPolicyEnforcement(
        this MemoryConsentManager consentManager,
        AssistantMemoryStore store,
        PolicyEngine policyEngine)
    {
        return new PolicyGovernedMemoryManager(consentManager, store, policyEngine);
    }
}
