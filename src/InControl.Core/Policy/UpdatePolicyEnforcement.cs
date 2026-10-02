using InControl.Core.Updates;
using SysVersion = System.Version;

namespace InControl.Core.Policy;

/// <summary>
/// Wraps an UpdateManager with policy enforcement.
/// Updates are governed by channel restrictions, deferral, and minimum version requirements.
/// </summary>
public sealed class PolicyGovernedUpdateManager
{
    private readonly UpdateManager _innerManager;
    private readonly PolicyEngine _policyEngine;
    private readonly string _currentChannel;

    /// <summary>
    /// Event raised when auto-update is blocked by policy.
    /// </summary>
    public event EventHandler<AutoUpdateBlockedEventArgs>? AutoUpdateBlocked;

    /// <summary>
    /// Event raised when a channel change is blocked.
    /// </summary>
    public event EventHandler<ChannelBlockedEventArgs>? ChannelBlocked;

    /// <summary>
    /// Event raised when an update is deferred.
    /// </summary>
    public event EventHandler<UpdateDeferredEventArgs>? UpdateDeferred;

    public PolicyGovernedUpdateManager(
        UpdateManager innerManager,
        PolicyEngine policyEngine,
        string currentChannel = "stable")
    {
        _innerManager = innerManager ?? throw new ArgumentNullException(nameof(innerManager));
        _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
        _currentChannel = currentChannel;
    }

    /// <summary>
    /// Gets the underlying update manager.
    /// </summary>
    public UpdateManager InnerManager => _innerManager;

    /// <summary>
    /// Gets the policy engine.
    /// </summary>
    public PolicyEngine PolicyEngine => _policyEngine;

    /// <summary>
    /// Gets the current release channel.
    /// </summary>
    public string CurrentChannel => _currentChannel;

    /// <summary>
    /// Gets the current version.
    /// </summary>
    public SysVersion CurrentVersion => _innerManager.CurrentVersion;

    /// <summary>
    /// Gets the current update state.
    /// </summary>
    public UpdateState State => _innerManager.State;

    /// <summary>
    /// Gets the current update mode.
    /// </summary>
    public UpdateMode Mode => _innerManager.Mode;

    /// <summary>
    /// Gets the current update policy evaluation.
    /// </summary>
    public UpdatePolicyEvaluation GetCurrentPolicy()
    {
        return _policyEngine.EvaluateUpdatePolicy();
    }

    /// <summary>
    /// Checks the current update policy status.
    /// </summary>
    public UpdatePolicyStatus CheckUpdatePolicy()
    {
        var policy = GetCurrentPolicy();

        var canAutoUpdate = policy.AutoUpdate;
        var isChannelAllowed = IsChannelAllowed(_currentChannel);
        var meetsMinimum = MeetsMinimumVersion();

        string? reason = null;
        if (!canAutoUpdate) reason = "Auto-update is disabled by policy";
        else if (!isChannelAllowed) reason = $"Channel '{_currentChannel}' is not allowed";
        else if (!meetsMinimum) reason = $"Current version below minimum required: {policy.MinimumVersion}";

        return new UpdatePolicyStatus(
            CanAutoUpdate: canAutoUpdate,
            CanCheckOnStartup: policy.CheckOnStartup,
            CurrentChannel: _currentChannel,
            RequiredChannel: policy.RequiredChannel,
            AllowedChannels: policy.AllowedChannels?.ToList(),
            IsChannelAllowed: isChannelAllowed,
            DeferDays: policy.DeferDays,
            MinimumVersion: policy.MinimumVersion,
            MeetsMinimumVersion: meetsMinimum,
            Reason: reason);
    }

    /// <summary>
    /// Checks if a channel is allowed by policy.
    /// </summary>
    public bool IsChannelAllowed(string channel)
    {
        var policy = GetCurrentPolicy();

        // If a required channel is set, only that channel is allowed
        if (!string.IsNullOrEmpty(policy.RequiredChannel))
        {
            return channel.Equals(policy.RequiredChannel, StringComparison.OrdinalIgnoreCase);
        }

        // If allowed channels are set, channel must be in the list
        if (policy.AllowedChannels != null && policy.AllowedChannels.Count > 0)
        {
            return policy.AllowedChannels.Contains(channel, StringComparer.OrdinalIgnoreCase);
        }

        // No restrictions
        return true;
    }

