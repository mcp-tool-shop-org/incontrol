using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using InControl.Core.Connectivity;

namespace InControl.ViewModels.Connectivity;

/// <summary>
/// ViewModel for connectivity settings and status display.
/// Provides operator visibility into network activity.
/// </summary>
public partial class ConnectivityViewModel : ViewModelBase
{
    private readonly ConnectivityManager _connectivity;

    /// <summary>
    /// Current connectivity mode.
    /// </summary>
    [ObservableProperty]
    private ConnectivityMode _mode = ConnectivityMode.OfflineOnly;

    /// <summary>
    /// Current connectivity status.
    /// </summary>
    [ObservableProperty]
    private ConnectivityStatus _status = ConnectivityStatus.Offline;

    /// <summary>
    /// Whether the app is currently online (can make requests).
    /// </summary>
    [ObservableProperty]
    private bool _isOnline;

    /// <summary>
    /// Human-readable status description.
    /// </summary>
    [ObservableProperty]
    private string _statusDescription = "Offline — no network activity";

    /// <summary>
    /// Status indicator color for UI.
    /// </summary>
    [ObservableProperty]
    private string _statusColor = "Gray";

    /// <summary>
    /// Total requests made in current session.
    /// </summary>
    [ObservableProperty]
    private int _totalRequests;

    /// <summary>
    /// Total data sent in current session.
    /// </summary>
    [ObservableProperty]
    private long _totalBytesSent;

    /// <summary>
    /// Total data received in current session.
    /// </summary>
    [ObservableProperty]
    private long _totalBytesReceived;

    /// <summary>
    /// Recent network activity entries.
    /// </summary>
    public ObservableCollection<NetworkActivityEntry> RecentActivity { get; } = [];

    /// <summary>
    /// Whether there is any network activity to show.
    /// </summary>
    public bool HasActivity => RecentActivity.Count > 0;

    /// <summary>
    /// Available connectivity modes.
    /// </summary>
    public static IReadOnlyList<ConnectivityModeOption> AvailableModes { get; } =
    [
        new(ConnectivityMode.OfflineOnly, "Offline Only", "No network access. Fully local operation.", "Shield"),
        new(ConnectivityMode.Assisted, "Assisted", "Network available for approved operations only.", "ShieldCheck"),
        new(ConnectivityMode.Connected, "Connected", "Full network access with audit logging.", "Globe")
    ];

    public ConnectivityViewModel(
        ConnectivityManager connectivity,
        ILogger<ConnectivityViewModel> logger)
        : base(logger)
    {
        _connectivity = connectivity;

        // Subscribe to events
        _connectivity.ModeChanged += OnModeChanged;
        _connectivity.StatusChanged += OnStatusChanged;
        _connectivity.RequestMade += OnRequestMade;

        // Load initial state
        RefreshState();
    }

    /// <summary>
    /// Refreshes the ViewModel state from the connectivity manager.
    /// </summary>
    private void RefreshState()
    {
        Mode = _connectivity.Mode;
        Status = _connectivity.Status;
        IsOnline = _connectivity.IsOnline;
        UpdateStatusDisplay();
        LoadRecentActivity();
    }

    /// <summary>
    /// Updates the status description and color.
    /// </summary>
    private void UpdateStatusDisplay()
    {
        (StatusDescription, StatusColor) = (Mode, Status) switch
        {
            (ConnectivityMode.OfflineOnly, _) => ("Offline — no network activity", "Gray"),
            (_, ConnectivityStatus.Offline) => ("Disconnected", "Orange"),
            (_, ConnectivityStatus.Idle) => ("Online — idle", "Green"),
            (_, ConnectivityStatus.Active) => ("Online — active", "Blue"),
            _ => ("Unknown", "Gray")
        };
    }

    /// <summary>
    /// Loads recent network activity from the audit log.
    /// </summary>
    private void LoadRecentActivity()
    {
        var recent = _connectivity.GetRequestHistory()
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .Select(e => new NetworkActivityEntry(e))
            .ToList();

        RecentActivity.Clear();
        foreach (var entry in recent)
        {
            RecentActivity.Add(entry);
        }

        // Update totals
        TotalRequests = _connectivity.GetRequestHistory().Count;
        OnPropertyChanged(nameof(HasActivity));
    }

    /// <summary>
    /// Sets the connectivity mode.
    /// </summary>
    [RelayCommand]
    private void SetMode(ConnectivityMode newMode)
    {
        _connectivity.SetMode(newMode);
    }

    /// <summary>
    /// Goes offline immediately (panic button).
    /// </summary>
    [RelayCommand]
    private void GoOffline()
    {
        _connectivity.SetMode(ConnectivityMode.OfflineOnly);
        Logger.LogInformation("User triggered immediate offline mode");
    }

    /// <summary>
    /// Clears the network activity audit log.
    /// </summary>
    [RelayCommand]
    private void ClearAuditLog()
    {
        _connectivity.ClearHistory();
        RecentActivity.Clear();
        TotalRequests = 0;
        TotalBytesSent = 0;
        TotalBytesReceived = 0;
        OnPropertyChanged(nameof(HasActivity));
        Logger.LogInformation("User cleared network audit log");
    }

    /// <summary>
    /// Exports the audit log for external review.
    /// </summary>
    [RelayCommand]
    private async Task ExportAuditLogAsync()
    {
        await ExecuteAsync(async () =>
        {
            var log = _connectivity.GetRequestHistory();
            var json = System.Text.Json.JsonSerializer.Serialize(log, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            // For now, just log - actual file dialog would be in the view
            Logger.LogInformation("Audit log export prepared with {Count} entries", log.Count);
            await Task.CompletedTask;
        });
    }

    private void OnModeChanged(object? sender, ConnectivityModeChangedEventArgs e)
    {
        Mode = e.NewMode;
        UpdateStatusDisplay();
    }

    private void OnStatusChanged(object? sender, ConnectivityStatusChangedEventArgs e)
    {
        Status = e.NewStatus;
        IsOnline = _connectivity.IsOnline;
        UpdateStatusDisplay();
    }

    private void OnRequestMade(object? sender, NetworkRequestEventArgs e)
    {
        // Add to recent activity (on UI thread)
        var entry = new NetworkActivityEntry(e.Entry);
        RecentActivity.Insert(0, entry);

        // Keep only last 50
        while (RecentActivity.Count > 50)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }

        TotalRequests++;
        OnPropertyChanged(nameof(HasActivity));
    }
}

/// <summary>
/// Display model for a connectivity mode option.
/// </summary>
public sealed record ConnectivityModeOption(
    ConnectivityMode Mode,
    string Name,
    string Description,
    string Icon
);

/// <summary>
/// Display model for a network activity entry.
/// </summary>
public sealed class NetworkActivityEntry
{
    public DateTimeOffset Timestamp { get; }
    public string Endpoint { get; }
    public string Method { get; }
    public string Intent { get; }
    public bool IsSuccess { get; }
    public int StatusCode { get; }
    public string Duration { get; }
    public string StatusIcon { get; }

    public NetworkActivityEntry(NetworkAuditEntry entry)
    {
        Timestamp = entry.Timestamp;
        Endpoint = entry.Request.Endpoint;
        Method = entry.Request.Method;
        Intent = entry.Request.Intent;
        IsSuccess = entry.Response?.IsSuccess ?? false;
        StatusCode = entry.Response?.StatusCode ?? 0;
        Duration = entry.Response?.Duration.TotalMilliseconds.ToString("F0") + "ms" ?? "—";
        StatusIcon = IsSuccess ? "CheckCircle" : "XCircle";
    }
}
