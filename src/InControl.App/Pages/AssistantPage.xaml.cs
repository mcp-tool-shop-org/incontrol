using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InControl.Services.Interfaces;
using InControl.ViewModels.Sessions;

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

    /// <summary>
    /// Event raised after every remembered note was cleared.
    /// </summary>
    public event EventHandler? MemoryCleared;

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);

        // Tab navigation
        MemoryTab.Checked += (s, e) => SwitchToTab("Memory");
        ToolsTab.Checked += (s, e) => SwitchToTab("Tools");
        ActivityTab.Checked += (s, e) => SwitchToTab("Activity");

        // Actions
        ClearMemoryButton.Click += OnClearMemoryClick;
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
            Content = "This permanently deletes every note you asked InControl to remember, in every project and session. Your chats are not deleted. This cannot be undone.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await SessionMemoryClearer.ClearAllAsync(
                App.GetService<ISessionMemory>(),
                App.GetService<IProjectLibrary>(),
                App.GetService<IChatService>());
            MemoryCountText.Text = "0 items";
            MemoryCleared?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var text = ex.Message.Trim();
            var cut = text.IndexOfAny(['\r', '\n']);
            if (cut >= 0)
            {
                text = text[..cut];
            }

            await ShowMessageAsync("Could not clear memory", text.Length == 0 ? ex.GetType().Name : text);
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // A dialog is already open. The message has nowhere else to go.
        }
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
