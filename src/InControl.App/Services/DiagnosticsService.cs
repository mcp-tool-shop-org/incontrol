using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace InControl.App.Services;

/// <summary>
/// Service for running system diagnostics and generating reports.
/// Used by Help page to help users self-rescue common issues.
/// </summary>
public sealed class DiagnosticsService
{
    private static DiagnosticsService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static DiagnosticsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new DiagnosticsService();
                }
            }
            return _instance;
        }
    }

    private DiagnosticsService() { }

    /// <summary>
    /// Run all diagnostics and return a comprehensive report.
    /// </summary>
    public async Task<DiagnosticsReport> RunDiagnosticsAsync()
    {
        var report = new DiagnosticsReport
        {
            Timestamp = DateTime.UtcNow,
            AppVersion = GetAppVersion()
        };

        // Run all checks concurrently
        var tasks = new[]
        {
            CheckOllamaConnectionAsync(report),
            CheckSystemResourcesAsync(report),
            CheckStorageAsync(report),
            CheckNetworkAsync(report)
        };

        await Task.WhenAll(tasks);

        // Determine overall status
        report.OverallStatus = DetermineOverallStatus(report);

        return report;
    }

    private async Task CheckOllamaConnectionAsync(DiagnosticsReport report)
    {
        var check = new DiagnosticCheck
        {
            Name = "Ollama Connection",
            Category = "Model Engine"
        };

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://localhost:11434/api/version");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                check.Status = DiagnosticStatus.Pass;
                check.Message = "Ollama is running";
                check.Details = $"Endpoint: localhost:11434, Response: {content}";
            }
            else
            {
                check.Status = DiagnosticStatus.Warning;
                check.Message = $"Ollama returned status {response.StatusCode}";
                check.Details = "Ollama may not be fully operational";
            }
        }
        catch (HttpRequestException)
        {
            check.Status = DiagnosticStatus.Fail;
            check.Message = "Ollama is not running";
            check.Details = "Start Ollama service or install from ollama.com";
            check.HelpLink = "help://troubleshooting/ollama-not-running";
        }
        catch (TaskCanceledException)
        {
            check.Status = DiagnosticStatus.Fail;
            check.Message = "Ollama connection timed out";
            check.Details = "Ollama may be overloaded or unresponsive";
        }

        report.Checks.Add(check);
    }

    private Task CheckSystemResourcesAsync(DiagnosticsReport report)
    {
        var check = new DiagnosticCheck
        {
            Name = "System Resources",
            Category = "System"
        };

        try
        {
            using var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);

            // Get system memory
            var gcInfo = GC.GetGCMemoryInfo();
            var totalMemoryGB = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);

            check.Status = memoryMB < 500 ? DiagnosticStatus.Pass :
                          memoryMB < 1000 ? DiagnosticStatus.Warning :
                          DiagnosticStatus.Fail;

            check.Message = $"App using {memoryMB} MB";
            check.Details = $"System has {totalMemoryGB:F1} GB available";

            if (check.Status == DiagnosticStatus.Fail)
            {
                check.Details += "\nConsider restarting the app to free memory";
            }
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Warning;
            check.Message = "Could not check memory";
            check.Details = ex.Message;
        }

        report.Checks.Add(check);
        return Task.CompletedTask;
    }

    private Task CheckStorageAsync(DiagnosticsReport report)
    {
        var check = new DiagnosticCheck
        {
            Name = "Storage",
            Category = "System"
        };

        try
        {
            var dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InControl");

            var drive = new DriveInfo(Path.GetPathRoot(dataPath) ?? "C:");
            var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

            check.Status = freeGB > 10 ? DiagnosticStatus.Pass :
                          freeGB > 2 ? DiagnosticStatus.Warning :
                          DiagnosticStatus.Fail;

            check.Message = $"{freeGB:F1} GB free on {drive.Name}";
            check.Details = $"Data path: {dataPath}";

            if (check.Status != DiagnosticStatus.Pass)
            {
                check.Details += "\nLow disk space may prevent model downloads";
            }
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Warning;
            check.Message = "Could not check storage";
            check.Details = ex.Message;
        }

        report.Checks.Add(check);
        return Task.CompletedTask;
    }

    private async Task CheckNetworkAsync(DiagnosticsReport report)
    {
        var check = new DiagnosticCheck
        {
            Name = "Network (Optional)",
            Category = "Connectivity"
        };

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("https://ollama.com");

            check.Status = response.IsSuccessStatusCode ? DiagnosticStatus.Pass : DiagnosticStatus.Warning;
            check.Message = response.IsSuccessStatusCode ? "Internet accessible" : "Limited connectivity";
            check.Details = "Network access is optional for local-only operation";
        }
        catch
        {
            check.Status = DiagnosticStatus.Info;
            check.Message = "No internet access (offline mode)";
            check.Details = "This is fine for local-only operation";
        }

        report.Checks.Add(check);
    }

    private static DiagnosticStatus DetermineOverallStatus(DiagnosticsReport report)
    {
        if (report.Checks.Any(c => c.Status == DiagnosticStatus.Fail))
            return DiagnosticStatus.Fail;
        if (report.Checks.Any(c => c.Status == DiagnosticStatus.Warning))
            return DiagnosticStatus.Warning;
        return DiagnosticStatus.Pass;
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

    /// <summary>
    /// Generate a copy-pasteable diagnostics report.
    /// </summary>
    public string GenerateTextReport(DiagnosticsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== InControl Diagnostics Report ===");
        sb.AppendLine($"Timestamp: {report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Version: {report.AppVersion}");
        sb.AppendLine($"Overall: {report.OverallStatus}");
        sb.AppendLine();

        foreach (var check in report.Checks)
        {
            var statusIcon = check.Status switch
            {
                DiagnosticStatus.Pass => "[OK]",
                DiagnosticStatus.Warning => "[WARN]",
                DiagnosticStatus.Fail => "[FAIL]",
                _ => "[INFO]"
            };

            sb.AppendLine($"{statusIcon} {check.Name}: {check.Message}");
            if (!string.IsNullOrEmpty(check.Details))
            {
                sb.AppendLine($"    {check.Details}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Complete diagnostics report.
/// </summary>
public class DiagnosticsReport
{
    public DateTime Timestamp { get; set; }
    public string AppVersion { get; set; } = "";
    public DiagnosticStatus OverallStatus { get; set; }
    public List<DiagnosticCheck> Checks { get; set; } = new();
}

/// <summary>
/// Individual diagnostic check result.
/// </summary>
public class DiagnosticCheck
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public DiagnosticStatus Status { get; set; }
    public string Message { get; set; } = "";
    public string Details { get; set; } = "";
    public string? HelpLink { get; set; }
}

/// <summary>
/// Status of a diagnostic check.
/// </summary>
public enum DiagnosticStatus
{
    Pass,
    Warning,
    Fail,
    Info
}
