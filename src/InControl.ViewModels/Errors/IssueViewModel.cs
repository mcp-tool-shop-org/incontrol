using System.ComponentModel;

namespace InControl.ViewModels.Errors;

/// <summary>
/// ViewModel for displaying an issue (error) to the user.
/// Per UX contract: No blame language. State facts, provide context, offer actions.
/// </summary>
public sealed class IssueViewModel : INotifyPropertyChanged
{
    private readonly string _id;
    private string _title;
    private string _detail;
    private IssueSeverity _severity;
    private bool _isDismissed;

    public IssueViewModel(string title, string detail, IssueSeverity severity = IssueSeverity.Warning)
    {
        _id = Guid.NewGuid().ToString();
        _title = title;
        _detail = detail;
        _severity = severity;
        RecoveryActions = new List<RecoveryAction>();
        OccurredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Unique identifier for this issue.
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// Short title describing what happened.
    /// Per UX contract: State facts, not blame.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    /// <summary>
    /// Detailed explanation of the issue.
    /// Per UX contract: Provide context about why this happened.
    /// </summary>
    public string Detail
    {
        get => _detail;
        set
        {
            if (_detail != value)
            {
                _detail = value;
                OnPropertyChanged(nameof(Detail));
            }
        }
    }

    /// <summary>
    /// The severity level of this issue.
    /// </summary>
    public IssueSeverity Severity
    {
        get => _severity;
        set
        {
            if (_severity != value)
            {
                _severity = value;
                OnPropertyChanged(nameof(Severity));
                OnPropertyChanged(nameof(SeverityIcon));
            }
        }
    }

    /// <summary>
    /// Icon glyph for the severity level.
    /// </summary>
    public string SeverityIcon => _severity switch
    {
        IssueSeverity.Info => "\uE946",      // Info
        IssueSeverity.Warning => "\uE7BA",   // Warning
        IssueSeverity.Critical => "\uEA39",  // Error circle
        _ => "\uE946"
    };

    /// <summary>
    /// Available recovery actions.
    /// Per UX contract: Always offer what to do next.
    /// </summary>
    public List<RecoveryAction> RecoveryActions { get; }

    /// <summary>
    /// Whether this issue has recovery actions.
    /// </summary>
    public bool HasRecoveryActions => RecoveryActions.Count > 0;

    /// <summary>
    /// Suggestions for what to try.
    /// Per UX contract: Help users understand recovery options.
    /// </summary>
    public List<string> Suggestions { get; } = new();

    /// <summary>
    /// Whether this issue has suggestions.
    /// </summary>
    public bool HasSuggestions => Suggestions.Count > 0;

    /// <summary>
    /// When this issue occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Whether this issue has been dismissed.
    /// </summary>
    public bool IsDismissed
    {
        get => _isDismissed;
        set
        {
            if (_isDismissed != value)
            {
                _isDismissed = value;
                OnPropertyChanged(nameof(IsDismissed));
            }
        }
    }

    /// <summary>
    /// Adds a recovery action.
    /// </summary>
    public IssueViewModel WithAction(string label, Action action, bool isPrimary = false)
    {
        RecoveryActions.Add(new RecoveryAction(label, action, isPrimary));
        OnPropertyChanged(nameof(HasRecoveryActions));
        return this;
    }

    /// <summary>
    /// Adds a suggestion for what to try.
    /// </summary>
    public IssueViewModel WithSuggestion(string suggestion)
    {
        Suggestions.Add(suggestion);
        OnPropertyChanged(nameof(HasSuggestions));
        return this;
    }

    /// <summary>
    /// Adds multiple suggestions.
    /// </summary>
    public IssueViewModel WithSuggestions(params string[] suggestions)
    {
        foreach (var suggestion in suggestions)
        {
            Suggestions.Add(suggestion);
        }
        OnPropertyChanged(nameof(HasSuggestions));
        return this;
    }

    /// <summary>
    /// Dismisses this issue.
    /// </summary>
    public void Dismiss()
    {
        IsDismissed = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Factory methods for common issues

    /// <summary>
    /// Creates a connection unavailable issue.
    /// </summary>
    public static IssueViewModel ConnectionUnavailable(string endpoint)
    {
        return new IssueViewModel(
            "Connection unavailable",
            $"The inference backend at {endpoint} is not responding.",
            IssueSeverity.Critical)
            .WithSuggestions(
                "Check if the backend service is running",
                "Verify the endpoint address is correct",
                "Check your network connection")
            .WithAction("Retry connection", () => { }, isPrimary: true)
            .WithAction("Check backend status", () => { });
    }

    /// <summary>
    /// Creates a model not found issue.
    /// </summary>
    public static IssueViewModel ModelNotFound(string modelName)
    {
        return new IssueViewModel(
            "Model not found",
            $"\"{modelName}\" is not available on this device.",
            IssueSeverity.Warning)
            .WithSuggestions(
                "The model may need to be downloaded first",
                "Check if the model file was moved or renamed")
            .WithAction($"Pull model", () => { }, isPrimary: true)
            .WithAction("Select a different model", () => { });
    }

    /// <summary>
    /// Creates a context limit exceeded issue.
    /// </summary>
    public static IssueViewModel ContextLimitExceeded(int tokenCount, int maxTokens)
    {
        return new IssueViewModel(
            "Context limit exceeded",
            $"The input exceeds the model's context window ({maxTokens:N0} tokens). Current: {tokenCount:N0} tokens.",
            IssueSeverity.Warning)
            .WithSuggestions(
                "Remove some context attachments",
                "Shorten the conversation history",
                "Use a model with a larger context window")
            .WithAction("Reduce context size", () => { }, isPrimary: true)
            .WithAction("Start a new session", () => { });
    }

    /// <summary>
    /// Creates an out of memory issue.
    /// </summary>
    public static IssueViewModel OutOfMemory(long requiredBytes, long availableBytes)
    {
        var required = FormatBytes(requiredBytes);
        var available = FormatBytes(availableBytes);
        return new IssueViewModel(
            "Insufficient GPU memory",
            $"The model requires {required} but only {available} is available.",
            IssueSeverity.Critical)
            .WithSuggestions(
                "Close other GPU-intensive applications",
                "Use a quantized (smaller) version of the model",
                "Select a model with fewer parameters")
            .WithAction("Select a smaller model", () => { }, isPrimary: true)
            .WithAction("Close other applications", () => { });
    }

    /// <summary>
    /// Creates an execution interrupted issue.
    /// </summary>
    public static IssueViewModel ExecutionInterrupted(string reason)
    {
        return new IssueViewModel(
            "Execution interrupted",
            reason,
            IssueSeverity.Warning)
            .WithSuggestion("This may be a temporary issue. Try running again.")
            .WithAction("Retry", () => { }, isPrimary: true);
    }

    /// <summary>
    /// Creates a generic operation failed issue.
    /// </summary>
    public static IssueViewModel OperationFailed(string operation, string reason)
    {
        return new IssueViewModel(
            $"{operation} did not complete",
            reason,
            IssueSeverity.Warning)
            .WithSuggestion("Check the logs for more details if the issue persists.");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

/// <summary>
/// Severity levels for issues.
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Informational, no action required.
    /// </summary>
    Info,

    /// <summary>
    /// Warning, may require attention.
    /// </summary>
    Warning,

    /// <summary>
    /// Critical issue, requires action.
    /// </summary>
    Critical
}

/// <summary>
/// A recovery action that can be taken for an issue.
/// </summary>
public sealed class RecoveryAction
{
    private readonly Action _action;

    public RecoveryAction(string label, Action action, bool isPrimary = false)
    {
        Label = label;
        _action = action;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// The display label for this action.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Whether this is the primary action.
    /// </summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// Executes this recovery action.
    /// </summary>
    public void Execute()
    {
        _action();
    }
}
