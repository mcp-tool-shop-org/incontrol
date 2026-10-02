using InControl.Core.Connectivity;

namespace InControl.Core.Policy;

/// <summary>
/// Wraps a ConnectivityManager with policy enforcement.
/// Network access is governed by allowed modes, domains, and telemetry settings.
/// </summary>
public sealed class PolicyGovernedConnectivityManager
{
    private readonly ConnectivityManager _innerManager;
    private readonly PolicyEngine _policyEngine;

    /// <summary>
    /// Event raised when mode change is blocked by policy.
    /// </summary>
    public event EventHandler<ModeChangeBlockedEventArgs>? ModeChangeBlocked;

    /// <summary>
    /// Event raised when a domain is blocked by policy.
    /// </summary>
    public event EventHandler<DomainBlockedEventArgs>? DomainBlocked;

    public PolicyGovernedConnectivityManager(
        ConnectivityManager innerManager,
        PolicyEngine policyEngine)
    {
        _innerManager = innerManager ?? throw new ArgumentNullException(nameof(innerManager));
        _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
    }

    /// <summary>
    /// Gets the underlying connectivity manager.
    /// </summary>
    public ConnectivityManager InnerManager => _innerManager;

    /// <summary>
    /// Gets the policy engine.
    /// </summary>
    public PolicyEngine PolicyEngine => _policyEngine;

    /// <summary>
    /// Gets the current mode.
    /// </summary>
    public ConnectivityMode Mode => _innerManager.Mode;

    /// <summary>
    /// Gets the current status.
    /// </summary>
    public ConnectivityStatus Status => _innerManager.Status;

    /// <summary>
    /// Whether the app is currently online.
    /// </summary>
    public bool IsOnline => _innerManager.IsOnline;

    /// <summary>
    /// Gets the current connectivity policy evaluation.
    /// </summary>
    public ConnectivityPolicyEvaluation GetCurrentPolicy()
    {
        return _policyEngine.EvaluateConnectivityPolicy();
    }

    /// <summary>
    /// Checks if connectivity settings can be changed.
    /// </summary>
    public ConnectivityPolicyStatus CheckConnectivityPolicy()
    {
        var policy = GetCurrentPolicy();
        var currentMode = MapModeToString(_innerManager.Mode);

        var canChangeMode = policy.AllowModeChange;
        var allowedModes = policy.AllowedModes?.Select(ParseMode).Where(m => m.HasValue).Select(m => m!.Value).ToList();

        return new ConnectivityPolicyStatus(
            CanChangeMode: canChangeMode,
            CurrentMode: _innerManager.Mode,
            DefaultMode: ParseMode(policy.DefaultMode) ?? ConnectivityMode.Connected,
            AllowedModes: allowedModes,
            BlockedDomains: policy.BlockedDomains.ToList(),
            AllowedDomains: policy.AllowedDomains?.ToList(),
            TelemetryAllowed: policy.AllowTelemetry,
            Reason: !canChangeMode ? "Mode changes are disabled by policy" : null);
    }

