using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using InControl.Core.Plugins;

namespace InControl.ViewModels.Plugins;

/// <summary>
/// ViewModel for managing plugins - install, enable, disable, review.
/// Provides operator visibility into plugin operations.
/// </summary>
public partial class PluginManagerViewModel : ViewModelBase
{
    private readonly PluginHost _host;
    private readonly IPluginAuditLog _auditLog;

    /// <summary>
    /// All loaded plugins.
    /// </summary>
    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    /// <summary>
    /// Currently selected plugin for detail view.
    /// </summary>
    [ObservableProperty]
    private PluginItemViewModel? _selectedPlugin;

    /// <summary>
    /// Count of enabled plugins.
    /// </summary>
    [ObservableProperty]
    private int _enabledCount;

    /// <summary>
    /// Count of total loaded plugins.
    /// </summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>
    /// Whether there are any plugins loaded.
    /// </summary>
    public bool HasPlugins => Plugins.Count > 0;

    /// <summary>
    /// Status summary text.
    /// </summary>
    [ObservableProperty]
    private string _statusSummary = "No plugins installed";

    /// <summary>
    /// Recent plugin activity entries.
    /// </summary>
    public ObservableCollection<PluginActivityEntry> RecentActivity { get; } = [];

    /// <summary>
    /// Whether there is any plugin activity to show.
    /// </summary>
    public bool HasActivity => RecentActivity.Count > 0;

    /// <summary>
    /// Current audit statistics.
    /// </summary>
    [ObservableProperty]
    private PluginAuditStatistics? _auditStatistics;

    /// <summary>
    /// Formatted statistics summary for display.
    /// </summary>
    public string StatisticsSummary => AuditStatistics switch
    {
        null => "No activity recorded",
        { TotalEntries: 0 } => "No activity recorded",
        var s => $"{s.TotalEntries} events | {s.ActionCompletedEvents} actions ({s.ActionSuccessRate:P0} success) | {s.DeniedResourceAccesses + s.DeniedPermissionChecks} denied"
    };

    public PluginManagerViewModel(
        PluginHost host,
        IPluginAuditLog auditLog,
        ILogger<PluginManagerViewModel> logger)
        : base(logger)
    {
        _host = host;
        _auditLog = auditLog;

        // Subscribe to plugin events
        _host.PluginLoaded += OnPluginLoaded;
        _host.PluginUnloaded += OnPluginUnloaded;

        // Load initial state
        RefreshPlugins();
        LoadRecentActivity();
        RefreshStatistics();
    }

    /// <summary>
    /// Refreshes the plugin list from the host.
    /// </summary>
    [RelayCommand]
    private void RefreshPlugins()
    {
        Plugins.Clear();

        foreach (var plugin in _host.LoadedPlugins)
        {
            var vm = new PluginItemViewModel(plugin, this);
            Plugins.Add(vm);
        }

        UpdateCounts();
        OnPropertyChanged(nameof(HasPlugins));
    }

    /// <summary>
    /// Updates the count summaries.
    /// </summary>
    private void UpdateCounts()
    {
        TotalCount = Plugins.Count;
        EnabledCount = Plugins.Count(p => p.IsEnabled);
        StatusSummary = TotalCount switch
        {
            0 => "No plugins installed",
            1 when EnabledCount == 1 => "1 plugin enabled",
            1 => "1 plugin (disabled)",
            _ => $"{EnabledCount} of {TotalCount} plugins enabled"
        };
    }

    /// <summary>
    /// Enables a plugin.
    /// </summary>
    public bool EnablePlugin(string pluginId)
    {
        var result = _host.EnablePlugin(pluginId);
        if (result)
        {
            RefreshPlugins(); // Refresh to get updated state
            Logger.LogInformation("Plugin enabled: {PluginId}", pluginId);
        }
        return result;
    }

    /// <summary>
    /// Disables a plugin.
    /// </summary>
    public bool DisablePlugin(string pluginId)
    {
        var result = _host.DisablePlugin(pluginId);
        if (result)
        {
            RefreshPlugins(); // Refresh to get updated state
            Logger.LogInformation("Plugin disabled: {PluginId}", pluginId);
        }
        return result;
    }

