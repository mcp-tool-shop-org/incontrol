using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InControl.App.Pages;

/// <summary>
/// Assistant visibility page - makes the assistant's behavior tangible.
/// Shows memory, tools, and activity trace.
/// </summary>
public sealed partial class AssistantPage : UserControl
{
    public AssistantPage()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    /// <summary>
    /// Event raised when user wants to go back.
    /// </summary>
    public event EventHandler? BackRequested;

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);

        // Tab navigation
        MemoryTab.Checked += (s, e) => SwitchToTab("Memory");
        ToolsTab.Checked += (s, e) => SwitchToTab("Tools");
        ActivityTab.Checked += (s, e) => SwitchToTab("Activity");

        // Actions
        ClearMemoryButton.Click += OnClearMemoryClick;
        ClearActivityButton.Click += OnClearActivityClick;
        AssistantEnabledToggle.Toggled += OnAssistantEnabledToggled;
    }

    private void SwitchToTab(string tabName)
    {
        MemoryView.Visibility = tabName == "Memory" ? Visibility.Visible : Visibility.Collapsed;
        ToolsView.Visibility = tabName == "Tools" ? Visibility.Visible : Visibility.Collapsed;
        ActivityView.Visibility = tabName == "Activity" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnClearMemoryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Memory",
            Content = "Are you sure you want to clear all stored memories? This cannot be undone.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Clear memories
        }
    }

    private void OnClearActivityClick(object sender, RoutedEventArgs e)
    {
        // Clear activity log
    }

    private void OnAssistantEnabledToggled(object sender, RoutedEventArgs e)
    {
        var isEnabled = AssistantEnabledToggle.IsOn;
        AssistantStatusText.Text = isEnabled ? "Active" : "Disabled";
        AssistantStatusIndicator.Fill = isEnabled
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
    }
}
