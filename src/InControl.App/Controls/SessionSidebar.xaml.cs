using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using InControl.ViewModels.Sessions;

namespace InControl.App.Controls;

/// <summary>
/// Left sidebar: projects, the sessions in the selected project, and notes for that project.
/// </summary>
public sealed partial class SessionSidebar : UserControl
{
    private SessionListViewModel? _viewModel;
    private bool _suppressProjectEvent;
    private bool _settingInstructions;
    private bool _instructionsDirty;
    private Guid _instructionsProjectId;

    public SessionSidebar()
    {
        this.InitializeComponent();
        SetupEventHandlers();
        SetupKeyboardNavigation();
    }

    #region Events

    /// <summary>
    /// Event raised when a new session is requested.
    /// </summary>
    public event EventHandler? NewSessionRequested;

    /// <summary>
    /// Event raised when a new project name has been entered.
    /// </summary>
    public event EventHandler<string>? NewProjectRequested;

    /// <summary>
    /// Event raised when a project is selected.
    /// </summary>
    public event EventHandler<Guid>? ProjectSelected;

    /// <summary>
    /// Event raised when a session is selected by ID.
    /// </summary>
    public event EventHandler<Guid>? SessionSelected;

    /// <summary>
    /// Event raised when session search text changes.
    /// </summary>
    public event EventHandler<string>? SearchTextChanged;

    /// <summary>
    /// Event raised when a session is renamed.
    /// </summary>
    public event EventHandler<(Guid Id, string NewTitle)>? SessionRenamed;

    /// <summary>
    /// Event raised when a session should be deleted.
    /// </summary>
    public event EventHandler<Guid>? SessionDeleteRequested;

    /// <summary>
    /// Event raised when a session should be exported.
    /// </summary>
    public event EventHandler<Guid>? SessionExportRequested;

    /// <summary>
    /// Event raised when the user asks to remember text for the whole project.
    /// </summary>
    public event EventHandler<string>? RememberForProjectRequested;

    /// <summary>
    /// Event raised when the user asks to remember text for the open session only.
    /// </summary>
    public event EventHandler<string>? RememberForSessionRequested;

    /// <summary>
    /// Event raised when the user asks to forget one note.
    /// </summary>
    public event EventHandler<Guid>? ForgetMemoryRequested;

    /// <summary>
    /// Event raised when project instructions lose focus after an edit.
    /// </summary>
    public event EventHandler<string>? InstructionsChanged;

    #endregion

    /// <summary>
    /// Binds the sidebar to a SessionListViewModel.
    /// </summary>
    public void SetViewModel(SessionListViewModel viewModel)
    {
        _viewModel = viewModel;

        SessionList.ItemsSource = viewModel.FilteredSessions;
        PinnedList.ItemsSource = viewModel.VisiblePinned;
        ProjectList.ItemsSource = viewModel.Projects;

        viewModel.FilteredSessions.CollectionChanged += (s, e) => RefreshVisualState();
        viewModel.VisiblePinned.CollectionChanged += (s, e) => RefreshVisualState();

        RefreshVisualState();
        SelectProject(viewModel.SelectedProjectId);
    }

    /// <summary>
    /// Refreshes the visual state (empty state, pinned section visibility).
    /// </summary>
    public void RefreshVisualState()
    {
        if (_viewModel is null) return;

        var hasSessions = _viewModel.HasVisibleSessions;
        EmptyState.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;
        SessionList.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
        PinnedSection.Visibility = _viewModel.HasVisiblePinned ? Visibility.Visible : Visibility.Collapsed;
        RememberSessionButton.IsEnabled = _viewModel.HasSelectedSession;
    }

    /// <summary>
    /// Selects a session in the list by its ID.
    /// </summary>
    public void SelectSession(Guid id)
    {
        if (_viewModel is null) return;

        foreach (var session in _viewModel.FilteredSessions)
        {
            if (session.Id == id)
            {
                SessionList.SelectedItem = session;
                _viewModel.SelectedSession = session;
                RememberSessionButton.IsEnabled = true;
                return;
            }
        }

        foreach (var session in _viewModel.VisiblePinned)
        {
            if (session.Id == id)
            {
                PinnedList.SelectedItem = session;
                _viewModel.SelectedSession = session;
                RememberSessionButton.IsEnabled = true;
                return;
            }
        }
    }

    /// <summary>
    /// Clears the highlighted session. The project stays selected.
    /// </summary>
    public void ClearSessionSelection()
    {
        SessionList.SelectedItem = null;
        PinnedList.SelectedItem = null;
        if (_viewModel is not null)
            _viewModel.SelectedSession = null;
        RememberSessionButton.IsEnabled = false;
    }