    /// <summary>
    /// Unloads a plugin.
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        var result = await _host.UnloadPluginAsync(pluginId);
        if (result)
        {
            RefreshPlugins();
            Logger.LogInformation("Plugin unloaded: {PluginId}", pluginId);
        }
        return result;
    }

    /// <summary>
    /// Disables all plugins (panic button).
    /// </summary>
    [RelayCommand]
    private void DisableAllPlugins()
    {
        _host.DisableAllPlugins();
        RefreshPlugins();
        Logger.LogWarning("All plugins disabled by user");
    }

    /// <summary>
    /// Loads recent plugin activity from the audit log.
    /// </summary>
    private void LoadRecentActivity()
    {
        var entries = _auditLog.GetRecentEntries(50)
            .Select(e => new PluginActivityEntry(e))
            .ToList();

        RecentActivity.Clear();
        foreach (var entry in entries)
        {
            RecentActivity.Add(entry);
        }

        OnPropertyChanged(nameof(HasActivity));
    }

    /// <summary>
    /// Refreshes audit statistics.
    /// </summary>
    [RelayCommand]
    private void RefreshStatistics()
    {
        AuditStatistics = _auditLog.GetStatistics();
        OnPropertyChanged(nameof(StatisticsSummary));
    }

    /// <summary>
    /// Clears the plugin audit log.
    /// </summary>
    [RelayCommand]
    private void ClearAuditLog()
    {
        _auditLog.Clear();
        RecentActivity.Clear();
        RefreshStatistics();
        OnPropertyChanged(nameof(HasActivity));
        Logger.LogInformation("User cleared plugin audit log");
    }

    /// <summary>
    /// Exports the audit log for review.
    /// </summary>
    public PluginAuditExport ExportAuditLog()
    {
        var export = _auditLog.ExportEntries();
        Logger.LogInformation("Exported {Count} audit entries", export.EntryCount);
        return export;
    }

    private void OnPluginLoaded(object? sender, PluginLoadedEventArgs e)
    {
        var vm = new PluginItemViewModel(e.Plugin, this);
        Plugins.Add(vm);
        UpdateCounts();
        OnPropertyChanged(nameof(HasPlugins));
    }

    private void OnPluginUnloaded(object? sender, PluginUnloadedEventArgs e)
    {
        var vm = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
        if (vm != null)
        {
            Plugins.Remove(vm);
        }
        UpdateCounts();
        OnPropertyChanged(nameof(HasPlugins));
    }
}

/// <summary>
/// ViewModel for a single plugin in the list.
/// </summary>
public partial class PluginItemViewModel : ObservableObject
{
    private readonly LoadedPlugin _plugin;
    private readonly PluginManagerViewModel _manager;

    public string PluginId => _plugin.Manifest.Id;
    public string Name => _plugin.Manifest.Name;
    public string Version => _plugin.Manifest.Version;
    public string Author => _plugin.Manifest.Author;
    public string Description => _plugin.Manifest.Description;
    public DateTimeOffset LoadedAt => _plugin.LoadedAt;

    public PluginState State => _plugin.State;
    public bool IsEnabled => _plugin.State == PluginState.Enabled;
    public bool IsDisabled => _plugin.State == PluginState.Disabled;
    public bool IsFaulted => _plugin.State == PluginState.Faulted;

    public string StateIcon => _plugin.State switch
    {
        PluginState.Enabled => "CheckCircle",
        PluginState.Disabled => "PauseCircle",
        PluginState.Faulted => "XCircle",
        _ => "QuestionCircle"
    };

    public string StateColor => _plugin.State switch
    {
        PluginState.Enabled => "Green",
        PluginState.Disabled => "Gray",
        PluginState.Faulted => "Red",
        _ => "Orange"
    };

    public PluginRiskLevel RiskLevel => _plugin.Manifest.RiskLevel;

    public string RiskLevelDisplay => _plugin.Manifest.RiskLevel switch
    {
        PluginRiskLevel.ReadOnly => "Read-Only",
        PluginRiskLevel.LocalMutation => "Local Mutation",
        PluginRiskLevel.Network => "Network Access",
        PluginRiskLevel.SystemAdjacent => "System Access",
        _ => "Unknown"
    };

    public string RiskLevelColor => _plugin.Manifest.RiskLevel switch
    {
        PluginRiskLevel.ReadOnly => "Green",
        PluginRiskLevel.LocalMutation => "Orange",
        PluginRiskLevel.Network => "Red",
        PluginRiskLevel.SystemAdjacent => "DarkRed",
        _ => "Gray"
    };

    public int CapabilityCount => _plugin.Manifest.Capabilities.Count;
    public int PermissionCount => _plugin.Manifest.Permissions.Count;

