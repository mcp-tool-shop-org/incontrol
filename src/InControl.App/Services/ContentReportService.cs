using System.Diagnostics;
using System.Text.Json;

namespace InControl.App.Services;

/// <summary>
/// Service for reporting inappropriate AI-generated content.
/// Stores reports locally for review and compliance with Store requirements.
/// </summary>
public sealed class ContentReportService
{
    private const string ReportsFileName = "content-reports.json";

    private static ContentReportService? _instance;
    private static readonly object _lock = new();

    private readonly string _dataPath;
    private readonly List<ContentReport> _reports = new();

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static ContentReportService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ContentReportService();
                }
            }
            return _instance;
        }
    }

    private ContentReportService()
    {
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InControl", "Reports");

        try
        {
            Directory.CreateDirectory(_dataPath);
            LoadReports();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize report service: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves a content report.
    /// </summary>
    public void SaveReport(ContentReport report)
    {
        try
        {
            _reports.Add(report);
            PersistReports();
            Debug.WriteLine($"Content report saved: {report.Reason}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save report: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all reports.
    /// </summary>
    public IReadOnlyList<ContentReport> GetReports() => _reports.AsReadOnly();

    private void LoadReports()
    {
        var filePath = Path.Combine(_dataPath, ReportsFileName);
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var reports = JsonSerializer.Deserialize<List<ContentReport>>(json);
                if (reports != null)
                {
                    _reports.AddRange(reports);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load reports: {ex.Message}");
            }
        }
    }

    private void PersistReports()
    {
        var filePath = Path.Combine(_dataPath, ReportsFileName);
        try
        {
            var json = JsonSerializer.Serialize(_reports, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to persist reports: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents a report of inappropriate AI-generated content.
/// </summary>
public class ContentReport
{
    /// <summary>
    /// Unique identifier for this report.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The ID of the message being reported.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// The content of the message (for context).
    /// </summary>
    public string MessageContent { get; set; } = string.Empty;

    /// <summary>
    /// The model that generated the content.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// The reason for the report.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Additional details provided by the user.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When the report was submitted.
    /// </summary>
    public DateTimeOffset ReportedAt { get; set; }
}
