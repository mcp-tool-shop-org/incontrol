namespace InControl.Core.Storage;

/// <summary>
/// Abstraction over data path resolution for dependency injection.
/// Inject this interface instead of referencing the static <see cref="DataPaths"/> class directly.
/// </summary>
public interface IDataPathsProvider
{
    /// <inheritdoc cref="DataPaths.AppDataRoot"/>
    string AppDataRoot { get; }

    /// <inheritdoc cref="DataPaths.Sessions"/>
    string Sessions { get; }

    /// <inheritdoc cref="DataPaths.Logs"/>
    string Logs { get; }

    /// <inheritdoc cref="DataPaths.Cache"/>
    string Cache { get; }

    /// <inheritdoc cref="DataPaths.Exports"/>
    string Exports { get; }

    /// <inheritdoc cref="DataPaths.Config"/>
    string Config { get; }

    /// <inheritdoc cref="DataPaths.Temp"/>
    string Temp { get; }

    /// <inheritdoc cref="DataPaths.Support"/>
    string Support { get; }

    /// <inheritdoc cref="DataPaths.Configuration"/>
    DataPathsConfig Configuration { get; }

    /// <inheritdoc cref="DataPaths.IsPathAllowed"/>
    bool IsPathAllowed(string path);
}

/// <summary>
/// Default implementation of <see cref="IDataPathsProvider"/> that delegates to the static <see cref="DataPaths"/> class.
/// Register as singleton in DI: <c>services.AddSingleton&lt;IDataPathsProvider, DataPathsProvider&gt;();</c>
/// </summary>
public sealed class DataPathsProvider : IDataPathsProvider
{
    /// <inheritdoc />
    public string AppDataRoot => DataPaths.AppDataRoot;

    /// <inheritdoc />
    public string Sessions => DataPaths.Sessions;

    /// <inheritdoc />
    public string Logs => DataPaths.Logs;

    /// <inheritdoc />
    public string Cache => DataPaths.Cache;

    /// <inheritdoc />
    public string Exports => DataPaths.Exports;

    /// <inheritdoc />
    public string Config => DataPaths.Config;

    /// <inheritdoc />
    public string Temp => DataPaths.Temp;

    /// <inheritdoc />
    public string Support => DataPaths.Support;

    /// <inheritdoc />
    public DataPathsConfig Configuration => DataPaths.Configuration;

    /// <inheritdoc />
    public bool IsPathAllowed(string path) => DataPaths.IsPathAllowed(path);
}

/// <summary>
/// Defines all data storage paths for the application.
/// All paths are explicit and bounded to specific roots.
/// For DI scenarios, inject <see cref="IDataPathsProvider"/> instead of using this class directly.
/// </summary>
public static class DataPaths
{
    private static readonly Lazy<DataPathsConfig> _config = new(InitializePaths);
    private static DataPathsConfig? _override;

    /// <summary>
    /// Overrides the default path configuration.
    /// Call before any path property is accessed. Useful for testing and custom deployments.
    /// </summary>
    /// <param name="config">The custom configuration to use.</param>
    /// <exception cref="InvalidOperationException">Thrown if paths have already been read.</exception>
    public static void Configure(DataPathsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _override = config;
    }

    /// <summary>
    /// Resets configuration to the default (environment-derived) paths.
    /// Primarily intended for test cleanup.
    /// </summary>
    public static void ResetConfiguration() => _override = null;

    private static DataPathsConfig CurrentConfig => _override ?? _config.Value;

    /// <summary>
    /// Gets the root directory for all application data.
    /// Default: %LOCALAPPDATA%\InControl
    /// </summary>
    public static string AppDataRoot => CurrentConfig.AppDataRoot;

    /// <summary>
    /// Gets the directory where sessions are stored.
    /// Path: {AppDataRoot}\sessions
    /// </summary>
    public static string Sessions => CurrentConfig.Sessions;

    /// <summary>
    /// Gets the directory where log files are stored.
    /// Path: {AppDataRoot}\logs
    /// </summary>
    public static string Logs => CurrentConfig.Logs;

    /// <summary>
    /// Gets the directory where cache files are stored.
    /// Path: {AppDataRoot}\cache
    /// </summary>
    public static string Cache => CurrentConfig.Cache;

    /// <summary>
    /// Gets the directory where exported files are stored.
    /// Path: {Documents}\InControl\exports
    /// </summary>
    public static string Exports => CurrentConfig.Exports;

    /// <summary>
    /// Gets the directory where configuration files are stored.
    /// Path: {AppDataRoot}\config
    /// </summary>
    public static string Config => CurrentConfig.Config;

    /// <summary>
    /// Gets the directory where temporary files are stored.
    /// Path: {AppDataRoot}\temp
    /// </summary>
    public static string Temp => CurrentConfig.Temp;

    /// <summary>
    /// Gets the directory where support bundles are stored.
    /// Path: {AppDataRoot}\support
    /// </summary>
    public static string Support => CurrentConfig.Support;

    /// <summary>
    /// Gets all paths configuration.
    /// </summary>
    public static DataPathsConfig Configuration => CurrentConfig;