    public IReadOnlyList<PluginCapability> Capabilities => _plugin.Manifest.Capabilities;
    public IReadOnlyList<PluginPermission> Permissions => _plugin.Manifest.Permissions;

    public PluginItemViewModel(LoadedPlugin plugin, PluginManagerViewModel manager)
    {
        _plugin = plugin;
        _manager = manager;
    }

    /// <summary>
    /// Toggles the plugin enabled/disabled state.
    /// </summary>
    [RelayCommand]
    private void ToggleEnabled()
    {
        if (IsEnabled)
        {
            _manager.DisablePlugin(PluginId);
        }
        else
        {
            _manager.EnablePlugin(PluginId);
        }

        // Notify all state-related properties
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(StateIcon));
        OnPropertyChanged(nameof(StateColor));
    }

    /// <summary>
    /// Enables the plugin.
    /// </summary>
    [RelayCommand]
    private void Enable()
    {
        _manager.EnablePlugin(PluginId);
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(StateIcon));
        OnPropertyChanged(nameof(StateColor));
    }

    /// <summary>
    /// Disables the plugin.
    /// </summary>
    [RelayCommand]
    private void Disable()
    {
        _manager.DisablePlugin(PluginId);
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(StateIcon));
        OnPropertyChanged(nameof(StateColor));
    }

    /// <summary>
    /// Unloads and removes the plugin.
    /// </summary>
    [RelayCommand]
    private async Task UnloadAsync()
    {
        await _manager.UnloadPluginAsync(PluginId);
    }
}

/// <summary>
/// Display model for a plugin audit log entry.
/// </summary>
public sealed class PluginActivityEntry
{
    public DateTimeOffset Timestamp { get; }
    public string PluginId { get; }
    public string EventType { get; }
    public string ActionId { get; }
    public string Details { get; }
    public bool IsSuccess { get; }
    public string StatusIcon { get; }
    public string? Resource { get; }
    public string? ResourceType { get; }

    /// <summary>
    /// Formatted display text for the entry.
    /// </summary>
    public string DisplayText { get; }

    public PluginActivityEntry(PluginAuditEntry entry)
    {
        Timestamp = entry.Timestamp;
        PluginId = entry.PluginId;
        EventType = entry.EventType.ToString();
        ActionId = entry.ActionId ?? "";
        Details = entry.Details ?? "";
        IsSuccess = entry.Success ?? true;
        StatusIcon = IsSuccess ? "CheckCircle" : "XCircle";
        Resource = entry.Resource;
        ResourceType = entry.ResourceType?.ToString();

        // Build display text based on event type
        DisplayText = entry.EventType switch
        {
            PluginAuditEventType.Loaded => $"Plugin loaded: {entry.Details}",
            PluginAuditEventType.Unloaded => "Plugin unloaded",
            PluginAuditEventType.Enabled => "Plugin enabled",
            PluginAuditEventType.Disabled => "Plugin disabled",
            PluginAuditEventType.Error => $"Error: {entry.Details}",
            PluginAuditEventType.ActionStarted => $"Started: {entry.ActionId}",
            PluginAuditEventType.ActionCompleted => $"Completed: {entry.ActionId} ({entry.Duration?.TotalMilliseconds:F0}ms)",
            PluginAuditEventType.ActionFailed => $"Failed: {entry.ActionId} - {entry.Details}",
            PluginAuditEventType.ResourceAccess => FormatResourceAccess(entry),
            PluginAuditEventType.PermissionCheck => FormatPermissionCheck(entry),
            _ => entry.Details ?? EventType
        };
    }

    private static string FormatResourceAccess(PluginAuditEntry entry)
    {
        var status = entry.Success == true ? "allowed" : "DENIED";
        return $"{entry.ResourceType}: {entry.Resource} [{status}]";
    }

    private static string FormatPermissionCheck(PluginAuditEntry entry)
    {
        var status = entry.Success == true ? "allowed" : "DENIED";
        return $"Permission check: {entry.PermissionChecked} {entry.AccessRequested} [{status}]";
    }
}

/// <summary>
/// ViewModel for viewing plugin details.
/// </summary>
public partial class PluginDetailViewModel : ViewModelBase
{
    private readonly PluginHost _host;
    private readonly IPluginAuditLog _auditLog;

    [ObservableProperty]
    private string _pluginId = string.Empty;

    [ObservableProperty]
    private PluginManifest? _manifest;

