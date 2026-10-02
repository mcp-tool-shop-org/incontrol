using System.Reflection;

namespace InControl.Core.Version;

/// <summary>
/// Provides access to application version information.
/// Version is embedded at build time from Directory.Build.props.
/// </summary>
public static class AppVersion
{
    private static readonly Lazy<VersionInfo> _info = new(LoadVersionInfo);

    /// <summary>
    /// Gets the full version string (e.g., "0.4.0-alpha").
    /// </summary>
    public static string Full => _info.Value.Full;

    /// <summary>
    /// Gets the semantic version (e.g., "0.4.0").
    /// </summary>
    public static string SemVer => _info.Value.SemVer;

    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public static int Major => _info.Value.Major;

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public static int Minor => _info.Value.Minor;

    /// <summary>
    /// Gets the patch version number.
    /// </summary>
    public static int Patch => _info.Value.Patch;

    /// <summary>
    /// Gets the prerelease tag (e.g., "alpha", "beta", or empty for release).
    /// </summary>
    public static string Prerelease => _info.Value.Prerelease;

    /// <summary>
    /// Gets whether this is a prerelease version.
    /// </summary>
    public static bool IsPrerelease => !string.IsNullOrEmpty(_info.Value.Prerelease);

    /// <summary>
    /// Gets the product name.
    /// </summary>
    public static string ProductName => _info.Value.ProductName;

    /// <summary>
    /// Gets the copyright notice.
    /// </summary>
    public static string Copyright => _info.Value.Copyright;

    /// <summary>
    /// Gets the build configuration (Debug/Release).
    /// </summary>
    public static string Configuration => _info.Value.Configuration;

    /// <summary>
    /// Gets the complete version information.
    /// </summary>
    public static VersionInfo Info => _info.Value;

    private static VersionInfo LoadVersionInfo()
    {
        var assembly = typeof(AppVersion).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.0.0.0";
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "InControl";
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

        // Parse informational version (e.g., "0.4.0-alpha+abc123")
        var versionParts = informationalVersion.Split('+')[0]; // Remove build metadata
        var dashIndex = versionParts.IndexOf('-');

        string semVer;
        string prerelease;

        if (dashIndex > 0)
        {
            semVer = versionParts[..dashIndex];
            prerelease = versionParts[(dashIndex + 1)..];
        }
        else
        {
            semVer = versionParts;
            prerelease = string.Empty;
        }

        var semVerParts = semVer.Split('.');
        var major = semVerParts.Length > 0 && int.TryParse(semVerParts[0], out var m) ? m : 0;
        var minor = semVerParts.Length > 1 && int.TryParse(semVerParts[1], out var n) ? n : 0;
        var patch = semVerParts.Length > 2 && int.TryParse(semVerParts[2], out var p) ? p : 0;

#if DEBUG
        var configuration = "Debug";
#else
        var configuration = "Release";
#endif

        return new VersionInfo(
            Full: informationalVersion,
            SemVer: semVer,
            Major: major,
            Minor: minor,
            Patch: patch,
            Prerelease: prerelease,
            ProductName: product,
            Copyright: copyright,
            Configuration: configuration
        );
    }
}

/// <summary>
/// Contains complete version information for the application.
/// </summary>
public sealed record VersionInfo(
    string Full,
    string SemVer,
    int Major,
    int Minor,
    int Patch,
    string Prerelease,
    string ProductName,
    string Copyright,
    string Configuration
)
{
    /// <summary>
    /// Gets a user-friendly display string (e.g., "InControl 0.4.0-alpha").
    /// </summary>
    public string DisplayString => $"{ProductName} {Full}";

    /// <summary>
    /// Gets a short display string (e.g., "v0.4.0-alpha").
    /// </summary>
    public string ShortDisplay => $"v{Full}";
}
