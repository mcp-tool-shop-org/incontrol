using System.Text.Json;
using System.Text.Json.Serialization;

namespace InControl.Core.Connectivity;

/// <summary>
/// Manages internet connectivity with explicit operator control.
/// The app never "phones home" accidentally.
/// </summary>
public sealed class ConnectivityManager
{
    private readonly INetworkGateway _gateway;
    private readonly string _settingsPath;
    private readonly object _lock = new();

    private ConnectivitySettings _settings;
    private ConnectivityStatus _status = ConnectivityStatus.Offline;

    /// <summary>
    /// Event raised when connectivity mode changes.
    /// </summary>
    public event EventHandler<ConnectivityModeChangedEventArgs>? ModeChanged;

    /// <summary>
    /// Event raised when connectivity status changes.
    /// </summary>
    public event EventHandler<ConnectivityStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Event raised when a network request is made (for auditing).
    /// </summary>
    public event EventHandler<NetworkRequestEventArgs>? RequestMade;

    /// <summary>
    /// Event raised when a network request is blocked.
    /// </summary>
    public event EventHandler<NetworkRequestBlockedEventArgs>? RequestBlocked;

    public ConnectivityManager(INetworkGateway gateway, string settingsPath)
    {
        _gateway = gateway;
        _settingsPath = settingsPath;
        _settings = LoadSettings() ?? ConnectivitySettings.Default;
    }

    /// <summary>
    /// Current connectivity mode.
    /// </summary>
    public ConnectivityMode Mode => _settings.Mode;

    /// <summary>
    /// Current connectivity status.
    /// </summary>
    public ConnectivityStatus Status
    {
        get
        {
            lock (_lock)
            {
                return _status;
            }
        }
        private set
        {
            lock (_lock)
            {
                if (_status != value)
                {
                    var oldStatus = _status;
                    _status = value;
                    StatusChanged?.Invoke(this, new ConnectivityStatusChangedEventArgs(oldStatus, value));
                }
            }
        }
    }

    /// <summary>
    /// Whether the app is currently allowed to make network requests.
    /// </summary>
    public bool IsOnline => Mode != ConnectivityMode.OfflineOnly && Status != ConnectivityStatus.Offline;

    /// <summary>
    /// Changes the connectivity mode.
    /// </summary>
    public void SetMode(ConnectivityMode mode)
    {
        lock (_lock)
        {
            if (_settings.Mode != mode)
            {
                var oldMode = _settings.Mode;
                _settings = _settings with { Mode = mode };
                SaveSettings();

                // Update status based on new mode
                if (mode == ConnectivityMode.OfflineOnly)
                {
                    Status = ConnectivityStatus.Offline;
                }
                else if (oldMode == ConnectivityMode.OfflineOnly)
                {
                    // Coming online - set to Idle
                    Status = ConnectivityStatus.Idle;
                }

                ModeChanged?.Invoke(this, new ConnectivityModeChangedEventArgs(oldMode, mode));
            }
        }
    }

    /// <summary>
    /// Immediately goes offline, regardless of current mode.
    /// </summary>
    public void GoOfflineNow()
    {
        SetMode(ConnectivityMode.OfflineOnly);
    }

