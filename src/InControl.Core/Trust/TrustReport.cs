using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using InControl.Core.State;

namespace InControl.Core.Trust;

/// <summary>
/// A comprehensive self-audit report of the application's current state.
/// This provides user-visible trust signals about the running instance.
/// </summary>
public sealed record TrustReport
{
    /// <summary>
    /// Build information for the running application.
    /// </summary>
    public required BuildInfo Build { get; init; }

    /// <summary>
    /// Runtime environment information.
    /// </summary>
    public required RuntimeInfo Runtime { get; init; }

    /// <summary>
    /// Security-relevant configuration.
    /// </summary>
    public required SecurityConfig Security { get; init; }

    /// <summary>
    /// When this report was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Overall trust level based on configuration.
    /// </summary>
    [JsonIgnore]
    public TrustLevel TrustLevel => CalculateTrustLevel();

    /// <summary>
    /// Human-readable summary of trust status.
    /// </summary>
    public string TrustSummary => TrustLevel switch
    {
        TrustLevel.High => "All security features enabled, release build",
        TrustLevel.Medium => "Some security features disabled or debug build",
        TrustLevel.Low => "Multiple security features disabled",
        _ => "Unknown trust state"
    };

    private TrustLevel CalculateTrustLevel()
    {
        var issues = 0;

        if (Build.IsDebugBuild) issues++;
        if (!Security.PathBoundaryEnforced) issues++;
        if (!Security.InferenceIsolated) issues++;
        if (Security.AllowedDataPaths.Count == 0) issues++;

        return issues switch
        {
            0 => TrustLevel.High,
            1 or 2 => TrustLevel.Medium,
            _ => TrustLevel.Low
        };
    }

    /// <summary>
    /// Creates a trust report from the current application state.
    /// </summary>
    public static TrustReport Generate(SecurityConfig security)
    {
        return new TrustReport
        {
            Build = BuildInfo.FromEntryAssembly(),
            Runtime = RuntimeInfo.Current(),
            Security = security
        };
    }

    /// <summary>
    /// Serializes this report to JSON for display or logging.
    /// </summary>
    public string ToJson(bool compact = false) => StateSerializer.Serialize(this, compact);
}

/// <summary>
/// Runtime environment information.
/// </summary>
public sealed record RuntimeInfo
{
    /// <summary>
    /// The .NET runtime description.
    /// </summary>
    public required string Framework { get; init; }

    /// <summary>
    /// The operating system description.
    /// </summary>
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// The process architecture (x64, Arm64, etc.).
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// The machine name (for identification).
    /// </summary>
    public required string MachineName { get; init; }

    /// <summary>
    /// The current user name.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Whether running as 64-bit process.
    /// </summary>
    public bool Is64BitProcess { get; init; }

    /// <summary>
    /// Number of available processors.
    /// </summary>
    public int ProcessorCount { get; init; }

    /// <summary>
    /// Process ID for the running instance.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// When the process started.
    /// </summary>
    public DateTimeOffset ProcessStartTime { get; init; }

    /// <summary>
    /// Creates RuntimeInfo from the current environment.
    /// </summary>
    public static RuntimeInfo Current()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();

        return new RuntimeInfo
        {
            Framework = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            Is64BitProcess = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount,
            ProcessId = Environment.ProcessId,
            ProcessStartTime = process.StartTime.ToUniversalTime()
        };
    }
}

/// <summary>
/// Security-relevant configuration for trust verification.
/// </summary>
public sealed record SecurityConfig
{
    /// <summary>
    /// Whether path boundary enforcement is active.
    /// </summary>
    public bool PathBoundaryEnforced { get; init; }

    /// <summary>
    /// List of allowed data paths.
    /// </summary>
    public IReadOnlyList<string> AllowedDataPaths { get; init; } = [];

    /// <summary>
    /// Whether inference is properly isolated.
    /// </summary>
    public bool InferenceIsolated { get; init; }

    /// <summary>
    /// The configured inference backend.
    /// </summary>
    public string InferenceBackend { get; init; } = "Unknown";

    /// <summary>
    /// Whether telemetry is enabled.
    /// </summary>
    public bool TelemetryEnabled { get; init; }

    /// <summary>
    /// Additional security notes or warnings.
    /// </summary>
    public IReadOnlyList<string> SecurityNotes { get; init; } = [];

    /// <summary>
    /// Creates a default secure configuration.
    /// </summary>
    public static SecurityConfig Default() => new()
    {
        PathBoundaryEnforced = true,
        InferenceIsolated = true,
        TelemetryEnabled = false
    };
}

/// <summary>
/// Trust levels for the application.
/// </summary>
public enum TrustLevel
{
    /// <summary>
    /// All security features enabled, production-ready.
    /// </summary>
    High,

    /// <summary>
    /// Some features disabled or debug build.
    /// </summary>
    Medium,

    /// <summary>
    /// Multiple security features disabled.
    /// </summary>
    Low
}