    /// <summary>
    /// Highlights a project and filters the session list to it.
    /// </summary>
    public void SelectProject(Guid id)
    {
        if (_viewModel is null) return;

        _suppressProjectEvent = true;
        _viewModel.SelectProject(id);
        foreach (var project in _viewModel.Projects)
        {
            if (project.Id == id)
            {
                ProjectList.SelectedItem = project;
                break;
            }
        }

        _suppressProjectEvent = false;
        RefreshVisualState();
    }

    /// <summary>
    /// Shows standing instructions. Does not overwrite text the user is still editing.
    /// </summary>
    public void ShowInstructions(Guid projectId, string? text)
    {
        if (projectId == _instructionsProjectId && _instructionsDirty)
            return;

        _instructionsProjectId = projectId;
        _instructionsDirty = false;
        _settingInstructions = true;
        InstructionsBox.Text = text ?? string.Empty;
        _settingInstructions = false;
    }

    /// <summary>
    /// Shows the notes for the selected project and session.
    /// </summary>
    public void ShowMemory(IReadOnlyList<MemoryNoteItem> notes)
    {
        MemoryList.ItemsSource = notes;
        ForgetMemoryButton.IsEnabled = false;
    }

    /// <summary>
    /// Enables the session-note button only when a session is open.
    /// </summary>
    public void SetRememberSessionEnabled(bool enabled)
    {
        RememberSessionButton.IsEnabled = enabled;
    }

    private void SetupEventHandlers()
    {
        NewSessionButton.Click += OnNewSessionClick;
        NewProjectButton.Click += OnNewProjectClick;

        SessionSearch.TextChanged += OnSearchTextChanged;
        SessionSearch.QuerySubmitted += OnSearchQuerySubmitted;

        SessionList.ItemClick += OnSessionItemClick;
        PinnedList.ItemClick += OnSessionItemClick;
        ProjectList.ItemClick += OnProjectItemClick;

        SessionList.RightTapped += OnSessionRightTapped;
        PinnedList.RightTapped += OnSessionRightTapped;

        InstructionsBox.TextChanged += OnInstructionsTextChanged;
        InstructionsBox.LostFocus += OnInstructionsLostFocus;
        RememberProjectButton.Click += OnRememberProjectClick;
        RememberSessionButton.Click += OnRememberSessionClick;
        ForgetMemoryButton.Click += OnForgetMemoryClick;
        MemoryList.SelectionChanged += OnMemorySelectionChanged;
    }

    private void OnProjectItemClick(object sender, ItemClickEventArgs e)
    {
        if (_suppressProjectEvent || _viewModel is null)
            return;

        if (e.ClickedItem is not ProjectItemViewModel project)
            return;

        _viewModel.SelectProject(project.Id);
        ProjectSelected?.Invoke(this, project.Id);
        RefreshVisualState();
    }

    private async void OnNewProjectClick(object sender, RoutedEventArgs e)
    {
        var inputBox = new TextBox
        {
            PlaceholderText = "Project name"
        };

        var dialog = new ContentDialog
        {
            Title = "New project",
            Content = inputBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
            NewProjectRequested?.Invoke(this, inputBox.Text.Trim());
    }

    private void OnInstructionsTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_settingInstructions)
            return;