    /// <summary>
    /// Makes a network request through the controlled gateway.
    /// Returns null if the request is blocked by policy.
    /// </summary>
    public async Task<NetworkResponse?> RequestAsync(NetworkRequest request, CancellationToken ct = default)
    {
        // Check if request is allowed
        var checkResult = CheckRequestAllowed(request);
        if (!checkResult.Allowed)
        {
            RequestBlocked?.Invoke(this, new NetworkRequestBlockedEventArgs(request, checkResult.Reason ?? "Request not allowed"));
            return null;
        }

        // Update status
        Status = ConnectivityStatus.Active;

        try
        {
            // Log the request for auditing
            var auditEntry = new NetworkAuditEntry(
                Id: Guid.NewGuid(),
                Timestamp: DateTimeOffset.UtcNow,
                Request: request,
                Status: NetworkRequestStatus.InProgress,
                Response: null,
                Error: null
            );

            RequestMade?.Invoke(this, new NetworkRequestEventArgs(auditEntry));

            // Make the actual request
            var response = await _gateway.SendAsync(request, ct).ConfigureAwait(false);

            // Update audit entry
            auditEntry = auditEntry with
            {
                Status = response.IsSuccess ? NetworkRequestStatus.Completed : NetworkRequestStatus.Failed,
                Response = response,
                Error = response.IsSuccess ? null : response.Error
            };

            // Record in history
            RecordRequest(auditEntry);

            return response;
        }
        finally
        {
            // Update status
            if (Mode != ConnectivityMode.OfflineOnly)
            {
                Status = ConnectivityStatus.Idle;
            }
        }
    }

    /// <summary>
    /// Checks if a request would be allowed under current policy.
    /// </summary>
    public RequestCheckResult CheckRequestAllowed(NetworkRequest request)
    {
        // Hard block in offline mode
        if (Mode == ConnectivityMode.OfflineOnly)
        {
            return new RequestCheckResult(false, "Offline mode is enabled");
        }

        // Check endpoint allowlist in Assisted mode
        if (Mode == ConnectivityMode.Assisted)
        {
            if (!IsEndpointAllowed(request.Endpoint))
            {
                return new RequestCheckResult(false, $"Endpoint not in allowlist: {request.Endpoint}");
            }
        }

        // Check intent is declared
        if (string.IsNullOrWhiteSpace(request.Intent))
        {
            return new RequestCheckResult(false, "Network intent must be declared");
        }

        return new RequestCheckResult(true, null);
    }

    /// <summary>
    /// Gets the network request history for auditing.
    /// </summary>
    public IReadOnlyList<NetworkAuditEntry> GetRequestHistory()
    {
        lock (_lock)
        {
            return _settings.RequestHistory.ToList();
        }
    }

    /// <summary>
    /// Gets recent network activity (last N requests).
    /// </summary>
    public IReadOnlyList<NetworkAuditEntry> GetRecentActivity(int count = 10)
    {
        lock (_lock)
        {
            return _settings.RequestHistory.TakeLast(count).ToList();
        }
    }

