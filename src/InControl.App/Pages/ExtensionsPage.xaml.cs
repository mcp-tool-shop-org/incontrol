using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace InControl.App.Pages;

/// <summary>
/// Extensions management page - view, install, and configure extensions.
/// </summary>
public sealed partial class ExtensionsPage : UserControl
{
    private readonly ObservableCollection<ExtensionInfo> _extensions = new();

    public ExtensionsPage()
    {
        this.InitializeComponent();
        SetupEventHandlers();
        ExtensionsListView.ItemsSource = _extensions;
    }

    /// <summary>
    /// Event raised when user wants to go back.
    /// </summary>
    public event EventHandler? BackRequested;

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        InstallButton.Click += OnInstallClick;
        EmptyInstallButton.Click += OnInstallClick;

        // Tab navigation
        InstalledTab.Checked += (s, e) => SwitchToTab("Installed");
        AvailableTab.Checked += (s, e) => SwitchToTab("Available");

        // Selection
        ExtensionsListView.SelectionChanged += OnExtensionSelected;
    }

    private void SwitchToTab(string tabName)
    {
        SectionTitle.Text = tabName == "Installed" ? "Installed Extensions" : "Available Extensions";
        // Refresh list based on tab
    }

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        // Show extension browser/installer dialog
    }

    private void OnExtensionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ExtensionsListView.SelectedItem is ExtensionInfo ext)
        {
            NoSelectionState.Visibility = Visibility.Collapsed;
            ExtensionDetails.Visibility = Visibility.Visible;
            DetailName.Text = ext.Name;
            DetailDescription.Text = ext.Description;
        }
        else
        {
            NoSelectionState.Visibility = Visibility.Visible;
            ExtensionDetails.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Loads extensions into the list.
    /// </summary>
    public void LoadExtensions(IEnumerable<ExtensionInfo> extensions)
    {
        _extensions.Clear();
        foreach (var ext in extensions)
        {
            _extensions.Add(ext);
        }
        UpdateUI();
    }

    private void UpdateUI()
    {
        var count = _extensions.Count;
        ExtensionCountText.Text = count == 1 ? "1 extension" : $"{count} extensions";
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ExtensionsListView.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>
/// Extension information for UI display.
/// </summary>
public class ExtensionInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Icon { get; set; } = "\uEA86";
    public bool IsEnabled { get; set; } = true;
    public List<string> Permissions { get; set; } = new();
}