    /// <summary>
    /// Checks if the current version meets the minimum requirement.
    /// </summary>
    public bool MeetsMinimumVersion()
    {
        var policy = GetCurrentPolicy();

        if (string.IsNullOrEmpty(policy.MinimumVersion))
            return true;

        if (!SysVersion.TryParse(policy.MinimumVersion, out var minVersion))
            return true;

        return _innerManager.CurrentVersion >= minVersion;
    }

    /// <summary>
    /// Checks if an update should be deferred based on policy.
    /// </summary>
    public DeferralCheckResult CheckDeferral(UpdateInfo update)
    {
        var policy = GetCurrentPolicy();

        if (policy.DeferDays <= 0)
        {
            return new DeferralCheckResult(
                ShouldDefer: false,
                DeferUntil: null,
                DaysRemaining: 0);
        }

        var deferUntil = update.ReleasedAt.AddDays(policy.DeferDays);
        var now = DateTimeOffset.UtcNow;

        if (now < deferUntil)
        {
            var daysRemaining = (int)Math.Ceiling((deferUntil - now).TotalDays);
            return new DeferralCheckResult(
                ShouldDefer: true,
                DeferUntil: deferUntil,
                DaysRemaining: daysRemaining);
        }

        return new DeferralCheckResult(
            ShouldDefer: false,
            DeferUntil: null,
            DaysRemaining: 0);
    }

    /// <summary>
    /// Sets the update mode with policy enforcement.
    /// </summary>
    public UpdateModeChangeResult SetMode(UpdateMode mode)
    {
        var policy = GetCurrentPolicy();

        // Trying to enable auto-update when policy disables it
        if ((mode == UpdateMode.AutoDownload || mode == UpdateMode.AutoInstall) && !policy.AutoUpdate)
        {
            AutoUpdateBlocked?.Invoke(this, new AutoUpdateBlockedEventArgs(
                mode,
                "Auto-update is disabled by policy"));

            return new UpdateModeChangeResult(
                IsSuccess: false,
                WasBlocked: true,
                BlockReason: "Auto-update is disabled by policy");
        }

        _innerManager.SetMode(mode);
        return new UpdateModeChangeResult(
            IsSuccess: true,
            WasBlocked: false,
            BlockReason: null);
    }

    /// <summary>
    /// Checks for updates with policy enforcement.
    /// </summary>
    public async Task<PolicyGovernedCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var policy = GetCurrentPolicy();

        // Check if current channel is allowed
        if (!IsChannelAllowed(_currentChannel))
        {
            ChannelBlocked?.Invoke(this, new ChannelBlockedEventArgs(
                _currentChannel,
                policy.RequiredChannel ?? string.Join(", ", policy.AllowedChannels ?? [])));

            return PolicyGovernedCheckResult.ChannelBlocked(
                _currentChannel,
                policy.RequiredChannel);
        }

        // Perform the check
        var update = await _innerManager.CheckForUpdateAsync(ct);

        if (update == null)
        {
            return PolicyGovernedCheckResult.NoUpdate();
        }

        // Check if update should be deferred
        var deferral = CheckDeferral(update);
        if (deferral.ShouldDefer && !update.IsCritical)
        {
            UpdateDeferred?.Invoke(this, new UpdateDeferredEventArgs(
                update,
                deferral.DeferUntil!.Value,
                deferral.DaysRemaining));

            return PolicyGovernedCheckResult.Deferred(
                update,
                deferral.DeferUntil!.Value,
                deferral.DaysRemaining);
        }

