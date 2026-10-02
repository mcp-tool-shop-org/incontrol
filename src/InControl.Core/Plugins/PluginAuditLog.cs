namespace InControl.Core.Plugins;

/// <summary>
/// Audit logging for plugin operations.
/// All plugin activity is logged and cannot be disabled.
/// </summary>
public interface IPluginAuditLog
{
    /// <summary>
    /// Logs that a plugin was loaded.
    /// </summary>
    void LogPluginLoaded(string pluginId, string version);

    /// <summary>
    /// Logs that a plugin was unloaded.
    /// </summary>
    void LogPluginUnloaded(string pluginId);

    /// <summary>
    /// Logs that a plugin was enabled.
    /// </summary>
    void LogPluginEnabled(string pluginId);

    /// <summary>
    /// Logs that a plugin was disabled.
    /// </summary>
    void LogPluginDisabled(string pluginId);

    /// <summary>
    /// Logs a plugin error.
    /// </summary>
    void LogPluginError(string pluginId, string action, string error);

    /// <summary>
    /// Logs that an action started.
    /// </summary>
    void LogActionStarted(string pluginId, string actionId, Guid executionId);

    /// <summary>
    /// Logs that an action completed.
    /// </summary>
    void LogActionCompleted(string pluginId, string actionId, Guid executionId, bool success, TimeSpan duration);

    /// <summary>
    /// Logs that an action failed.
    /// </summary>
    void LogActionFailed(string pluginId, string actionId, Guid executionId, string error, TimeSpan duration);

    /// <summary>
    /// Logs a resource access attempt by a plugin.
    /// </summary>
    void LogResourceAccess(string pluginId, ResourceAccessType resourceType, string resource, bool permitted, string? details = null);

    /// <summary>
    /// Logs a permission check.
    /// </summary>
    void LogPermissionCheck(string pluginId, PermissionType permissionType, PermissionAccess access, string? scope, bool allowed);

    /// <summary>
    /// Gets recent audit entries.
    /// </summary>
    IReadOnlyList<PluginAuditEntry> GetRecentEntries(int count = 100);

    /// <summary>
    /// Gets audit entries for a specific plugin.
    /// </summary>
    IReadOnlyList<PluginAuditEntry> GetEntriesForPlugin(string pluginId, int count = 100);

    /// <summary>
    /// Gets audit entries filtered by event type.
    /// </summary>
    IReadOnlyList<PluginAuditEntry> GetEntriesByType(PluginAuditEventType eventType, int count = 100);

    /// <summary>
    /// Gets audit entries within a time range.
    /// </summary>
    IReadOnlyList<PluginAuditEntry> GetEntriesInRange(DateTimeOffset start, DateTimeOffset end);

    /// <summary>
    /// Gets statistics for plugin activity.
    /// </summary>
    PluginAuditStatistics GetStatistics();

    /// <summary>
    /// Gets statistics for a specific plugin.
    /// </summary>
    PluginAuditStatistics GetStatisticsForPlugin(string pluginId);

    /// <summary>
    /// Exports audit entries in a structured format.
    /// </summary>
    PluginAuditExport ExportEntries(DateTimeOffset? start = null, DateTimeOffset? end = null);

    /// <summary>
    /// Clears the audit log (operator action only).
    /// </summary>
    void Clear();
}

/// <summary>
/// A single audit log entry.
/// </summary>
public sealed record PluginAuditEntry(
    DateTimeOffset Timestamp,
    string PluginId,
    PluginAuditEventType EventType,
    string? ActionId,
    Guid? ExecutionId,
    bool? Success,
    TimeSpan? Duration,
    string? Details,
    ResourceAccessType? ResourceType = null,
    string? Resource = null,
    PermissionType? PermissionChecked = null,
    PermissionAccess? AccessRequested = null
);

/// <summary>
/// Types of plugin audit events.
/// </summary>
public enum PluginAuditEventType
{
    Loaded,
    Unloaded,
    Enabled,
    Disabled,
    Error,
    ActionStarted,
    ActionCompleted,
    ActionFailed,
    ResourceAccess,
    PermissionCheck
}

