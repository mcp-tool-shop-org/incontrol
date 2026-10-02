using System.ComponentModel;

namespace InControl.ViewModels.Onboarding;

/// <summary>
/// ViewModel for re-entry experience when returning to the app.
/// Provides context about previous state and quick-start options.
/// </summary>
public sealed class ReentryViewModel : INotifyPropertyChanged
{
    private string _lastSessionTitle = string.Empty;
    private DateTimeOffset? _lastSessionTime;
    private string _lastUsedModel = string.Empty;
    private int _totalSessions;
    private bool _hasRecentSession;
    private ReentryAction _suggestedAction = ReentryAction.NewSession;

    /// <summary>
    /// Title of the last session.
    /// </summary>
    public string LastSessionTitle
    {
        get => _lastSessionTitle;
        set
        {
            if (_lastSessionTitle != value)
            {
                _lastSessionTitle = value;
                OnPropertyChanged(nameof(LastSessionTitle));
                OnPropertyChanged(nameof(HasLastSession));
            }
        }
    }

    /// <summary>
    /// Whether there is a last session to resume.
    /// </summary>
    public bool HasLastSession => !string.IsNullOrEmpty(_lastSessionTitle);

    /// <summary>
    /// Time of the last session.
    /// </summary>
    public DateTimeOffset? LastSessionTime
    {
        get => _lastSessionTime;
        set
        {
            if (_lastSessionTime != value)
            {
                _lastSessionTime = value;
                OnPropertyChanged(nameof(LastSessionTime));
                OnPropertyChanged(nameof(LastSessionTimeText));
            }
        }
    }

    /// <summary>
    /// Formatted text for last session time.
    /// </summary>
    public string LastSessionTimeText
    {
        get
        {
            if (!_lastSessionTime.HasValue)
                return string.Empty;

            var elapsed = DateTimeOffset.UtcNow - _lastSessionTime.Value;

            if (elapsed.TotalMinutes < 1)
                return "Just now";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed.TotalDays < 7)
                return $"{(int)elapsed.TotalDays}d ago";

            return _lastSessionTime.Value.ToString("MMM d");
        }
    }

    /// <summary>
    /// The last model used.
    /// </summary>
    public string LastUsedModel
    {
        get => _lastUsedModel;
        set
        {
            if (_lastUsedModel != value)
            {
                _lastUsedModel = value;
                OnPropertyChanged(nameof(LastUsedModel));
                OnPropertyChanged(nameof(HasLastUsedModel));
            }
        }
    }

    /// <summary>
    /// Whether there is a last used model.
    /// </summary>
    public bool HasLastUsedModel => !string.IsNullOrEmpty(_lastUsedModel);

    /// <summary>
    /// Total number of sessions.
    /// </summary>
    public int TotalSessions
    {
        get => _totalSessions;
        set
        {
            if (_totalSessions != value)
            {
                _totalSessions = value;
                OnPropertyChanged(nameof(TotalSessions));
                OnPropertyChanged(nameof(TotalSessionsText));
            }
        }
    }

    /// <summary>
    /// Formatted text for total sessions.
    /// </summary>
    public string TotalSessionsText => _totalSessions == 1
        ? "1 session"
        : $"{_totalSessions} sessions";

    /// <summary>
    /// Whether there is a recent session (within last hour).
    /// </summary>
    public bool HasRecentSession
    {
        get => _hasRecentSession;
        set
        {
            if (_hasRecentSession != value)
            {
                _hasRecentSession = value;
                OnPropertyChanged(nameof(HasRecentSession));
            }
        }
    }

    /// <summary>
    /// The suggested action for re-entry.
    /// </summary>
    public ReentryAction SuggestedAction
    {
        get => _suggestedAction;
        set
        {
            if (_suggestedAction != value)
            {
                _suggestedAction = value;
                OnPropertyChanged(nameof(SuggestedAction));
                OnPropertyChanged(nameof(SuggestedActionText));
            }
        }
    }

    /// <summary>
    /// Text describing the suggested action.
    /// </summary>
    public string SuggestedActionText => _suggestedAction switch
    {
        ReentryAction.ContinueSession => $"Continue \"{_lastSessionTitle}\"",
        ReentryAction.NewSession => "Start a new session",
        ReentryAction.BrowseSessions => "Browse your sessions",
        _ => string.Empty
    };

    /// <summary>
    /// Welcome message based on time of day.
    /// </summary>
    public string WelcomeGreeting
    {
        get
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                < 12 => "Good morning",
                < 17 => "Good afternoon",
                _ => "Good evening"
            };
        }
    }

    /// <summary>
    /// Updates the re-entry state from session history.
    /// </summary>
    public void UpdateFromHistory(
        string? lastSessionTitle,
        DateTimeOffset? lastSessionTime,
        string? lastUsedModel,
        int totalSessions)
    {
        LastSessionTitle = lastSessionTitle ?? string.Empty;
        LastSessionTime = lastSessionTime;
        LastUsedModel = lastUsedModel ?? string.Empty;
        TotalSessions = totalSessions;

        // Determine if there's a recent session
        HasRecentSession = lastSessionTime.HasValue &&
            (DateTimeOffset.UtcNow - lastSessionTime.Value).TotalHours < 1;

        // Suggest action based on state
        if (HasRecentSession && HasLastSession)
        {
            SuggestedAction = ReentryAction.ContinueSession;
        }
        else if (totalSessions > 5)
        {
            SuggestedAction = ReentryAction.BrowseSessions;
        }
        else
        {
            SuggestedAction = ReentryAction.NewSession;
        }
    }

    /// <summary>
    /// Resets the re-entry state.
    /// </summary>
    public void Reset()
    {
        LastSessionTitle = string.Empty;
        LastSessionTime = null;
        LastUsedModel = string.Empty;
        TotalSessions = 0;
        HasRecentSession = false;
        SuggestedAction = ReentryAction.NewSession;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Suggested actions for re-entry.
/// </summary>
public enum ReentryAction
{
    /// <summary>
    /// Continue the last session.
    /// </summary>
    ContinueSession,

    /// <summary>
    /// Start a new session.
    /// </summary>
    NewSession,

    /// <summary>
    /// Browse existing sessions.
    /// </summary>
    BrowseSessions
}