    /// <summary>
    /// Checks if a specific mode is allowed.
    /// </summary>
    public bool IsModeAllowed(ConnectivityMode mode)
    {
        var policy = GetCurrentPolicy();

        if (policy.AllowedModes == null || policy.AllowedModes.Count == 0)
            return true; // No restrictions

        var modeString = MapModeToString(mode);
        return policy.AllowedModes.Contains(modeString, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to change the connectivity mode with policy enforcement.
    /// </summary>
    public ModeChangeResult SetMode(ConnectivityMode mode)
    {
        var policy = GetCurrentPolicy();

        // Check if mode changes are allowed
        if (!policy.AllowModeChange)
        {
            ModeChangeBlocked?.Invoke(this, new ModeChangeBlockedEventArgs(
                _innerManager.Mode,
                mode,
                "Mode changes are disabled by policy"));

            return ModeChangeResult.Blocked("Mode changes are disabled by policy");
        }

        // Check if the specific mode is allowed
        if (!IsModeAllowed(mode))
        {
            var allowedList = string.Join(", ", policy.AllowedModes ?? []);
            ModeChangeBlocked?.Invoke(this, new ModeChangeBlockedEventArgs(
                _innerManager.Mode,
                mode,
                $"Mode '{mode}' is not allowed. Allowed modes: {allowedList}"));

            return ModeChangeResult.Blocked($"Mode '{mode}' is not allowed by policy");
        }

        _innerManager.SetMode(mode);
        return ModeChangeResult.Success(mode);
    }

    /// <summary>
    /// Goes offline (always allowed as it's a safety action).
    /// </summary>
    public void GoOfflineNow()
    {
        _innerManager.GoOfflineNow();
    }

    /// <summary>
    /// Checks if a domain is allowed by policy.
    /// </summary>
    public DomainCheckResult CheckDomain(string domain)
    {
        var policyResult = _policyEngine.EvaluateDomain(domain);

        if (policyResult.Decision == PolicyDecision.Deny)
        {
            return new DomainCheckResult(
                IsAllowed: false,
                Domain: domain,
                Reason: policyResult.Reason,
                Source: policyResult.Source);
        }

        return new DomainCheckResult(
            IsAllowed: true,
            Domain: domain,
            Reason: policyResult.Reason,
            Source: policyResult.Source);
    }

    /// <summary>
    /// Makes a network request with policy enforcement.
    /// </summary>
    public async Task<PolicyGovernedNetworkResponse> RequestAsync(NetworkRequest request, CancellationToken ct = default)
    {
        // Extract domain from endpoint
        var domain = ExtractDomain(request.Endpoint);

        // Check domain against policy
        var domainCheck = CheckDomain(domain);
        if (!domainCheck.IsAllowed)
        {
            DomainBlocked?.Invoke(this, new DomainBlockedEventArgs(
                domain,
                request.Endpoint,
                domainCheck.Reason ?? "Domain blocked by policy",
                domainCheck.Source));

            return PolicyGovernedNetworkResponse.Blocked(
                request.Endpoint,
                domainCheck.Reason ?? "Domain blocked by policy",
                domainCheck.Source);
        }

        // Check telemetry if this is a telemetry request
        if (IsTelemetryRequest(request))
        {
            var policy = GetCurrentPolicy();
            if (!policy.AllowTelemetry)
            {
                return PolicyGovernedNetworkResponse.Blocked(
                    request.Endpoint,
                    "Telemetry is disabled by policy",
                    PolicySource.Organization);
            }
        }

        // Forward to inner manager
        var response = await _innerManager.RequestAsync(request, ct).ConfigureAwait(false);

        if (response == null)
        {
            return PolicyGovernedNetworkResponse.Failed(
                request.Endpoint,
                "Request blocked by connectivity manager");
        }

        return PolicyGovernedNetworkResponse.Completed(response);
    }

    /// <summary>
    /// Checks if a request would be allowed (without making it).
    /// </summary>
    public PolicyGovernedRequestCheck CheckRequestAllowed(NetworkRequest request)
    {
        // Check domain policy
        var domain = ExtractDomain(request.Endpoint);
        var domainCheck = CheckDomain(domain);

        if (!domainCheck.IsAllowed)
        {
            return new PolicyGovernedRequestCheck(
                Allowed: false,
                Reason: domainCheck.Reason,
                PolicySource: domainCheck.Source);
        }

        // Check telemetry
        if (IsTelemetryRequest(request))
        {
            var policy = GetCurrentPolicy();
            if (!policy.AllowTelemetry)
            {
                return new PolicyGovernedRequestCheck(
                    Allowed: false,
                    Reason: "Telemetry is disabled by policy",
                    PolicySource: PolicySource.Organization);
            }
        }

        // Check inner manager's rules
        var innerCheck = _innerManager.CheckRequestAllowed(request);
        if (!innerCheck.Allowed)
        {
            return new PolicyGovernedRequestCheck(
                Allowed: false,
                Reason: innerCheck.Reason,
                PolicySource: null);
        }

        return new PolicyGovernedRequestCheck(
            Allowed: true,
            Reason: null,
            PolicySource: null);
    }

    /// <summary>
    /// Gets network request history.
    /// </summary>
    public IReadOnlyList<NetworkAuditEntry> GetRequestHistory() => _innerManager.GetRequestHistory();

    /// <summary>
    /// Gets recent network activity.
    /// </summary>
    public IReadOnlyList<NetworkAuditEntry> GetRecentActivity(int count = 10) => _innerManager.GetRecentActivity(count);

    /// <summary>
    /// Clears network request history.
    /// </summary>
    public void ClearHistory() => _innerManager.ClearHistory();

    /// <summary>
    /// Gets connectivity statistics with policy context.
    /// </summary>
    public ConnectivityPolicyStatistics GetStatistics()
    {
        var policy = GetCurrentPolicy();
        var history = _innerManager.GetRequestHistory();

        var blocked = history.Count(e => e.Status == NetworkRequestStatus.Blocked);
        var failed = history.Count(e => e.Status == NetworkRequestStatus.Failed);
        var completed = history.Count(e => e.Status == NetworkRequestStatus.Completed);

        return new ConnectivityPolicyStatistics(
            CurrentMode: _innerManager.Mode,
            CurrentStatus: _innerManager.Status,
            DefaultMode: ParseMode(policy.DefaultMode) ?? ConnectivityMode.Connected,
            AllowedModes: policy.AllowedModes?.ToList(),
            CanChangeMode: policy.AllowModeChange,
            BlockedDomainCount: policy.BlockedDomains.Count,
            AllowedDomainCount: policy.AllowedDomains?.Count,
            TelemetryAllowed: policy.AllowTelemetry,
            TotalRequests: history.Count,
            CompletedRequests: completed,
            BlockedRequests: blocked,
            FailedRequests: failed,
            SuccessRate: history.Count > 0 ? (completed * 100.0 / history.Count) : 100.0);
    }

    #region Helpers

    private static string MapModeToString(ConnectivityMode mode) => mode switch
    {
        ConnectivityMode.OfflineOnly => "offline",
        ConnectivityMode.Assisted => "local",
        ConnectivityMode.Connected => "online",
        _ => "online"
    };

    private static ConnectivityMode? ParseMode(string? mode) => mode?.ToLowerInvariant() switch
    {
        "offline" => ConnectivityMode.OfflineOnly,
        "local" or "hybrid" => ConnectivityMode.Assisted,
        "online" => ConnectivityMode.Connected,
        _ => null
    };

    private static string ExtractDomain(string endpoint)
    {
        try
        {
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        catch { }

        // Fallback: treat as domain
        return endpoint.Split('/')[0].Split(':')[0];
    }

    private static bool IsTelemetryRequest(NetworkRequest request)
    {
        // Heuristic: check intent and common telemetry patterns
        var lowerIntent = request.Intent?.ToLowerInvariant() ?? "";
        var lowerEndpoint = request.Endpoint.ToLowerInvariant();

        return lowerIntent.Contains("telemetry") ||
               lowerIntent.Contains("analytics") ||
               lowerIntent.Contains("tracking") ||
               lowerEndpoint.Contains("telemetry") ||
               lowerEndpoint.Contains("analytics") ||
               lowerEndpoint.Contains("metrics");
    }

    #endregion
}

#region Result Types

/// <summary>
/// Status of connectivity policy.
/// </summary>
public sealed record ConnectivityPolicyStatus(
    bool CanChangeMode,
    ConnectivityMode CurrentMode,
    ConnectivityMode DefaultMode,
    IReadOnlyList<ConnectivityMode>? AllowedModes,
    IReadOnlyList<string> BlockedDomains,
    IReadOnlyList<string>? AllowedDomains,
    bool TelemetryAllowed,
    string? Reason);

/// <summary>
/// Result of mode change attempt.
/// </summary>
public sealed record ModeChangeResult
{
    public bool IsSuccess { get; init; }
    public bool WasBlocked { get; init; }
    public string? BlockReason { get; init; }
    public ConnectivityMode? NewMode { get; init; }

    public static ModeChangeResult Success(ConnectivityMode mode) => new()
    {
        IsSuccess = true,
        NewMode = mode
    };

    public static ModeChangeResult Blocked(string reason) => new()
    {
        IsSuccess = false,
        WasBlocked = true,
        BlockReason = reason
    };
}

/// <summary>
/// Result of domain check.
/// </summary>
public sealed record DomainCheckResult(
    bool IsAllowed,
    string Domain,
    string? Reason,
    PolicySource Source);

/// <summary>
/// Result of policy-governed network request.
/// </summary>
public sealed record PolicyGovernedNetworkResponse
{
    public bool IsSuccess { get; init; }
    public bool WasBlocked { get; init; }
    public string? Endpoint { get; init; }
    public string? BlockReason { get; init; }
    public PolicySource? BlockSource { get; init; }
    public NetworkResponse? Response { get; init; }
    public string? Error { get; init; }

    public static PolicyGovernedNetworkResponse Completed(NetworkResponse response) => new()
    {
        IsSuccess = response.IsSuccess,
        Response = response,
        Error = response.Error
    };

    public static PolicyGovernedNetworkResponse Blocked(string endpoint, string reason, PolicySource source) => new()
    {
        IsSuccess = false,
        WasBlocked = true,
        Endpoint = endpoint,
        BlockReason = reason,
        BlockSource = source
    };

    public static PolicyGovernedNetworkResponse Failed(string endpoint, string error) => new()
    {
        IsSuccess = false,
        Endpoint = endpoint,
        Error = error
    };
}

/// <summary>
/// Check result for a request.
/// </summary>
public sealed record PolicyGovernedRequestCheck(
    bool Allowed,
    string? Reason,
    PolicySource? PolicySource);

/// <summary>
/// Connectivity statistics with policy context.
/// </summary>
public sealed record ConnectivityPolicyStatistics(
    ConnectivityMode CurrentMode,
    ConnectivityStatus CurrentStatus,
    ConnectivityMode DefaultMode,
    IReadOnlyList<string>? AllowedModes,
    bool CanChangeMode,
    int BlockedDomainCount,
    int? AllowedDomainCount,
    bool TelemetryAllowed,
    int TotalRequests,
    int CompletedRequests,
    int BlockedRequests,
    int FailedRequests,
    double SuccessRate);

#endregion

#region Event Args

/// <summary>
/// Event args for blocked mode change.
/// </summary>
public sealed class ModeChangeBlockedEventArgs : EventArgs
{
    public ConnectivityMode CurrentMode { get; }
    public ConnectivityMode RequestedMode { get; }
    public string Reason { get; }

    public ModeChangeBlockedEventArgs(ConnectivityMode currentMode, ConnectivityMode requestedMode, string reason)
    {
        CurrentMode = currentMode;
        RequestedMode = requestedMode;
        Reason = reason;
    }
}

/// <summary>
/// Event args for blocked domain.
/// </summary>
public sealed class DomainBlockedEventArgs : EventArgs
{
    public string Domain { get; }
    public string Endpoint { get; }
    public string Reason { get; }
    public PolicySource Source { get; }

    public DomainBlockedEventArgs(string domain, string endpoint, string reason, PolicySource source)
    {
        Domain = domain;
        Endpoint = endpoint;
        Reason = reason;
        Source = source;
    }
}

#endregion

/// <summary>
/// Extensions for easy policy integration with connectivity.
/// </summary>
public static class ConnectivityPolicyExtensions
{
    /// <summary>
    /// Creates a policy-governed wrapper around this connectivity manager.
    /// </summary>
    public static PolicyGovernedConnectivityManager WithPolicyEnforcement(
        this ConnectivityManager manager,
        PolicyEngine policyEngine)
    {
        return new PolicyGovernedConnectivityManager(manager, policyEngine);
    }
}
