using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;

namespace InControl.App.Controls;

/// <summary>
/// Command Palette (Ctrl+K) - power user command-driven control.
/// Provides fuzzy search across all major actions.
/// </summary>
public sealed partial class CommandPalette : UserControl
{
    private readonly ObservableCollection<CommandItem> _allCommands = new();
    private readonly ObservableCollection<CommandItem> _filteredCommands = new();

    public CommandPalette()
    {
        this.InitializeComponent();
        InitializeCommands();
        CommandsList.ItemsSource = _filteredCommands;
        SetupEventHandlers();
    }

    /// <summary>
    /// Event raised when a command is executed.
    /// </summary>
    public event EventHandler<string>? CommandExecuted;

    /// <summary>
    /// Event raised when palette should close.
    /// </summary>
    public event EventHandler? CloseRequested;

    private void InitializeCommands()
    {
        _allCommands.Add(new CommandItem("new-session", "New Session", "Create a new conversation", "\uE710", "Ctrl+N"));
        _allCommands.Add(new CommandItem("open-settings", "Open Settings", "Configure InControl preferences", "\uE713", "Ctrl+,"));
        _allCommands.Add(new CommandItem("open-model-manager", "Open Model Manager", "Manage AI models", "\uE950", ""));
        _allCommands.Add(new CommandItem("toggle-offline", "Toggle Offline Mode", "Enable or disable network connections", "\uE8CD", ""));
        _allCommands.Add(new CommandItem("open-extensions", "Open Extensions", "View and manage extensions", "\uEA86", ""));
        _allCommands.Add(new CommandItem("open-assistant", "Open Assistant", "Configure assistant behavior", "\uE99A", ""));
        _allCommands.Add(new CommandItem("view-policy", "View Policy", "View security policies", "\uE72E", ""));
        _allCommands.Add(new CommandItem("open-connectivity", "Open Connectivity", "Manage network settings", "\uE701", ""));
        _allCommands.Add(new CommandItem("open-help", "Open Help", "Get help using InControl", "\uE897", "F1"));
        _allCommands.Add(new CommandItem("export-diagnostics", "Export Diagnostics", "Create a support bundle", "\uE7B8", ""));
        _allCommands.Add(new CommandItem("search-sessions", "Search Sessions", "Find a conversation", "\uE721", "Ctrl+F"));
        _allCommands.Add(new CommandItem("clear-memory", "Clear Memory", "Remove assistant memories", "\uE74D", ""));

        FilterCommands("");
    }

    private void SetupEventHandlers()
    {
        SearchInput.TextChanged += OnSearchTextChanged;
        SearchInput.KeyDown += OnSearchKeyDown;
        CommandsList.ItemClick += OnCommandClick;
    }

    /// <summary>
    /// Focuses the search input.
    /// </summary>
    public void Focus()
    {
        SearchInput.Focus(FocusState.Programmatic);
        SearchInput.SelectAll();
    }

    /// <summary>
    /// Clears and resets the palette.
    /// </summary>
    public void Reset()
    {
        SearchInput.Text = "";
        FilterCommands("");
        CommandsList.SelectedIndex = 0;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterCommands(SearchInput.Text);
    }

    private void FilterCommands(string query)
    {
        _filteredCommands.Clear();

        var lowerQuery = query.ToLowerInvariant();

        foreach (var cmd in _allCommands)
        {
            if (string.IsNullOrEmpty(query) ||
                cmd.Name.ToLowerInvariant().Contains(lowerQuery) ||
                cmd.Description.ToLowerInvariant().Contains(lowerQuery) ||
                cmd.Id.Contains(lowerQuery))
            {
                _filteredCommands.Add(cmd);
            }
        }

        if (_filteredCommands.Count > 0)
        {
            CommandsList.SelectedIndex = 0;
        }
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                ExecuteSelectedCommand();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Down:
                if (CommandsList.SelectedIndex < _filteredCommands.Count - 1)
                {
                    CommandsList.SelectedIndex++;
                }
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Up:
                if (CommandsList.SelectedIndex > 0)
                {
                    CommandsList.SelectedIndex--;
                }
                e.Handled = true;
                break;
        }
    }

    private void OnCommandClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandItem cmd)
        {
            CommandExecuted?.Invoke(this, cmd.Id);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ExecuteSelectedCommand()
    {
        if (CommandsList.SelectedItem is CommandItem cmd)
        {
            CommandExecuted?.Invoke(this, cmd.Id);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// Represents a command in the palette.
/// </summary>
public class CommandItem
{
    public CommandItem(string id, string name, string description, string icon, string shortcut)
    {
        Id = id;
        Name = name;
        Description = description;
        Icon = icon;
        Shortcut = shortcut;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Icon { get; }
    public string Shortcut { get; }
    public Visibility HasDescription => string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
}
