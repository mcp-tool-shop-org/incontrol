using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using InControl.ViewModels.Sessions;

namespace InControl.App.Controls;

/// <summary>
/// Left sidebar containing session list with new session button, search, and pinned items.
/// Supports keyboard navigation and context menu actions.
/// </summary>
public sealed partial class SessionSidebar : UserControl
{
    private SessionListViewModel? _viewModel;

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

    #endregion

    /// <summary>
    /// Binds the sidebar to a SessionListViewModel.
    /// </summary>
    public void SetViewModel(SessionListViewModel viewModel)
    {
        _viewModel = viewModel;

        // Bind the filtered sessions to the list
        SessionList.ItemsSource = viewModel.FilteredSessions;
        PinnedList.ItemsSource = viewModel.PinnedSessions;

        // Listen for collection changes to update empty state
        viewModel.FilteredSessions.CollectionChanged += (s, e) => RefreshVisualState();
        viewModel.PinnedSessions.CollectionChanged += (s, e) => RefreshVisualState();

        RefreshVisualState();
    }

    /// <summary>
    /// Refreshes the visual state (empty state, pinned section visibility).
    /// </summary>
    public void RefreshVisualState()
    {
        if (_viewModel is null) return;

        var hasSessions = _viewModel.HasSessions;
        EmptyState.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;
        SessionList.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
        PinnedSection.Visibility = _viewModel.HasPinnedSessions ? Visibility.Visible : Visibility.Collapsed;
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
                return;
            }
        }

        foreach (var session in _viewModel.PinnedSessions)
        {
            if (session.Id == id)
            {
                PinnedList.SelectedItem = session;
                _viewModel.SelectedSession = session;
                return;
            }
        }
    }

    private void SetupEventHandlers()
    {
        // New Session button
        NewSessionButton.Click += OnNewSessionClick;

        // Session search
        SessionSearch.TextChanged += OnSearchTextChanged;
        SessionSearch.QuerySubmitted += OnSearchQuerySubmitted;

        // Session list item click
        SessionList.ItemClick += OnSessionItemClick;
        PinnedList.ItemClick += OnSessionItemClick;

        // Right-click context menu (built in code since MenuFlyout in resources can't have x:Name)
        SessionList.RightTapped += OnSessionRightTapped;
        PinnedList.RightTapped += OnSessionRightTapped;
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
        if (sender is not ListView listView) return;

        // Find the item under the pointer
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
            SessionSelected?.Invoke(this, session.Id);
        }
    }

    private SessionItemViewModel? GetContextSession(object sender)
    {
        // Walk up from the MenuFlyoutItem to find the DataContext
        if (sender is MenuFlyoutItem menuItem &&
            menuItem.DataContext is SessionItemViewModel session)
        {
            return session;
        }

        // Fallback to selected item
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
