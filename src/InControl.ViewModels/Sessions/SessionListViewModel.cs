using System.Collections.ObjectModel;
using System.ComponentModel;
using InControl.Core.Models;
using InControl.Core.UX;

namespace InControl.ViewModels.Sessions;

/// <summary>
/// ViewModel for the session sidebar list.
/// Manages session collection, search, pinning, and selection.
/// </summary>
public sealed class SessionListViewModel : INotifyPropertyChanged
{
    private string _searchQuery = string.Empty;
    private SessionItemViewModel? _selectedSession;
    private bool _isLoading;

    public SessionListViewModel()
    {
        Sessions = new ObservableCollection<SessionItemViewModel>();
        PinnedSessions = new ObservableCollection<SessionItemViewModel>();
        FilteredSessions = new ObservableCollection<SessionItemViewModel>();
    }

    /// <summary>
    /// All sessions (unpinned).
    /// </summary>
    public ObservableCollection<SessionItemViewModel> Sessions { get; }

    /// <summary>
    /// Pinned sessions shown at the top.
    /// </summary>
    public ObservableCollection<SessionItemViewModel> PinnedSessions { get; }

    /// <summary>
    /// Sessions filtered by search query.
    /// </summary>
    public ObservableCollection<SessionItemViewModel> FilteredSessions { get; }

    /// <summary>
    /// Whether there are any sessions.
    /// </summary>
    public bool HasSessions => Sessions.Count > 0 || PinnedSessions.Count > 0;

    /// <summary>
    /// Whether there are pinned sessions.
    /// </summary>
    public bool HasPinnedSessions => PinnedSessions.Count > 0;

    /// <summary>
    /// The current search query.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// The currently selected session.
    /// </summary>
    public SessionItemViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (_selectedSession != value)
            {
                if (_selectedSession != null)
                    _selectedSession.IsSelected = false;

                _selectedSession = value;

                if (_selectedSession != null)
                    _selectedSession.IsSelected = true;

                OnPropertyChanged(nameof(SelectedSession));
                OnPropertyChanged(nameof(HasSelectedSession));
            }
        }
    }

    /// <summary>
    /// Whether a session is currently selected.
    /// </summary>
    public bool HasSelectedSession => SelectedSession != null;

    /// <summary>
    /// Whether sessions are being loaded.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }
    }

    /// <summary>
    /// Creates a new session and adds it to the list.
    /// </summary>
    public SessionItemViewModel CreateSession()
    {
        var conversation = Conversation.Create();
        var viewModel = new SessionItemViewModel(conversation);

        Sessions.Insert(0, viewModel);
        SelectedSession = viewModel;

        OnPropertyChanged(nameof(HasSessions));
        return viewModel;
    }

    /// <summary>
    /// Adds an existing conversation to the list.
    /// </summary>
    public void AddSession(Conversation conversation, bool isPinned = false)
    {
        var viewModel = new SessionItemViewModel(conversation) { IsPinned = isPinned };

        if (isPinned)
        {
            PinnedSessions.Add(viewModel);
            OnPropertyChanged(nameof(HasPinnedSessions));
        }
        else
        {
            Sessions.Add(viewModel);
        }

        OnPropertyChanged(nameof(HasSessions));
        ApplyFilter();
    }

    /// <summary>
    /// Removes a session from the list.
    /// </summary>
    public void RemoveSession(SessionItemViewModel session)
    {
        if (session.IsPinned)
        {
            PinnedSessions.Remove(session);
            OnPropertyChanged(nameof(HasPinnedSessions));
        }
        else
        {
            Sessions.Remove(session);
            FilteredSessions.Remove(session);
        }

        if (SelectedSession == session)
            SelectedSession = null;

        OnPropertyChanged(nameof(HasSessions));
    }

    /// <summary>
    /// Pins or unpins a session.
    /// </summary>
    public void TogglePin(SessionItemViewModel session)
    {
        if (session.IsPinned)
        {
            PinnedSessions.Remove(session);
            session.IsPinned = false;
            Sessions.Insert(0, session);
        }
        else
        {
            Sessions.Remove(session);
            FilteredSessions.Remove(session);
            session.IsPinned = true;
            PinnedSessions.Add(session);
        }

        OnPropertyChanged(nameof(HasPinnedSessions));
        ApplyFilter();
    }

    /// <summary>
    /// Duplicates a session.
    /// </summary>
    public SessionItemViewModel DuplicateSession(SessionItemViewModel source)
    {
        var original = source.GetConversation();
        var duplicate = Conversation.Create(original.Title + " (copy)");

        // Copy messages
        foreach (var message in original.Messages)
        {
            duplicate = duplicate.WithMessage(message);
        }

        var viewModel = new SessionItemViewModel(duplicate);
        Sessions.Insert(0, viewModel);

        OnPropertyChanged(nameof(HasSessions));
        ApplyFilter();

        return viewModel;
    }

    /// <summary>
    /// Clears the search query and shows all sessions.
    /// </summary>
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    public void ApplyFilter()
    {
        FilteredSessions.Clear();

        var query = SearchQuery.Trim();
        var source = string.IsNullOrEmpty(query) ? Sessions : Sessions;

        foreach (var session in source)
        {
            if (string.IsNullOrEmpty(query) ||
                session.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSessions.Add(session);
            }
        }

        OnPropertyChanged(nameof(FilteredSessions));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
