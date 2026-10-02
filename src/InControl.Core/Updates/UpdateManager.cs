using System.Text.Json;
using System.Text.Json.Serialization;
using SysVersion = global::System.Version;

namespace InControl.Core.Updates;

/// <summary>
/// Manages application updates with full operator control.
/// Updates never happen without explicit operator consent.
/// </summary>
public sealed class UpdateManager
{
    private readonly IUpdateChecker _checker;
    private readonly IUpdateInstaller _installer;
    private UpdateSettings _settings;
    private readonly string _settingsPath;
    private readonly object _lock = new();

    private UpdateInfo? _availableUpdate;
    private UpdateState _state = UpdateState.Idle;

    /// <summary>
    /// Event raised when an update becomes available.
    /// </summary>
    public event EventHandler<UpdateAvailableEventArgs>? UpdateAvailable;

    /// <summary>
    /// Event raised when update state changes.
    /// </summary>
    public event EventHandler<UpdateStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when a check or install fails.
    /// </summary>
    public event EventHandler<UpdateErrorEventArgs>? Error;

    public UpdateManager(
        IUpdateChecker checker,
        IUpdateInstaller installer,
        string settingsPath)
    {
        _checker = checker;
        _installer = installer;
        _settingsPath = settingsPath;
        _settings = LoadSettings() ?? UpdateSettings.Default;
    }

    /// <summary>
    /// Current update mode.
    /// </summary>
    public UpdateMode Mode => _settings.Mode;

