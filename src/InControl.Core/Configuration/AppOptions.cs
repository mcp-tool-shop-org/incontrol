namespace InControl.Core.Configuration;

/// <summary>
/// Application-wide configuration options.
/// </summary>
public sealed class AppOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "App";

    /// <summary>
    /// Application theme (System, Light, Dark).
    /// </summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Whether to show the system tray icon.
    /// </summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>
    /// Whether to minimize to tray instead of closing.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Whether to start minimized.
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Whether to check for updates on startup.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// Path for storing conversations and user data.
    /// </summary>
    public string DataPath { get; set; } = "%LOCALAPPDATA%/InControl/data";

    /// <summary>
    /// Gets the expanded data path with environment variables resolved.
    /// </summary>
    public string ExpandedDataPath => Environment.ExpandEnvironmentVariables(DataPath);
}