    /// <summary>
    /// Clears the network request history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _settings = _settings with { RequestHistory = [] };
            SaveSettings();
        }
    }

    /// <summary>
    /// Adds an endpoint to the allowlist (for Assisted mode).
    /// </summary>
    public void AllowEndpoint(string endpoint)
    {
        lock (_lock)
        {
            if (!_settings.AllowedEndpoints.Contains(endpoint))
            {
                var endpoints = _settings.AllowedEndpoints.ToList();
                endpoints.Add(endpoint);
                _settings = _settings with { AllowedEndpoints = endpoints };
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Removes an endpoint from the allowlist.
    /// </summary>
    public void DenyEndpoint(string endpoint)
    {
        lock (_lock)
        {
            var endpoints = _settings.AllowedEndpoints.ToList();
            if (endpoints.Remove(endpoint))
            {
                _settings = _settings with { AllowedEndpoints = endpoints };
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets the list of allowed endpoints.
    /// </summary>
    public IReadOnlyList<string> GetAllowedEndpoints()
    {
        lock (_lock)
        {
            return _settings.AllowedEndpoints.ToList();
        }
    }

    private bool IsEndpointAllowed(string endpoint)
    {
        lock (_lock)
        {
            return _settings.AllowedEndpoints.Any(allowed =>
                endpoint.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void RecordRequest(NetworkAuditEntry entry)
    {
        lock (_lock)
        {
            var history = _settings.RequestHistory.ToList();
            history.Add(entry);

            // Trim to max history size
            while (history.Count > 1000)
            {
                history.RemoveAt(0);
            }

            _settings = _settings with { RequestHistory = history };
            SaveSettings();
        }
    }

    private ConnectivitySettings? LoadSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<ConnectivitySettings>(json, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });
        }
        catch
        {
            return null;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silently fail
        }
    }
}

/// <summary>
/// Connectivity modes controlling network access.
/// </summary>
public enum ConnectivityMode
{
    /// <summary>
    /// No network access. Completely offline.
    /// This is the default mode.
    /// </summary>
    OfflineOnly,

    /// <summary>
    /// Limited network access to approved endpoints only.
    /// Each endpoint must be explicitly allowed.
    /// </summary>
    Assisted,

    /// <summary>
    /// Full network access with logging.
    /// Operator explicitly enabled this mode.
    /// </summary>
    Connected
}

/// <summary>
/// Current network connectivity status.
/// </summary>
public enum ConnectivityStatus
{
    /// <summary>No network activity.</summary>
    Offline,

    /// <summary>Connected but idle.</summary>
    Idle,

    /// <summary>Network request in progress.</summary>
    Active
}

/// <summary>
/// Persistent connectivity settings.
/// </summary>
public sealed record ConnectivitySettings(
    ConnectivityMode Mode,
    IReadOnlyList<string> AllowedEndpoints,
    IReadOnlyList<NetworkAuditEntry> RequestHistory
)
{
    public static ConnectivitySettings Default => new(
        Mode: ConnectivityMode.OfflineOnly,
        AllowedEndpoints: [],
        RequestHistory: []
    );
}

/// <summary>
/// A network request to be made through the gateway.
/// </summary>
public sealed record NetworkRequest(
    string Endpoint,
    string Method,
    string Intent,
    string? DataSent,
    DateTimeOffset RequestedAt
);

/// <summary>
/// Response from a network request.
/// </summary>
public sealed record NetworkResponse(
    bool IsSuccess,
    int StatusCode,
    string? Data,
    string? Error,
    TimeSpan Duration
);

/// <summary>
/// Entry in the network request audit log.
/// </summary>
public sealed record NetworkAuditEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    NetworkRequest Request,
    NetworkRequestStatus Status,
    NetworkResponse? Response,
    string? Error
);

/// <summary>
/// Status of a network request in the audit log.
/// </summary>
public enum NetworkRequestStatus
{
    InProgress,
    Completed,
    Failed,
    Blocked
}

/// <summary>
/// Result of checking if a request is allowed.
/// </summary>
public sealed record RequestCheckResult(
    bool Allowed,
    string? Reason
);

/// <summary>
/// Interface for the network gateway.
/// All network calls go through this single point.
/// </summary>
public interface INetworkGateway
{
    Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default);
}

/// <summary>
/// Event args for mode changes.
/// </summary>
public sealed class ConnectivityModeChangedEventArgs : EventArgs
{
    public ConnectivityMode OldMode { get; }
    public ConnectivityMode NewMode { get; }

    public ConnectivityModeChangedEventArgs(ConnectivityMode oldMode, ConnectivityMode newMode)
    {
        OldMode = oldMode;
        NewMode = newMode;
    }
}

/// <summary>
/// Event args for status changes.
/// </summary>
public sealed class ConnectivityStatusChangedEventArgs : EventArgs
{
    public ConnectivityStatus OldStatus { get; }
    public ConnectivityStatus NewStatus { get; }

    public ConnectivityStatusChangedEventArgs(ConnectivityStatus oldStatus, ConnectivityStatus newStatus)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}

/// <summary>
/// Event args for network requests.
/// </summary>
public sealed class NetworkRequestEventArgs : EventArgs
{
    public NetworkAuditEntry Entry { get; }

    public NetworkRequestEventArgs(NetworkAuditEntry entry)
    {
        Entry = entry;
    }
}

/// <summary>
/// Event args for blocked requests.
/// </summary>
public sealed class NetworkRequestBlockedEventArgs : EventArgs
{
    public NetworkRequest Request { get; }
    public string Reason { get; }

    public NetworkRequestBlockedEventArgs(NetworkRequest request, string reason)
    {
        Request = request;
        Reason = reason;
    }
}