/// <summary>
/// Types of resources that plugins can access.
/// </summary>
public enum ResourceAccessType
{
    FileRead,
    FileWrite,
    FileList,
    NetworkRequest,
    MemoryRead,
    MemoryWrite,
    MemorySearch,
    StorageRead,
    StorageWrite,
    StorageDelete
}

/// <summary>
/// Statistics about plugin audit activity.
/// </summary>
public sealed record PluginAuditStatistics(
    int TotalEntries,
    int LoadEvents,
    int UnloadEvents,
    int EnableEvents,
    int DisableEvents,
    int ErrorEvents,
    int ActionStartedEvents,
    int ActionCompletedEvents,
    int ActionFailedEvents,
    int ResourceAccessEvents,
    int PermissionCheckEvents,
    int DeniedResourceAccesses,
    int DeniedPermissionChecks,
    double AverageActionDurationMs,
    double ActionSuccessRate,
    DateTimeOffset? FirstEntry,
    DateTimeOffset? LastEntry,
    IReadOnlyDictionary<string, int> EntriesByPlugin
);

/// <summary>
/// Export format for audit entries.
/// </summary>
public sealed record PluginAuditExport(
    DateTimeOffset ExportedAt,
    DateTimeOffset? RangeStart,
    DateTimeOffset? RangeEnd,
    int EntryCount,
    IReadOnlyList<PluginAuditEntry> Entries,
    PluginAuditStatistics Statistics
);

/// <summary>
/// In-memory implementation of plugin audit log.
/// </summary>
public sealed class InMemoryPluginAuditLog : IPluginAuditLog
{
    private readonly List<PluginAuditEntry> _entries = [];
    private readonly object _lock = new();
    private readonly int _maxEntries;

    public InMemoryPluginAuditLog(int maxEntries = 10000)
    {
        _maxEntries = maxEntries;
    }

