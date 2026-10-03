using System.Collections.ObjectModel;
using System.ComponentModel;
using InControl.Core.Models;

namespace InControl.ViewModels.Sessions;

/// <summary>
/// ViewModel for the session sidebar list.
/// Manages projects, the sessions in the selected project, search, pinning, and selection.
/// </summary>
public sealed class SessionListViewModel : INotifyPropertyChanged
{
    private string _searchQuery = string.Empty;
    private SessionItemViewModel? _selectedSession;
    private bool _isLoading;
    private Guid _selectedProjectId = ChatProject.GeneralId;

    public SessionListViewModel()
    {
        Sessions = new ObservableCollection<SessionItemViewModel>();
        PinnedSessions = new ObservableCollection<SessionItemViewModel>();
        FilteredSessions = new ObservableCollection<SessionItemViewModel>();
        VisiblePinned = new ObservableCollection<SessionItemViewModel>();
        Projects = new ObservableCollection<ProjectItemViewModel>();
        Projects.Add(new ProjectItemViewModel(ChatProject.General()) { IsSelected = true });
    }

    /// <summary>
    /// All sessions (unpinned), across projects.
    /// </summary>
    public ObservableCollection<SessionItemViewModel> Sessions { get; }

    /// <summary>
    /// Pinned sessions, across projects.
    /// </summary>
    public ObservableCollection<SessionItemViewModel> PinnedSessions { get; }

    /// <summary>
    /// Unpinned sessions in the selected project that match the search.
    /// </summary>
    public ObservableCollection<SessionItemViewModel> FilteredSessions { get; }

    /// <summary>
    /// Pinned sessions in the selected project.
    /// </summary>
    public ObservableCollection<SessionItemViewModel> VisiblePinned { get; }

    /// <summary>
    /// Projects shown above the session list.
    /// </summary>
    public ObservableCollection<ProjectItemViewModel> Projects { get; }

    /// <summary>
    /// Whether there are any sessions in any project.
    /// </summary>
    public bool HasSessions => Sessions.Count > 0 || PinnedSessions.Count > 0;

    /// <summary>
    /// Whether the selected project has any visible sessions.
    /// </summary>
    public bool HasVisibleSessions => FilteredSessions.Count > 0 || VisiblePinned.Count > 0;

    /// <summary>
    /// Whether there are pinned sessions in any project.
    /// </summary>
    public bool HasPinnedSessions => PinnedSessions.Count > 0;

    /// <summary>
    /// Whether the selected project has pinned sessions.
    /// </summary>
    public bool HasVisiblePinned => VisiblePinned.Count > 0;

    /// <summary>
    /// The project whose sessions are listed.
    /// </summary>
    public Guid SelectedProjectId => _selectedProjectId;

    /// <summary>
    /// The current search query.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
                return;

            _searchQuery = value;
            OnPropertyChanged(nameof(SearchQuery));
            ApplyFilter();
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
            if (_selectedSession == value)
                return;

            if (_selectedSession != null)
                _selectedSession.IsSelected = false;

            _selectedSession = value;

            if (_selectedSession != null)
                _selectedSession.IsSelected = true;

            OnPropertyChanged(nameof(SelectedSession));
            OnPropertyChanged(nameof(HasSelectedSession));
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
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    /// <summary>
    /// Replaces the project list. General is added if the source omitted it.
    /// </summary>
    public void SetProjects(IEnumerable<ChatProject> projects)
    {
        Projects.Clear();
        var list = projects.ToList();
        if (list.All(project => project.Id != ChatProject.GeneralId))
            list.Insert(0, ChatProject.General());

        foreach (var project in list)
            Projects.Add(new ProjectItemViewModel(project));

        if (Projects.All(project => project.Id != _selectedProjectId))
            _selectedProjectId = ChatProject.GeneralId;

        MarkSelectedProject();
        ApplyFilter();
    }

    /// <summary>
    /// Adds a project row.
    /// </summary>
    public void AddProject(ChatProject project)
    {
        Projects.Add(new ProjectItemViewModel(project));
    }

    /// <summary>
    /// Shows the sessions that belong to this project.
    /// </summary>
    public void SelectProject(Guid id)
    {
        if (Projects.All(project => project.Id != id))
            return;

        _selectedProjectId = id;
        MarkSelectedProject();
        OnPropertyChanged(nameof(SelectedProjectId));
        ApplyFilter();
    }

    /// <summary>
    /// Creates a new session in the selected project and adds it to the list.
    /// </summary>
    public SessionItemViewModel CreateSession()
    {
        var conversation = Conversation.Create(projectId: _selectedProjectId);
        var viewModel = new SessionItemViewModel(conversation);

        Sessions.Insert(0, viewModel);
        SelectedSession = viewModel;

        OnPropertyChanged(nameof(HasSessions));
        ApplyFilter();
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
        }

        if (SelectedSession == session)
            SelectedSession = null;

        OnPropertyChanged(nameof(HasSessions));
        ApplyFilter();
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
            session.IsPinned = true;
            PinnedSessions.Add(session);
        }

        OnPropertyChanged(nameof(HasPinnedSessions));
        ApplyFilter();
    }

    /// <summary>
    /// Duplicates a session into the same project.
    /// </summary>
    public SessionItemViewModel DuplicateSession(SessionItemViewModel source)
    {
        var original = source.GetConversation();
        var duplicate = Conversation.Create(
            original.Title + " (copy)",
            projectId: original.ProjectId);

        foreach (var message in original.Messages)
            duplicate = duplicate.WithMessage(message);

        var viewModel = new SessionItemViewModel(duplicate);
        Sessions.Insert(0, viewModel);

        OnPropertyChanged(nameof(HasSessions));
        ApplyFilter();

        return viewModel;
    }

    /// <summary>
    /// Finds a session in either the pinned or recent list.
    /// </summary>
    public SessionItemViewModel? FindSession(Guid id)
    {
        foreach (var session in Sessions)
        {
            if (session.Id == id)
                return session;
        }

        foreach (var session in PinnedSessions)
        {
            if (session.Id == id)
                return session;
        }

        return null;
    }

    /// <summary>
    /// Clears the search query and shows the selected project's sessions.
    /// </summary>
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    /// <summary>
    /// Rebuilds the visible session lists for the selected project and search text.
    /// </summary>
    public void ApplyFilter()
    {
        FilteredSessions.Clear();
        VisiblePinned.Clear();

        var query = SearchQuery.Trim();

        foreach (var session in PinnedSessions)
        {
            if (MatchesProject(session))
                VisiblePinned.Add(session);
        }

        foreach (var session in Sessions)
        {
            if (!MatchesProject(session))
                continue;

            if (string.IsNullOrEmpty(query) ||
                session.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSessions.Add(session);
            }
        }

        OnPropertyChanged(nameof(FilteredSessions));
        OnPropertyChanged(nameof(HasVisibleSessions));
        OnPropertyChanged(nameof(HasVisiblePinned));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool MatchesProject(SessionItemViewModel session)
    {
        return session.EffectiveProjectId == _selectedProjectId;
    }

    private void MarkSelectedProject()
    {
        foreach (var project in Projects)
            project.IsSelected = project.Id == _selectedProjectId;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
