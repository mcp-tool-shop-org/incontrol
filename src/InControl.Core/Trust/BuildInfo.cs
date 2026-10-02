using System.Reflection;
using System.Text.Json.Serialization;

namespace InControl.Core.Trust;

/// <summary>
/// Immutable record of build-time information for trust verification.
/// This enables users to verify they're running an expected build.
/// </summary>
public sealed record BuildInfo
{
    /// <summary>
    /// The version from the assembly.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The informational version (may include commit hash).
    /// </summary>
    public required string InformationalVersion { get; init; }

    /// <summary>
    /// The git commit hash if available.
    /// </summary>
    public string? CommitHash { get; init; }

    /// <summary>
    /// When this build was created (UTC).
    /// </summary>
    public DateTimeOffset? BuildTimestamp { get; init; }

    /// <summary>
    /// The build configuration (Debug/Release).
    /// </summary>
    public required string Configuration { get; init; }

    /// <summary>
    /// The target framework.
    /// </summary>
    public required string TargetFramework { get; init; }

    /// <summary>
    /// Whether this is a debug build.
    /// </summary>
    [JsonIgnore]
    public bool IsDebugBuild => Configuration.Equals("Debug", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates BuildInfo from the entry assembly.
    /// </summary>
    public static BuildInfo FromEntryAssembly()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return FromAssembly(assembly);
    }

    /// <summary>
    /// Creates BuildInfo from a specific assembly.
    /// </summary>
    public static BuildInfo FromAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        var version = assemblyName.Version?.ToString() ?? "0.0.0.0";

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version;

        // Parse commit hash from informational version if present
        // Format is typically "1.0.0+abc123" or "1.0.0-preview+abc123"
        string? commitHash = null;
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex >= 0 && plusIndex < informationalVersion.Length - 1)
        {
            commitHash = informationalVersion[(plusIndex + 1)..];
        }

        // Try to get build timestamp from assembly metadata
        DateTimeOffset? buildTimestamp = null;
        var timestampAttr = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimestamp");
        if (timestampAttr?.Value is not null &&
            DateTimeOffset.TryParse(timestampAttr.Value, out var parsed))
        {
            buildTimestamp = parsed;
        }

        // Determine configuration
#if DEBUG
        var configuration = "Debug";
#else
        var configuration = "Release";
#endif

        // Get target framework from metadata
        var targetFramework = assembly
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?
            .FrameworkName ?? "Unknown";

        return new BuildInfo
        {
            Version = version,
            InformationalVersion = informationalVersion,
            CommitHash = commitHash,
            BuildTimestamp = buildTimestamp,
            Configuration = configuration,
            TargetFramework = targetFramework
        };
    }

    /// <summary>
    /// Returns a compact single-line representation.
    /// </summary>
    public override string ToString()
    {
        var hash = CommitHash is not null ? $" ({CommitHash[..Math.Min(7, CommitHash.Length)]})" : "";
        return $"InControl v{Version}{hash} [{Configuration}]";
    }
}