    public void LogPluginLoaded(string pluginId, string version)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.Loaded,
            null,
            null,
            null,
            null,
            $"Version: {version}"));
    }

    public void LogPluginUnloaded(string pluginId)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.Unloaded,
            null,
            null,
            null,
            null,
            null));
    }

    public void LogPluginEnabled(string pluginId)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.Enabled,
            null,
            null,
            null,
            null,
            null));
    }

    public void LogPluginDisabled(string pluginId)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.Disabled,
            null,
            null,
            null,
            null,
            null));
    }

    public void LogPluginError(string pluginId, string action, string error)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.Error,
            action,
            null,
            false,
            null,
            error));
    }

    public void LogActionStarted(string pluginId, string actionId, Guid executionId)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.ActionStarted,
            actionId,
            executionId,
            null,
            null,
            null));
    }

    public void LogActionCompleted(string pluginId, string actionId, Guid executionId, bool success, TimeSpan duration)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.ActionCompleted,
            actionId,
            executionId,
            success,
            duration,
            null));
    }

    public void LogActionFailed(string pluginId, string actionId, Guid executionId, string error, TimeSpan duration)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.ActionFailed,
            actionId,
            executionId,
            false,
            duration,
            error));
    }

    public void LogResourceAccess(string pluginId, ResourceAccessType resourceType, string resource, bool permitted, string? details = null)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.ResourceAccess,
            null,
            null,
            permitted,
            null,
            details,
            resourceType,
            resource));
    }

    public void LogPermissionCheck(string pluginId, PermissionType permissionType, PermissionAccess access, string? scope, bool allowed)
    {
        AddEntry(new PluginAuditEntry(
            DateTimeOffset.UtcNow,
            pluginId,
            PluginAuditEventType.PermissionCheck,
            null,
            null,
            allowed,
            null,
            scope,
            null,
            null,
            permissionType,
            access));
    }

    public IReadOnlyList<PluginAuditEntry> GetRecentEntries(int count = 100)
    {
        lock (_lock)
        {
            return _entries
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    public IReadOnlyList<PluginAuditEntry> GetEntriesForPlugin(string pluginId, int count = 100)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.PluginId == pluginId)
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    public IReadOnlyList<PluginAuditEntry> GetEntriesByType(PluginAuditEventType eventType, int count = 100)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.EventType == eventType)
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    public IReadOnlyList<PluginAuditEntry> GetEntriesInRange(DateTimeOffset start, DateTimeOffset end)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Timestamp >= start && e.Timestamp <= end)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }
    }

    public PluginAuditStatistics GetStatistics()
    {
        lock (_lock)
        {
            return ComputeStatistics(_entries);
        }
    }

    public PluginAuditStatistics GetStatisticsForPlugin(string pluginId)
    {
        lock (_lock)
        {
            var pluginEntries = _entries.Where(e => e.PluginId == pluginId).ToList();
            return ComputeStatistics(pluginEntries);
        }
    }

    public PluginAuditExport ExportEntries(DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        lock (_lock)
        {
            var filteredEntries = _entries.AsEnumerable();

            if (start.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.Timestamp >= start.Value);
            }

            if (end.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.Timestamp <= end.Value);
            }

            var entriesList = filteredEntries.OrderByDescending(e => e.Timestamp).ToList();
            var statistics = ComputeStatistics(entriesList);

            return new PluginAuditExport(
                DateTimeOffset.UtcNow,
                start,
                end,
                entriesList.Count,
                entriesList,
                statistics);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private void AddEntry(PluginAuditEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);

            // Trim if over limit
            while (_entries.Count > _maxEntries)
            {
                _entries.RemoveAt(0);
            }
        }
    }

    private static PluginAuditStatistics ComputeStatistics(IReadOnlyList<PluginAuditEntry> entries)
    {
        var loadEvents = entries.Count(e => e.EventType == PluginAuditEventType.Loaded);
        var unloadEvents = entries.Count(e => e.EventType == PluginAuditEventType.Unloaded);
        var enableEvents = entries.Count(e => e.EventType == PluginAuditEventType.Enabled);
        var disableEvents = entries.Count(e => e.EventType == PluginAuditEventType.Disabled);
        var errorEvents = entries.Count(e => e.EventType == PluginAuditEventType.Error);
        var actionStarted = entries.Count(e => e.EventType == PluginAuditEventType.ActionStarted);
        var actionCompleted = entries.Count(e => e.EventType == PluginAuditEventType.ActionCompleted);
        var actionFailed = entries.Count(e => e.EventType == PluginAuditEventType.ActionFailed);
        var resourceAccess = entries.Count(e => e.EventType == PluginAuditEventType.ResourceAccess);
        var permissionCheck = entries.Count(e => e.EventType == PluginAuditEventType.PermissionCheck);

        var deniedResources = entries.Count(e =>
            e.EventType == PluginAuditEventType.ResourceAccess && e.Success == false);
        var deniedPermissions = entries.Count(e =>
            e.EventType == PluginAuditEventType.PermissionCheck && e.Success == false);

        var completedActions = entries
            .Where(e => e.EventType == PluginAuditEventType.ActionCompleted && e.Duration.HasValue)
            .ToList();

        var avgDuration = completedActions.Count > 0
            ? completedActions.Average(e => e.Duration!.Value.TotalMilliseconds)
            : 0;

        var successfulActions = completedActions.Count(e => e.Success == true);
        var successRate = completedActions.Count > 0
            ? (double)successfulActions / completedActions.Count
            : 0;

        var entriesByPlugin = entries
            .GroupBy(e => e.PluginId)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PluginAuditStatistics(
            TotalEntries: entries.Count,
            LoadEvents: loadEvents,
            UnloadEvents: unloadEvents,
            EnableEvents: enableEvents,
            DisableEvents: disableEvents,
            ErrorEvents: errorEvents,
            ActionStartedEvents: actionStarted,
            ActionCompletedEvents: actionCompleted,
            ActionFailedEvents: actionFailed,
            ResourceAccessEvents: resourceAccess,
            PermissionCheckEvents: permissionCheck,
            DeniedResourceAccesses: deniedResources,
            DeniedPermissionChecks: deniedPermissions,
            AverageActionDurationMs: avgDuration,
            ActionSuccessRate: successRate,
            FirstEntry: entries.MinBy(e => e.Timestamp)?.Timestamp,
            LastEntry: entries.MaxBy(e => e.Timestamp)?.Timestamp,
            EntriesByPlugin: entriesByPlugin);
    }
}
