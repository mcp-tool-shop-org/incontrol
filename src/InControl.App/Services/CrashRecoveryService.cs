using System.Diagnostics;
using System.Text.Json;

namespace InControl.App.Services;

/// <summary>
/// Service for detecting and recovering from unexpected application terminations.
/// Provides calm, user-friendly recovery experience without blame language.
/// </summary>
public sealed class CrashRecoveryService
{
    private const string CrashMarkerFileName = "crash-marker.json";

    private static CrashRecoveryService? _instance;
    private static readonly object _lock = new();

    private readonly string _dataPath;
    private bool _isRecoveryMode;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static CrashRecoveryService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new CrashRecoveryService();
                }
            }
            return _instance;
        }
    }

    private CrashRecoveryService()
    {
        try
        {
            _dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InControl");

            Directory.CreateDirectory(_dataPath);
        }
        catch (Exception ex)
        {
            // Fallback to temp directory if LocalApplicationData is not accessible
            Debug.WriteLine($"Failed to create data directory: {ex.Message}");
            _dataPath = Path.Combine(Path.GetTempPath(), "InControl");
            try
            {
                Directory.CreateDirectory(_dataPath);
            }
            catch
            {
                // Last resort - use current directory (will fail gracefully)
                _dataPath = Path.GetTempPath();
            }
        }
    }

    /// <summary>
    /// Whether the app is in recovery mode from a previous crash.
    /// </summary>
    public bool IsRecoveryMode => _isRecoveryMode;

    /// <summary>
    /// Check for crash markers and prepare recovery if needed.
    /// Call this at app startup before main window is created.
    /// </summary>
    public void CheckForCrashRecovery()
    {
        var crashMarkerPath = Path.Combine(_dataPath, CrashMarkerFileName);

        if (File.Exists(crashMarkerPath))
        {
            // A marker left behind means the last run did not exit cleanly,
            // even when the marker itself cannot be read.
            _isRecoveryMode = true;

            try
            {
                var json = File.ReadAllText(crashMarkerPath);
                var crashInfo = JsonSerializer.Deserialize<CrashMarkerInfo>(json);

                if (crashInfo != null)
                {
                    Debug.WriteLine($"Recovery mode: Previous session ended unexpectedly at {crashInfo.Timestamp}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read crash marker: {ex.Message}");
            }
            finally
            {
                // The notice is raised from IsRecoveryMode, so the marker can go now.
                try { File.Delete(crashMarkerPath); } catch { }
            }
        }
    }

    /// <summary>
    /// Set crash marker at startup to detect unexpected exits.
    /// </summary>
    public void SetCrashMarker()
    {
        var crashMarkerPath = Path.Combine(_dataPath, CrashMarkerFileName);

        var crashInfo = new CrashMarkerInfo
        {
            Timestamp = DateTime.UtcNow,
            Version = GetAppVersion(),
            ProcessId = Environment.ProcessId
        };

        try
        {
            var json = JsonSerializer.Serialize(crashInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(crashMarkerPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set crash marker: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear crash marker on clean shutdown.
    /// Call this when app exits normally.
    /// </summary>
    public void ClearCrashMarker()
    {
        var crashMarkerPath = Path.Combine(_dataPath, CrashMarkerFileName);

        try
        {
            if (File.Exists(crashMarkerPath))
            {
                File.Delete(crashMarkerPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear crash marker: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear recovery mode after the notice has been shown.
    /// </summary>
    public void AcknowledgeRecovery()
    {
        _isRecoveryMode = false;
    }

    /// <summary>
    /// Get the recovery message for the user.
    /// Uses calm, non-blame language.
    /// </summary>
    public string GetRecoveryMessage()
    {
        return "InControl closed unexpectedly last time. Your saved chats are intact.";
    }

    /// <summary>
    /// Get details about what was kept.
    /// </summary>
    public string GetRecoveryDetails()
    {
        return "Saved chats and settings were not changed.";
    }

    private static string GetAppVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}

/// <summary>
/// Information stored in crash marker file.
/// </summary>
public class CrashMarkerInfo
{
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = "";
    public int ProcessId { get; set; }
}