        _instructionsDirty = true;
    }

    private void OnInstructionsLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_instructionsDirty)
            return;

        _instructionsDirty = false;
        InstructionsChanged?.Invoke(this, InstructionsBox.Text);
    }

    private async void OnRememberProjectClick(object sender, RoutedEventArgs e)
    {
        var text = await AskNoteAsync("Remember for this project", "Saved for every session in this project.");
        if (text is not null)
            RememberForProjectRequested?.Invoke(this, text);
    }

    private async void OnRememberSessionClick(object sender, RoutedEventArgs e)
    {
        var text = await AskNoteAsync("Remember for this session", "Only this session will see it.");
        if (text is not null)
            RememberForSessionRequested?.Invoke(this, text);
    }

    private async Task<string?> AskNoteAsync(string title, string hint)
    {
        var inputBox = new TextBox
        {
            PlaceholderText = hint,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = inputBox,
            PrimaryButtonText = "Remember",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return null;

        var text = inputBox.Text.Trim();
        return text.Length == 0 ? null : text;
    }

    private void OnMemorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ForgetMemoryButton.IsEnabled = MemoryList.SelectedItem is MemoryNoteItem;
    }

    private async void OnForgetMemoryClick(object sender, RoutedEventArgs e)
    {
        if (MemoryList.SelectedItem is not MemoryNoteItem note)
            return;

        var dialog = new ContentDialog
        {
            Title = "Forget this note",
            Content = $"Forget \"{note.Title}\"?",
            PrimaryButtonText = "Forget",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            ForgetMemoryRequested?.Invoke(this, note.Id);
    }

    private MenuFlyout CreateContextMenu(SessionItemViewModel session)
    {
        var menu = new MenuFlyout();

        var rename = new MenuFlyoutItem { Text = "Rename", Icon = new SymbolIcon(Symbol.Rename) };
        rename.Click += OnRenameClick;
        rename.DataContext = session;
        menu.Items.Add(rename);

        var duplicate = new MenuFlyoutItem { Text = "Duplicate", Icon = new SymbolIcon(Symbol.Copy) };
        duplicate.Click += OnDuplicateClick;
        duplicate.DataContext = session;
        menu.Items.Add(duplicate);

        menu.Items.Add(new MenuFlyoutSeparator());

        var pin = new MenuFlyoutItem
        {
            Text = session.IsPinned ? "Unpin" : "Pin",
            Icon = new SymbolIcon(Symbol.Pin)
        };
        pin.Click += OnPinClick;
        pin.DataContext = session;
        menu.Items.Add(pin);

        var export = new MenuFlyoutItem { Text = "Export", Icon = new SymbolIcon(Symbol.Share) };
        export.Click += OnExportClick;
        export.DataContext = session;
        menu.Items.Add(export);

        menu.Items.Add(new MenuFlyoutSeparator());

        var delete = new MenuFlyoutItem { Text = "Delete", Icon = new SymbolIcon(Symbol.Delete) };
        delete.Click += OnDeleteClick;
        delete.DataContext = session;
        menu.Items.Add(delete);

        return menu;
    }

    private void OnSessionRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not ListView) return;

        var element = e.OriginalSource as FrameworkElement;
        while (element != null && element.DataContext is not SessionItemViewModel)
        {
            element = element.Parent as FrameworkElement;
        }

        if (element?.DataContext is SessionItemViewModel session)
        {
            if (_viewModel is not null)
                _viewModel.SelectedSession = session;

            var menu = CreateContextMenu(session);
            menu.ShowAt(element, e.GetPosition(element));
        }
    }

    private void OnNewSessionClick(object sender, RoutedEventArgs e)
    {
        NewSessionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            if (_viewModel is not null)
            {
                _viewModel.SearchQuery = sender.Text;
            }
            SearchTextChanged?.Invoke(this, sender.Text);
        }
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // Search is handled through text changed event
    }

    private void OnSessionItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SessionItemViewModel session)
        {
            if (_viewModel is not null)
                _viewModel.SelectedSession = session;
            RememberSessionButton.IsEnabled = true;
            SessionSelected?.Invoke(this, session.Id);
        }
    }

    private SessionItemViewModel? GetContextSession(object sender)
    {
        if (sender is MenuFlyoutItem menuItem &&
            menuItem.DataContext is SessionItemViewModel session)
        {
            return session;
        }

        return _viewModel?.SelectedSession;
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        var session = GetContextSession(sender);
        if (session is null || _viewModel is null) return;

        var inputBox = new TextBox
        {
            Text = session.Title,
            PlaceholderText = "Session name",
            SelectionStart = 0,
            SelectionLength = session.Title.Length
        };

        var dialog = new ContentDialog
        {
            Title = "Rename Session",
            Content = inputBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
        {
            SessionRenamed?.Invoke(this, (session.Id, inputBox.Text.Trim()));
        }
    }

    private void OnDuplicateClick(object sender, RoutedEventArgs e)
    {
        var session = GetContextSession(sender);
        if (session is null || _viewModel is null) return;

        _viewModel.DuplicateSession(session);
        RefreshVisualState();
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        var session = GetContextSession(sender);
        if (session is null || _viewModel is null) return;

        _viewModel.TogglePin(session);
        RefreshVisualState();
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var session = GetContextSession(sender);
        if (session is null) return;

        SessionExportRequested?.Invoke(this, session.Id);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var session = GetContextSession(sender);
        if (session is null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Session",
            Content = $"Are you sure you want to delete \"{session.Title}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            SessionDeleteRequested?.Invoke(this, session.Id);
        }
    }

    private void SetupKeyboardNavigation()
    {
        SessionList.KeyDown += OnSessionListKeyDown;
    }

    private void OnSessionListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (SessionList.SelectedItem is not SessionItemViewModel session)
            return;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                SessionSelected?.Invoke(this, session.Id);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.F2:
                OnRenameClick(sender, e);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Delete:
                OnDeleteClick(sender, e);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Updates the visibility of empty state vs session list.
    /// </summary>
    public void UpdateEmptyState(bool hasItems)
    {
        EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        SessionList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the visibility of the pinned section.
    /// </summary>
    public void UpdatePinnedSection(bool hasPinnedItems)
    {
        PinnedSection.Visibility = hasPinnedItems ? Visibility.Visible : Visibility.Collapsed;
    }
}
