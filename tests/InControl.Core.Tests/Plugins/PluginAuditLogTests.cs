using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// Tests for PluginAuditLog and traceability features.
/// </summary>
public class PluginAuditLogTests
{
    private readonly InMemoryPluginAuditLog _auditLog;

    public PluginAuditLogTests()
    {
        _auditLog = new InMemoryPluginAuditLog();
    }

    #region Basic Logging Tests

    [Fact]
    public void LogPluginLoaded_RecordsEntry()
    {
        _auditLog.LogPluginLoaded("test-plugin", "1.0.0");

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal("test-plugin", entries[0].PluginId);
        Assert.Equal(PluginAuditEventType.Loaded, entries[0].EventType);
        Assert.Contains("1.0.0", entries[0].Details);
    }

    [Fact]
    public void LogPluginUnloaded_RecordsEntry()
    {
        _auditLog.LogPluginUnloaded("test-plugin");

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.Unloaded, entries[0].EventType);
    }

    [Fact]
    public void LogPluginEnabled_RecordsEntry()
    {
        _auditLog.LogPluginEnabled("test-plugin");

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.Enabled, entries[0].EventType);
    }

    [Fact]
    public void LogPluginDisabled_RecordsEntry()
    {
        _auditLog.LogPluginDisabled("test-plugin");

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.Disabled, entries[0].EventType);
    }

    [Fact]
    public void LogPluginError_RecordsEntry()
    {
        _auditLog.LogPluginError("test-plugin", "test-action", "Test error");

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.Error, entries[0].EventType);
        Assert.Equal("test-action", entries[0].ActionId);
        Assert.Equal("Test error", entries[0].Details);
    }

    [Fact]
    public void LogActionStarted_RecordsEntry()
    {
        var executionId = Guid.NewGuid();
        _auditLog.LogActionStarted("test-plugin", "action-1", executionId);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.ActionStarted, entries[0].EventType);
        Assert.Equal("action-1", entries[0].ActionId);
        Assert.Equal(executionId, entries[0].ExecutionId);
    }

    [Fact]
    public void LogActionCompleted_RecordsEntry()
    {
        var executionId = Guid.NewGuid();
        var duration = TimeSpan.FromMilliseconds(250);

        _auditLog.LogActionCompleted("test-plugin", "action-1", executionId, true, duration);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.ActionCompleted, entries[0].EventType);
        Assert.True(entries[0].Success);
        Assert.Equal(duration, entries[0].Duration);
    }

    [Fact]
    public void LogActionFailed_RecordsEntry()
    {
        var executionId = Guid.NewGuid();
        var duration = TimeSpan.FromMilliseconds(100);

        _auditLog.LogActionFailed("test-plugin", "action-1", executionId, "Execution error", duration);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.ActionFailed, entries[0].EventType);
        Assert.False(entries[0].Success);
        Assert.Equal("Execution error", entries[0].Details);
    }

    #endregion

    #region Resource Access Logging Tests

    [Fact]
    public void LogResourceAccess_RecordsFileReadEntry()
    {
        _auditLog.LogResourceAccess("test-plugin", ResourceAccessType.FileRead, "/data/file.txt", true);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.ResourceAccess, entries[0].EventType);
        Assert.Equal(ResourceAccessType.FileRead, entries[0].ResourceType);
        Assert.Equal("/data/file.txt", entries[0].Resource);
        Assert.True(entries[0].Success);
    }

    [Fact]
    public void LogResourceAccess_RecordsDeniedAccess()
    {
        _auditLog.LogResourceAccess("test-plugin", ResourceAccessType.FileWrite, "/forbidden/file.txt", false);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.False(entries[0].Success);
    }

    [Fact]
    public void LogResourceAccess_RecordsNetworkRequest()
    {
        _auditLog.LogResourceAccess("test-plugin", ResourceAccessType.NetworkRequest, "https://api.example.com", true, "GET: Fetch data");

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(ResourceAccessType.NetworkRequest, entries[0].ResourceType);
        Assert.Equal("https://api.example.com", entries[0].Resource);
        Assert.Equal("GET: Fetch data", entries[0].Details);
    }

    [Fact]
    public void LogResourceAccess_RecordsMemoryAccess()
    {
        _auditLog.LogResourceAccess("test-plugin", ResourceAccessType.MemoryRead, "user-preferences", true);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(ResourceAccessType.MemoryRead, entries[0].ResourceType);
    }

    [Fact]
    public void LogResourceAccess_RecordsStorageAccess()
    {
        _auditLog.LogResourceAccess("test-plugin", ResourceAccessType.StorageWrite, "cache-key", true);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(ResourceAccessType.StorageWrite, entries[0].ResourceType);
    }

    #endregion

    #region Permission Check Logging Tests

    [Fact]
    public void LogPermissionCheck_RecordsAllowedPermission()
    {
        _auditLog.LogPermissionCheck("test-plugin", PermissionType.File, PermissionAccess.Read, "/data", true);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.Equal(PluginAuditEventType.PermissionCheck, entries[0].EventType);
        Assert.Equal(PermissionType.File, entries[0].PermissionChecked);
        Assert.Equal(PermissionAccess.Read, entries[0].AccessRequested);
        Assert.True(entries[0].Success);
    }

    [Fact]
    public void LogPermissionCheck_RecordsDeniedPermission()
    {
        _auditLog.LogPermissionCheck("test-plugin", PermissionType.Network, PermissionAccess.Write, "https://evil.com", false);

        var entries = _auditLog.GetRecentEntries();
        Assert.Single(entries);
        Assert.False(entries[0].Success);
        Assert.Equal(PermissionType.Network, entries[0].PermissionChecked);
    }

    #endregion

    #region Query Tests

    [Fact]
    public void GetEntriesByType_FiltersCorrectly()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginEnabled("plugin-1");
        _auditLog.LogPluginLoaded("plugin-2", "2.0.0");
        _auditLog.LogPluginDisabled("plugin-1");

        var loadedEntries = _auditLog.GetEntriesByType(PluginAuditEventType.Loaded);
        Assert.Equal(2, loadedEntries.Count);
        Assert.All(loadedEntries, e => Assert.Equal(PluginAuditEventType.Loaded, e.EventType));
    }

    [Fact]
    public void GetEntriesForPlugin_FiltersCorrectly()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginLoaded("plugin-2", "2.0.0");
        _auditLog.LogPluginEnabled("plugin-1");
        _auditLog.LogPluginEnabled("plugin-2");

        var plugin1Entries = _auditLog.GetEntriesForPlugin("plugin-1");
        Assert.Equal(2, plugin1Entries.Count);
        Assert.All(plugin1Entries, e => Assert.Equal("plugin-1", e.PluginId));
    }

    [Fact]
    public void GetEntriesInRange_FiltersCorrectly()
    {
        var start = DateTimeOffset.UtcNow;

        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        Thread.Sleep(10); // Ensure time passes

        var middle = DateTimeOffset.UtcNow;

        _auditLog.LogPluginEnabled("plugin-1");
        Thread.Sleep(10);

        var end = DateTimeOffset.UtcNow;

        // Query for entries after middle
        var entriesAfterMiddle = _auditLog.GetEntriesInRange(middle, end);
        Assert.Single(entriesAfterMiddle);
        Assert.Equal(PluginAuditEventType.Enabled, entriesAfterMiddle[0].EventType);
    }

    [Fact]
    public void GetRecentEntries_LimitsCount()
    {
        for (int i = 0; i < 10; i++)
        {
            _auditLog.LogPluginLoaded($"plugin-{i}", "1.0.0");
        }

        var entries = _auditLog.GetRecentEntries(5);
        Assert.Equal(5, entries.Count);
    }

    [Fact]
    public void GetRecentEntries_ReturnsInDescendingOrder()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        Thread.Sleep(10);
        _auditLog.LogPluginLoaded("plugin-2", "2.0.0");

        var entries = _auditLog.GetRecentEntries();
        Assert.Equal("plugin-2", entries[0].PluginId);
        Assert.Equal("plugin-1", entries[1].PluginId);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginLoaded("plugin-2", "2.0.0");
        _auditLog.LogPluginEnabled("plugin-1");
        _auditLog.LogPluginDisabled("plugin-2");
        _auditLog.LogPluginError("plugin-1", "action", "error");

        var stats = _auditLog.GetStatistics();

        Assert.Equal(5, stats.TotalEntries);
        Assert.Equal(2, stats.LoadEvents);
        Assert.Equal(1, stats.EnableEvents);
        Assert.Equal(1, stats.DisableEvents);
        Assert.Equal(1, stats.ErrorEvents);
    }

    [Fact]
    public void GetStatistics_CalculatesActionMetrics()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        _auditLog.LogActionStarted("plugin-1", "action-1", id1);
        _auditLog.LogActionCompleted("plugin-1", "action-1", id1, true, TimeSpan.FromMilliseconds(100));

        _auditLog.LogActionStarted("plugin-1", "action-2", id2);
        _auditLog.LogActionCompleted("plugin-1", "action-2", id2, true, TimeSpan.FromMilliseconds(200));

        _auditLog.LogActionStarted("plugin-1", "action-3", id3);
        _auditLog.LogActionFailed("plugin-1", "action-3", id3, "error", TimeSpan.FromMilliseconds(50));

        var stats = _auditLog.GetStatistics();

        Assert.Equal(3, stats.ActionStartedEvents);
        Assert.Equal(2, stats.ActionCompletedEvents);
        Assert.Equal(1, stats.ActionFailedEvents);
        Assert.Equal(150, stats.AverageActionDurationMs); // (100 + 200) / 2
        Assert.Equal(1.0, stats.ActionSuccessRate); // Both completed actions succeeded
    }

    [Fact]
    public void GetStatistics_CountsDeniedAccesses()
    {
        _auditLog.LogResourceAccess("plugin-1", ResourceAccessType.FileRead, "/allowed", true);
        _auditLog.LogResourceAccess("plugin-1", ResourceAccessType.FileRead, "/denied", false);
        _auditLog.LogResourceAccess("plugin-1", ResourceAccessType.NetworkRequest, "https://blocked.com", false);

        _auditLog.LogPermissionCheck("plugin-1", PermissionType.File, PermissionAccess.Read, "/data", true);
        _auditLog.LogPermissionCheck("plugin-1", PermissionType.Network, PermissionAccess.Write, null, false);

        var stats = _auditLog.GetStatistics();

        Assert.Equal(3, stats.ResourceAccessEvents);
        Assert.Equal(2, stats.DeniedResourceAccesses);
        Assert.Equal(2, stats.PermissionCheckEvents);
        Assert.Equal(1, stats.DeniedPermissionChecks);
    }

    [Fact]
    public void GetStatistics_GroupsByPlugin()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginEnabled("plugin-1");
        _auditLog.LogPluginLoaded("plugin-2", "2.0.0");

        var stats = _auditLog.GetStatistics();

        Assert.Equal(2, stats.EntriesByPlugin["plugin-1"]);
        Assert.Equal(1, stats.EntriesByPlugin["plugin-2"]);
    }

    [Fact]
    public void GetStatisticsForPlugin_ReturnsPluginSpecificStats()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginLoaded("plugin-2", "2.0.0");
        _auditLog.LogPluginEnabled("plugin-1");
        _auditLog.LogPluginEnabled("plugin-2");
        _auditLog.LogPluginDisabled("plugin-1");

        var plugin1Stats = _auditLog.GetStatisticsForPlugin("plugin-1");
        var plugin2Stats = _auditLog.GetStatisticsForPlugin("plugin-2");

        Assert.Equal(3, plugin1Stats.TotalEntries);
        Assert.Equal(1, plugin1Stats.DisableEvents);

        Assert.Equal(2, plugin2Stats.TotalEntries);
        Assert.Equal(0, plugin2Stats.DisableEvents);
    }

    #endregion

    #region Export Tests

    [Fact]
    public void ExportEntries_ReturnsAllEntries()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginEnabled("plugin-1");
        _auditLog.LogPluginDisabled("plugin-1");

        var export = _auditLog.ExportEntries();

        Assert.Equal(3, export.EntryCount);
        Assert.Equal(3, export.Entries.Count);
        Assert.NotNull(export.Statistics);
        Assert.True(export.ExportedAt <= DateTimeOffset.UtcNow);
        Assert.Null(export.RangeStart);
        Assert.Null(export.RangeEnd);
    }

    [Fact]
    public void ExportEntries_FiltersbyTimeRange()
    {
        var beforeStart = DateTimeOffset.UtcNow;
        _auditLog.LogPluginLoaded("plugin-old", "1.0.0");
        Thread.Sleep(10);

        var start = DateTimeOffset.UtcNow;
        Thread.Sleep(10);

        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginEnabled("plugin-1");
        Thread.Sleep(10);

        var end = DateTimeOffset.UtcNow;

        var export = _auditLog.ExportEntries(start, end);

        Assert.Equal(2, export.EntryCount);
        Assert.Equal(start, export.RangeStart);
        Assert.Equal(end, export.RangeEnd);
    }

    [Fact]
    public void ExportEntries_IncludesStatisticsForFilteredEntries()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginEnabled("plugin-1");

        var export = _auditLog.ExportEntries();

        Assert.Equal(2, export.Statistics.TotalEntries);
        Assert.Equal(1, export.Statistics.LoadEvents);
        Assert.Equal(1, export.Statistics.EnableEvents);
    }

    #endregion

    #region Capacity and Clear Tests

    [Fact]
    public void MaxEntries_TrimsOldEntries()
    {
        var smallLog = new InMemoryPluginAuditLog(maxEntries: 3);

        smallLog.LogPluginLoaded("plugin-1", "1.0.0");
        smallLog.LogPluginLoaded("plugin-2", "2.0.0");
        smallLog.LogPluginLoaded("plugin-3", "3.0.0");
        smallLog.LogPluginLoaded("plugin-4", "4.0.0");

        var entries = smallLog.GetRecentEntries();
        Assert.Equal(3, entries.Count);
        Assert.DoesNotContain(entries, e => e.PluginId == "plugin-1");
        Assert.Contains(entries, e => e.PluginId == "plugin-4");
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _auditLog.LogPluginLoaded("plugin-1", "1.0.0");
        _auditLog.LogPluginEnabled("plugin-1");

        _auditLog.Clear();

        var entries = _auditLog.GetRecentEntries();
        Assert.Empty(entries);
    }

    #endregion
}