        return PolicyGovernedCheckResult.Available(update);
    }

    /// <summary>
    /// Checks if startup update check is allowed.
    /// </summary>
    public bool CanCheckOnStartup()
    {
        return GetCurrentPolicy().CheckOnStartup;
    }

    /// <summary>
    /// Downloads an update with policy enforcement.
    /// </summary>
    public async Task<PolicyGovernedDownloadResult> DownloadUpdateAsync(CancellationToken ct = default)
    {
        var available = _innerManager.AvailableUpdate;
        if (available == null)
        {
            return PolicyGovernedDownloadResult.Failed("No update available");
        }

        // Check deferral
        var deferral = CheckDeferral(available);
        if (deferral.ShouldDefer && !available.IsCritical)
        {
            return PolicyGovernedDownloadResult.Deferred(
                deferral.DeferUntil!.Value,
                deferral.DaysRemaining);
        }

        var result = await _innerManager.DownloadUpdateAsync(ct);

        if (result.Success)
        {
            return PolicyGovernedDownloadResult.Success(result.DownloadPath!);
        }
        else
        {
            return PolicyGovernedDownloadResult.Failed(result.Error ?? "Download failed");
        }
    }

    /// <summary>
    /// Installs an update with policy enforcement.
    /// </summary>
    public async Task<PolicyGovernedInstallResult> InstallUpdateAsync(
        string downloadPath,
        bool requiresRestart,
        CancellationToken ct = default)
    {
        var result = await _innerManager.InstallUpdateAsync(downloadPath, requiresRestart, ct);

        if (result.Success)
        {
            return PolicyGovernedInstallResult.Success(result.RequiresRestart);
        }
        else
        {
            return PolicyGovernedInstallResult.Failed(result.Error ?? "Install failed");
        }
    }

    /// <summary>
    /// Gets comprehensive update statistics with policy context.
    /// </summary>
    public UpdatePolicyStatistics GetStatistics()
    {
        var policy = GetCurrentPolicy();

        return new UpdatePolicyStatistics(
            CurrentVersion: _innerManager.CurrentVersion,
            CurrentChannel: _currentChannel,
            RequiredChannel: policy.RequiredChannel,
            IsChannelAllowed: IsChannelAllowed(_currentChannel),
            AutoUpdateAllowed: policy.AutoUpdate,
            CheckOnStartupAllowed: policy.CheckOnStartup,
            DeferDays: policy.DeferDays,
            MinimumVersion: policy.MinimumVersion,
            MeetsMinimumVersion: MeetsMinimumVersion(),
            LastChecked: _innerManager.LastChecked,
            AvailableUpdate: _innerManager.AvailableUpdate,
            CurrentState: _innerManager.State);
    }

    /// <summary>
    /// Gets version compliance information.
    /// </summary>
    public VersionComplianceInfo GetComplianceInfo()
    {
        var policy = GetCurrentPolicy();
        var meetsMinimum = MeetsMinimumVersion();
        var isChannelAllowed = IsChannelAllowed(_currentChannel);

        var status = (meetsMinimum, isChannelAllowed) switch
        {
            (true, true) => ComplianceStatus.Compliant,
            (false, _) => ComplianceStatus.BelowMinimum,
            (_, false) => ComplianceStatus.WrongChannel
        };

        string? message = status switch
        {
            ComplianceStatus.BelowMinimum => $"Version {_innerManager.CurrentVersion} is below minimum required {policy.MinimumVersion}. Update required.",
            ComplianceStatus.WrongChannel => $"Current channel '{_currentChannel}' is not allowed. Required: {policy.RequiredChannel ?? "N/A"}",
            _ => null
        };

        return new VersionComplianceInfo(
            Status: status,
            Message: message,
            CurrentVersion: _innerManager.CurrentVersion,
            MinimumVersion: policy.MinimumVersion,
            CurrentChannel: _currentChannel,
            RequiredChannel: policy.RequiredChannel);
    }
}

#region Result Types

/// <summary>
/// Status of update policy.
/// </summary>
public sealed record UpdatePolicyStatus(
    bool CanAutoUpdate,
    bool CanCheckOnStartup,
    string CurrentChannel,
    string? RequiredChannel,
    IReadOnlyList<string>? AllowedChannels,
    bool IsChannelAllowed,
    int DeferDays,
    string? MinimumVersion,
    bool MeetsMinimumVersion,
    string? Reason);

/// <summary>
/// Result of update mode change attempt.
/// </summary>
public sealed record UpdateModeChangeResult(
    bool IsSuccess,
    bool WasBlocked,
    string? BlockReason);

/// <summary>
/// Result of checking for updates.
/// </summary>
public sealed record PolicyGovernedCheckResult
{
    public bool HasUpdate { get; init; }
    public bool WasDeferred { get; init; }
    public bool WasChannelBlocked { get; init; }
    public UpdateInfo? Update { get; init; }
    public DateTimeOffset? DeferredUntil { get; init; }
    public int DaysRemaining { get; init; }
    public string? BlockedChannel { get; init; }
    public string? RequiredChannel { get; init; }

    public static PolicyGovernedCheckResult NoUpdate() => new()
    {
        HasUpdate = false
    };

    public static PolicyGovernedCheckResult Available(UpdateInfo update) => new()
    {
        HasUpdate = true,
        Update = update
    };

    public static PolicyGovernedCheckResult Deferred(UpdateInfo update, DateTimeOffset deferredUntil, int daysRemaining) => new()
    {
        HasUpdate = true,
        WasDeferred = true,
        Update = update,
        DeferredUntil = deferredUntil,
        DaysRemaining = daysRemaining
    };

