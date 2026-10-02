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
    private const string LastSessionFileName = "last-session.json";

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
    /// Information about the last session, if recovered.
    /// </summary>
    public LastSessionInfo? RecoveredSession { get; private set; }

    /// <summary>
    /// Check for crash markers and prepare recovery if needed.
    /// Call this at app startup before main window is created.
    /// </summary>
    public void CheckForCrashRecovery()
    {
        var crashMarkerPath = Path.Combine(_dataPath, CrashMarkerFileName);

        if (File.Exists(crashMarkerPath))
        {
            try
            {
                var json = File.ReadAllText(crashMarkerPath);
                var crashInfo = JsonSerializer.Deserialize<CrashMarkerInfo>(json);

                if (crashInfo != null)
                {
                    _isRecoveryMode = true;
                    LoadLastSession();

                    // Log recovery
                    Debug.WriteLine($"Recovery mode: Previous session ended unexpectedly at {crashInfo.Timestamp}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read crash marker: {ex.Message}");
            }
            finally
            {
                // Remove crash marker regardless
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
    /// Save current session state for potential recovery.
    /// Call this periodically during normal operation.
    /// </summary>
    public void SaveSessionState(string? activeSessionId, string? lastPrompt, string? selectedModel)
    {
        var sessionPath = Path.Combine(_dataPath, LastSessionFileName);

        var sessionInfo = new LastSessionInfo
        {
            Timestamp = DateTime.UtcNow,
            ActiveSessionId = activeSessionId,
            LastPrompt = lastPrompt,
            SelectedModel = selectedModel,
            Version = GetAppVersion()
        };

        try
        {
            var json = JsonSerializer.Serialize(sessionInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(sessionPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save session state: {ex.Message}");
        }
    }

    /// <summary>
    /// Load last session info for recovery.
    /// </summary>
    private void LoadLastSession()
    {
        var sessionPath = Path.Combine(_dataPath, LastSessionFileName);

        if (File.Exists(sessionPath))
        {
            try
            {
                var json = File.ReadAllText(sessionPath);
                RecoveredSession = JsonSerializer.Deserialize<LastSessionInfo>(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load last session: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clear recovery mode after user acknowledges.
    /// </summary>
    public void AcknowledgeRecovery()
    {
        _isRecoveryMode = false;
        RecoveredSession = null;

        // Clean up last session file
        var sessionPath = Path.Combine(_dataPath, LastSessionFileName);
        try { if (File.Exists(sessionPath)) File.Delete(sessionPath); } catch { }
    }

    /// <summary>
    /// Get the recovery message for the user.
    /// Uses calm, non-blame language.
    /// </summary>
    public string GetRecoveryMessage()
    {
        return "InControl was restored after an unexpected stop.";
    }

    /// <summary>
    /// Get details about what can be recovered.
    /// </summary>
    public string GetRecoveryDetails()
    {
        if (RecoveredSession == null)
        {
            return "Your sessions and settings are safe.";
        }

        var details = "Your sessions and settings are safe.";

        if (!string.IsNullOrEmpty(RecoveredSession.ActiveSessionId))
        {
            details += "\n• Last active session is available";
        }

        if (!string.IsNullOrEmpty(RecoveredSession.LastPrompt))
        {
            details += "\n• Unsent prompt was recovered";
        }

        return details;
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

/// <summary>
/// Information about the last session for recovery.
/// </summary>
public class LastSessionInfo
{
    public DateTime Timestamp { get; set; }
    public string? ActiveSessionId { get; set; }
    public string? LastPrompt { get; set; }
    public string? SelectedModel { get; set; }
    public string Version { get; set; } = "";
}