    /// <summary>
    /// Validates that a path is within allowed write boundaries.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>True if the path is within allowed boundaries.</returns>
    public static bool IsPathAllowed(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var fullPath = Path.GetFullPath(path);

        // Allowed roots
        var allowedRoots = new[]
        {
            CurrentConfig.AppDataRoot,
            CurrentConfig.Exports
        };

        return allowedRoots.Any(root =>
            fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the purpose description for a given path.
    /// </summary>
    /// <param name="path">The path to describe.</param>
    /// <returns>A description of what the path is used for.</returns>
    public static string GetPathPurpose(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Unknown";

        var fullPath = Path.GetFullPath(path);

        if (fullPath.StartsWith(CurrentConfig.Sessions, StringComparison.OrdinalIgnoreCase))
            return "Session data (conversations, messages)";

        if (fullPath.StartsWith(CurrentConfig.Logs, StringComparison.OrdinalIgnoreCase))
            return "Application logs";

        if (fullPath.StartsWith(CurrentConfig.Cache, StringComparison.OrdinalIgnoreCase))
            return "Cached data (model info, temporary results)";

        if (fullPath.StartsWith(CurrentConfig.Exports, StringComparison.OrdinalIgnoreCase))
            return "Exported sessions and data";

        if (fullPath.StartsWith(CurrentConfig.Config, StringComparison.OrdinalIgnoreCase))
            return "User configuration and settings";

        if (fullPath.StartsWith(CurrentConfig.Temp, StringComparison.OrdinalIgnoreCase))
            return "Temporary files (cleared on startup)";

        if (fullPath.StartsWith(CurrentConfig.Support, StringComparison.OrdinalIgnoreCase))
            return "Support bundles and diagnostics";

        if (fullPath.StartsWith(CurrentConfig.AppDataRoot, StringComparison.OrdinalIgnoreCase))
            return "Application data";

        return "Unknown (outside allowed boundaries)";
    }

    /// <summary>
    /// Ensures all required directories exist.
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(CurrentConfig.Sessions);
        Directory.CreateDirectory(CurrentConfig.Logs);
        Directory.CreateDirectory(CurrentConfig.Cache);
        Directory.CreateDirectory(CurrentConfig.Exports);
        Directory.CreateDirectory(CurrentConfig.Config);
        Directory.CreateDirectory(CurrentConfig.Temp);
        Directory.CreateDirectory(CurrentConfig.Support);
    }

    /// <summary>
    /// Clears the temporary directory.
    /// </summary>
    public static void ClearTemp()
    {
        var tempPath = CurrentConfig.Temp;
        if (Directory.Exists(tempPath))
        {
            foreach (var file in Directory.GetFiles(tempPath))
            {
                try { File.Delete(file); }
                catch (IOException) { /* Ignore cleanup errors */ }
            }

            foreach (var dir in Directory.GetDirectories(tempPath))
            {
                try { Directory.Delete(dir, recursive: true); }
                catch (IOException) { /* Ignore cleanup errors */ }
            }
        }
    }

    /// <summary>
    /// Gets storage statistics for all paths.
    /// </summary>
    public static StorageStats GetStorageStats()
    {
        return new StorageStats(
            SessionsSize: GetDirectorySize(CurrentConfig.Sessions),
            LogsSize: GetDirectorySize(CurrentConfig.Logs),
            CacheSize: GetDirectorySize(CurrentConfig.Cache),
            ExportsSize: GetDirectorySize(CurrentConfig.Exports),
            ConfigSize: GetDirectorySize(CurrentConfig.Config),
            TempSize: GetDirectorySize(CurrentConfig.Temp),
            SupportSize: GetDirectorySize(CurrentConfig.Support)
        );
    }

    private static DataPathsConfig InitializePaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var appDataRoot = Path.Combine(localAppData, "InControl");

        return new DataPathsConfig(
            AppDataRoot: appDataRoot,
            Sessions: Path.Combine(appDataRoot, "sessions"),
            Logs: Path.Combine(appDataRoot, "logs"),
            Cache: Path.Combine(appDataRoot, "cache"),
            Exports: Path.Combine(documents, "InControl", "exports"),
            Config: Path.Combine(appDataRoot, "config"),
            Temp: Path.Combine(appDataRoot, "temp"),
            Support: Path.Combine(appDataRoot, "support")
        );
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }
        catch (IOException)
        {
            return 0;
        }
    }
}

/// <summary>
/// Configuration record containing all data paths.
/// </summary>
public sealed record DataPathsConfig(
    string AppDataRoot,
    string Sessions,
    string Logs,
    string Cache,
    string Exports,
    string Config,
    string Temp,
    string Support
);

/// <summary>
/// Storage statistics for all paths.
/// </summary>
public sealed record StorageStats(
    long SessionsSize,
    long LogsSize,
    long CacheSize,
    long ExportsSize,
    long ConfigSize,
    long TempSize,
    long SupportSize
)
{
    /// <summary>
    /// Gets the total storage used across all paths.
    /// </summary>
    public long TotalSize => SessionsSize + LogsSize + CacheSize + ExportsSize + ConfigSize + TempSize + SupportSize;

    /// <summary>
    /// Gets a formatted string of the total storage used.
    /// </summary>
    public string TotalFormatted => FormatBytes(TotalSize);

    /// <summary>
    /// Formats bytes to human-readable string.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }
}