    [ObservableProperty]
    private PluginState _state;

    [ObservableProperty]
    private DateTimeOffset _loadedAt;

    /// <summary>
    /// Permissions requested by this plugin.
    /// </summary>
    public ObservableCollection<PermissionDisplayItem> Permissions { get; } = [];

    /// <summary>
    /// Capabilities (tools) provided by this plugin.
    /// </summary>
    public ObservableCollection<CapabilityDisplayItem> Capabilities { get; } = [];

    /// <summary>
    /// Activity history for this plugin.
    /// </summary>
    public ObservableCollection<PluginActivityEntry> ActivityHistory { get; } = [];

    public PluginDetailViewModel(
        PluginHost host,
        IPluginAuditLog auditLog,
        ILogger<PluginDetailViewModel> logger)
        : base(logger)
    {
        _host = host;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Loads the detail view for a specific plugin.
    /// </summary>
    public void LoadPlugin(string pluginId)
    {
        var plugin = _host.GetPlugin(pluginId);
        if (plugin == null)
        {
            SetError($"Plugin not found: {pluginId}");
            return;
        }

        PluginId = pluginId;
        Manifest = plugin.Manifest;
        State = plugin.State;
        LoadedAt = plugin.LoadedAt;

        // Load permissions
        Permissions.Clear();
        foreach (var permission in plugin.Manifest.Permissions)
        {
            Permissions.Add(new PermissionDisplayItem(permission));
        }

        // Load capabilities
        Capabilities.Clear();
        foreach (var capability in plugin.Manifest.Capabilities)
        {
            Capabilities.Add(new CapabilityDisplayItem(capability));
        }

        // Load activity history for this plugin
        LoadActivityHistory(pluginId);
    }

    private void LoadActivityHistory(string pluginId)
    {
        var entries = _auditLog.GetEntriesForPlugin(pluginId)
            .OrderByDescending(e => e.Timestamp)
            .Take(100)
            .Select(e => new PluginActivityEntry(e))
            .ToList();

        ActivityHistory.Clear();
        foreach (var entry in entries)
        {
            ActivityHistory.Add(entry);
        }
    }

    /// <summary>
    /// Enables the plugin.
    /// </summary>
    [RelayCommand]
    private void Enable()
    {
        if (_host.EnablePlugin(PluginId))
        {
            State = PluginState.Enabled;
            Logger.LogInformation("Plugin enabled from detail view: {PluginId}", PluginId);
        }
    }

    /// <summary>
    /// Disables the plugin.
    /// </summary>
    [RelayCommand]
    private void Disable()
    {
        if (_host.DisablePlugin(PluginId))
        {
            State = PluginState.Disabled;
            Logger.LogInformation("Plugin disabled from detail view: {PluginId}", PluginId);
        }
    }
}

/// <summary>
/// Display item for a permission.
/// </summary>
public sealed class PermissionDisplayItem
{
    public PermissionType Type { get; }
    public PermissionAccess Access { get; }
    public string? Scope { get; }
    public string DisplayText { get; }
    public string Icon { get; }

    public PermissionDisplayItem(PluginPermission permission)
    {
        Type = permission.Type;
        Access = permission.Access;
        Scope = permission.Scope;

        DisplayText = $"{Type} ({Access})" + (Scope != null ? $": {Scope}" : "");

        Icon = permission.Type switch
        {
            PermissionType.File => "Folder",
            PermissionType.Memory => "Database",
            PermissionType.Network => "Globe",
            PermissionType.UI => "Window",
            PermissionType.Conversation => "Chat",
            PermissionType.Settings => "Settings",
            _ => "Key"
        };
    }
}

/// <summary>
/// Display item for a capability.
/// </summary>
public sealed class CapabilityDisplayItem
{
    public string ToolId { get; }
    public string Name { get; }
    public string Description { get; }
    public bool RequiresNetwork { get; }
    public bool ModifiesState { get; }
    public string Icon { get; }

    public CapabilityDisplayItem(PluginCapability capability)
    {
        ToolId = capability.ToolId;
        Name = capability.Name;
        Description = capability.Description;
        RequiresNetwork = capability.RequiresNetwork;
        ModifiesState = capability.ModifiesState;

        Icon = (RequiresNetwork, ModifiesState) switch
        {
            (true, true) => "GlobeEdit",
            (true, false) => "GlobeRead",
            (false, true) => "PencilSquare",
            (false, false) => "Eye"
        };
    }
}