    public static PolicyGovernedCheckResult ChannelBlocked(string blockedChannel, string? requiredChannel) => new()
    {
        HasUpdate = false,
        WasChannelBlocked = true,
        BlockedChannel = blockedChannel,
        RequiredChannel = requiredChannel
    };
}

/// <summary>
/// Result of download with policy context.
/// </summary>
public sealed record PolicyGovernedDownloadResult
{
    public bool IsSuccess { get; init; }
    public bool WasDeferred { get; init; }
    public string? DownloadPath { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset? DeferredUntil { get; init; }
    public int DaysRemaining { get; init; }

    public static PolicyGovernedDownloadResult Success(string downloadPath) => new()
    {
        IsSuccess = true,
        DownloadPath = downloadPath
    };

    public static PolicyGovernedDownloadResult Failed(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };

    public static PolicyGovernedDownloadResult Deferred(DateTimeOffset deferredUntil, int daysRemaining) => new()
    {
        IsSuccess = false,
        WasDeferred = true,
        DeferredUntil = deferredUntil,
        DaysRemaining = daysRemaining
    };
}

/// <summary>
/// Result of install with policy context.
/// </summary>
public sealed record PolicyGovernedInstallResult
{
    public bool IsSuccess { get; init; }
    public bool RequiresRestart { get; init; }
    public string? Error { get; init; }

    public static PolicyGovernedInstallResult Success(bool requiresRestart) => new()
    {
        IsSuccess = true,
        RequiresRestart = requiresRestart
    };

    public static PolicyGovernedInstallResult Failed(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}

/// <summary>
/// Result of deferral check.
/// </summary>
public sealed record DeferralCheckResult(
    bool ShouldDefer,
    DateTimeOffset? DeferUntil,
    int DaysRemaining);

/// <summary>
/// Update statistics with policy context.
/// </summary>
public sealed record UpdatePolicyStatistics(
    SysVersion CurrentVersion,
    string CurrentChannel,
    string? RequiredChannel,
    bool IsChannelAllowed,
    bool AutoUpdateAllowed,
    bool CheckOnStartupAllowed,
    int DeferDays,
    string? MinimumVersion,
    bool MeetsMinimumVersion,
    DateTimeOffset? LastChecked,
    UpdateInfo? AvailableUpdate,
    UpdateState CurrentState);

/// <summary>
/// Version compliance status.
/// </summary>
public enum ComplianceStatus
{
    Compliant,
    BelowMinimum,
    WrongChannel
}

/// <summary>
/// Version compliance information.
/// </summary>
public sealed record VersionComplianceInfo(
    ComplianceStatus Status,
    string? Message,
    SysVersion CurrentVersion,
    string? MinimumVersion,
    string CurrentChannel,
    string? RequiredChannel);

#endregion

#region Event Args

/// <summary>
/// Event args for auto-update blocked.
/// </summary>
public sealed class AutoUpdateBlockedEventArgs : EventArgs
{
    public UpdateMode RequestedMode { get; }
    public string Reason { get; }

    public AutoUpdateBlockedEventArgs(UpdateMode requestedMode, string reason)
    {
        RequestedMode = requestedMode;
        Reason = reason;
    }
}

/// <summary>
/// Event args for channel blocked.
/// </summary>
public sealed class ChannelBlockedEventArgs : EventArgs
{
    public string CurrentChannel { get; }
    public string RequiredChannel { get; }

    public ChannelBlockedEventArgs(string currentChannel, string requiredChannel)
    {
        CurrentChannel = currentChannel;
        RequiredChannel = requiredChannel;
    }
}

/// <summary>
/// Event args for deferred update.
/// </summary>
public sealed class UpdateDeferredEventArgs : EventArgs
{
    public UpdateInfo Update { get; }
    public DateTimeOffset DeferredUntil { get; }
    public int DaysRemaining { get; }

    public UpdateDeferredEventArgs(UpdateInfo update, DateTimeOffset deferredUntil, int daysRemaining)
    {
        Update = update;
        DeferredUntil = deferredUntil;
        DaysRemaining = daysRemaining;
    }
}

#endregion

/// <summary>
/// Extensions for easy policy integration with update manager.
/// </summary>
public static class UpdatePolicyExtensions
{
    /// <summary>
    /// Creates a policy-governed wrapper around this update manager.
    /// </summary>
    public static PolicyGovernedUpdateManager WithPolicyEnforcement(
        this UpdateManager manager,
        PolicyEngine policyEngine,
        string currentChannel = "stable")
    {
        return new PolicyGovernedUpdateManager(manager, policyEngine, currentChannel);
    }
}