    /// <summary>
    /// Current update state.
    /// </summary>
    public UpdateState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_lock)
            {
                if (_state != value)
                {
                    var oldState = _state;
                    _state = value;
                    StateChanged?.Invoke(this, new UpdateStateChangedEventArgs(oldState, value));
                }
            }
        }
    }

    /// <summary>
    /// Currently available update, if any.
    /// </summary>
    public UpdateInfo? AvailableUpdate
    {
        get
        {
            lock (_lock)
            {
                return _availableUpdate;
            }
        }
    }

    /// <summary>
    /// Current installed version.
    /// </summary>
    public SysVersion CurrentVersion => _settings.CurrentVersion;

    /// <summary>
    /// Last time an update check was performed.
    /// </summary>
    public DateTimeOffset? LastChecked => _settings.LastChecked;

    /// <summary>
    /// Sets the update mode.
    /// </summary>
    public void SetMode(UpdateMode mode)
    {
        lock (_lock)
        {
            if (_settings.Mode != mode)
            {
                _settings = _settings with { Mode = mode };
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Checks for available updates.
    /// Returns null if no update is available.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        if (Mode == UpdateMode.Manual)
        {
            // In manual mode, check is still allowed but won't auto-notify
        }

        State = UpdateState.Checking;

        try
        {
            var update = await _checker.CheckAsync(CurrentVersion, ct).ConfigureAwait(false);

            lock (_lock)
            {
                _availableUpdate = update;
                _settings = _settings with { LastChecked = DateTimeOffset.UtcNow };
                SaveSettings();
            }

            State = UpdateState.Idle;

            if (update != null)
            {
                UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(update));
            }

            return update;
        }
        catch (Exception ex)
        {
            State = UpdateState.Idle;
            Error?.Invoke(this, new UpdateErrorEventArgs(UpdateErrorType.CheckFailed, ex.Message, ex));
            return null;
        }
    }

    /// <summary>
    /// Downloads the available update.
    /// Does not install - operator must explicitly approve installation.
    /// </summary>
    public async Task<UpdateDownloadResult> DownloadUpdateAsync(CancellationToken ct = default)
    {
        var update = AvailableUpdate;
        if (update == null)
        {
            return new UpdateDownloadResult(false, null, "No update available");
        }

        State = UpdateState.Downloading;

        try
        {
            var downloadPath = await _installer.DownloadAsync(update, ct).ConfigureAwait(false);

            State = UpdateState.ReadyToInstall;

            return new UpdateDownloadResult(true, downloadPath, null);
        }
        catch (Exception ex)
        {
            State = UpdateState.Idle;
            Error?.Invoke(this, new UpdateErrorEventArgs(UpdateErrorType.DownloadFailed, ex.Message, ex));
            return new UpdateDownloadResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Installs the downloaded update.
    /// Requires explicit operator approval.
    /// </summary>
    public async Task<UpdateInstallResult> InstallUpdateAsync(
        string downloadPath,
        bool requiresRestart,
        CancellationToken ct = default)
    {
        State = UpdateState.Installing;

        try
        {
            var success = await _installer.InstallAsync(downloadPath, requiresRestart, ct).ConfigureAwait(false);

            if (success)
            {
                // Update current version after successful install
                var newVersion = AvailableUpdate?.Version;
                if (newVersion != null)
                {
                    lock (_lock)
                    {
                        _settings = _settings with
                        {
                            CurrentVersion = newVersion,
                            LastInstalledVersion = newVersion,
                            LastInstalledAt = DateTimeOffset.UtcNow
                        };
                        _availableUpdate = null;
                        SaveSettings();
                    }
                }

                State = requiresRestart ? UpdateState.PendingRestart : UpdateState.Idle;
                return new UpdateInstallResult(true, requiresRestart, null);
            }
            else
            {
                State = UpdateState.Idle;
                return new UpdateInstallResult(false, false, "Installation failed");
            }
        }
        catch (Exception ex)
        {
            State = UpdateState.Idle;
            Error?.Invoke(this, new UpdateErrorEventArgs(UpdateErrorType.InstallFailed, ex.Message, ex));
            return new UpdateInstallResult(false, false, ex.Message);
        }
    }

    /// <summary>
    /// Dismisses the current available update notification.
    /// The update can still be installed later.
    /// </summary>
    public void DismissUpdate()
    {
        lock (_lock)
        {
            if (_availableUpdate != null)
            {
                var dismissedVersions = _settings.DismissedVersions.ToList();
                dismissedVersions.Add(_availableUpdate.Version.ToString());
                _settings = _settings with { DismissedVersions = dismissedVersions };
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets the changelog for the available update.
    /// </summary>
    public async Task<string?> GetChangelogAsync(CancellationToken ct = default)
    {
        var update = AvailableUpdate;
        if (update?.ChangelogUrl == null)
        {
            return null;
        }

        try
        {
            return await _checker.GetChangelogAsync(update.ChangelogUrl, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets rollback options if available.
    /// </summary>
    public IReadOnlyList<RollbackOption> GetRollbackOptions()
    {
        return _installer.GetRollbackOptions();
    }

    /// <summary>
    /// Rolls back to a previous version.
    /// </summary>
    public async Task<bool> RollbackAsync(SysVersion targetVersion, CancellationToken ct = default)
    {
        State = UpdateState.RollingBack;

        try
        {
            var success = await _installer.RollbackAsync(targetVersion, ct).ConfigureAwait(false);

            if (success)
            {
                lock (_lock)
                {
                    _settings = _settings with { CurrentVersion = targetVersion };
                    SaveSettings();
                }
            }

            State = UpdateState.Idle;
            return success;
        }
        catch (Exception ex)
        {
            State = UpdateState.Idle;
            Error?.Invoke(this, new UpdateErrorEventArgs(UpdateErrorType.RollbackFailed, ex.Message, ex));
            return false;
        }
    }

    private UpdateSettings? LoadSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<UpdateSettings>(json, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter(), new VersionJsonConverter() }
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
                Converters = { new JsonStringEnumConverter(), new VersionJsonConverter() }
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silently fail - settings loss is not critical
        }
    }
}

/// <summary>
/// Update modes controlling how updates are handled.
/// </summary>
public enum UpdateMode
{
    /// <summary>
    /// Operator downloads and installs updates manually.
    /// No automatic checks or notifications.
    /// </summary>
    Manual,

    /// <summary>
    /// Application notifies when updates are available.
    /// Operator must approve download and install.
    /// </summary>
    NotifyOnly,

    /// <summary>
    /// Updates are downloaded automatically.
    /// Operator must approve installation.
    /// </summary>
    AutoDownload,

    /// <summary>
    /// Updates are installed automatically (opt-in only).
    /// Operator can always roll back.
    /// </summary>
    AutoInstall
}

/// <summary>
/// Current state of the update process.
/// </summary>
public enum UpdateState
{
    /// <summary>No update activity.</summary>
    Idle,

    /// <summary>Checking for updates.</summary>
    Checking,

    /// <summary>Downloading update.</summary>
    Downloading,

    /// <summary>Update downloaded, ready to install.</summary>
    ReadyToInstall,

    /// <summary>Installing update.</summary>
    Installing,

    /// <summary>Update installed, restart required.</summary>
    PendingRestart,

    /// <summary>Rolling back to previous version.</summary>
    RollingBack
}

/// <summary>
/// Information about an available update.
/// </summary>
public sealed record UpdateInfo(
    SysVersion Version,
    string Title,
    string? Description,
    string? ChangelogUrl,
    string DownloadUrl,
    long? SizeBytes,
    string? Checksum,
    DateTimeOffset ReleasedAt,
    bool IsCritical,
    bool IsPrerelease
);

/// <summary>
/// Persistent update settings.
/// </summary>
public sealed record UpdateSettings(
    UpdateMode Mode,
    SysVersion CurrentVersion,
    DateTimeOffset? LastChecked,
    SysVersion? LastInstalledVersion,
    DateTimeOffset? LastInstalledAt,
    IReadOnlyList<string> DismissedVersions
)
{
    public static UpdateSettings Default => new(
        Mode: UpdateMode.Manual,
        CurrentVersion: new SysVersion(1, 0, 0),
        LastChecked: null,
        LastInstalledVersion: null,
        LastInstalledAt: null,
        DismissedVersions: []
    );
}

/// <summary>
/// Result of downloading an update.
/// </summary>
public sealed record UpdateDownloadResult(
    bool Success,
    string? DownloadPath,
    string? Error
);

/// <summary>
/// Result of installing an update.
/// </summary>
public sealed record UpdateInstallResult(
    bool Success,
    bool RequiresRestart,
    string? Error
);

/// <summary>
/// Option for rolling back to a previous version.
/// </summary>
public sealed record RollbackOption(
    SysVersion Version,
    DateTimeOffset InstalledAt,
    string? Notes
);

/// <summary>
/// Types of update errors.
/// </summary>
public enum UpdateErrorType
{
    CheckFailed,
    DownloadFailed,
    InstallFailed,
    RollbackFailed
}

/// <summary>
/// Event args for update available.
/// </summary>
public sealed class UpdateAvailableEventArgs : EventArgs
{
    public UpdateInfo Update { get; }

    public UpdateAvailableEventArgs(UpdateInfo update)
    {
        Update = update;
    }
}

/// <summary>
/// Event args for state changes.
/// </summary>
public sealed class UpdateStateChangedEventArgs : EventArgs
{
    public UpdateState OldState { get; }
    public UpdateState NewState { get; }

    public UpdateStateChangedEventArgs(UpdateState oldState, UpdateState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// Event args for errors.
/// </summary>
public sealed class UpdateErrorEventArgs : EventArgs
{
    public UpdateErrorType Type { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    public UpdateErrorEventArgs(UpdateErrorType type, string message, Exception? exception = null)
    {
        Type = type;
        Message = message;
        Exception = exception;
    }
}

/// <summary>
/// Interface for checking for updates.
/// </summary>
public interface IUpdateChecker
{
    Task<UpdateInfo?> CheckAsync(SysVersion currentVersion, CancellationToken ct = default);
    Task<string?> GetChangelogAsync(string changelogUrl, CancellationToken ct = default);
}

/// <summary>
/// Interface for installing updates.
/// </summary>
public interface IUpdateInstaller
{
    Task<string> DownloadAsync(UpdateInfo update, CancellationToken ct = default);
    Task<bool> InstallAsync(string downloadPath, bool requiresRestart, CancellationToken ct = default);
    Task<bool> RollbackAsync(SysVersion targetVersion, CancellationToken ct = default);
    IReadOnlyList<RollbackOption> GetRollbackOptions();
}

/// <summary>
/// JSON converter for Version type.
/// </summary>
internal sealed class VersionJsonConverter : JsonConverter<SysVersion>
{
    public override SysVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value != null ? SysVersion.Parse(value) : null;
    }

    public override void Write(Utf8JsonWriter writer, SysVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
