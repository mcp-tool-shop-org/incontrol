namespace InControl.Core.Configuration;

/// <summary>
/// Configuration options for logging.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Logging";

    /// <summary>
    /// Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal).
    /// </summary>
    public string MinLevel { get; set; } = "Information";

    /// <summary>
    /// Path for log files. Supports environment variable expansion.
    /// </summary>
    public string FilePath { get; set; } = "%LOCALAPPDATA%/InControl/logs";

    /// <summary>
    /// Whether to write logs to the console.
    /// </summary>
    public bool WriteToConsole { get; set; } = true;

    /// <summary>
    /// Whether to write logs to file.
    /// </summary>
    public bool WriteToFile { get; set; } = true;

    /// <summary>
    /// Maximum log file size in megabytes before rolling.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Number of log files to retain.
    /// </summary>
    public int RetainedFileCount { get; set; } = 5;

    /// <summary>
    /// Gets the expanded file path with environment variables resolved.
    /// </summary>
    public string ExpandedFilePath => Environment.ExpandEnvironmentVariables(FilePath);
}
