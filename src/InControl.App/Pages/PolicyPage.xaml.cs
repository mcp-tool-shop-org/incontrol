using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InControl.App.Pages;

/// <summary>
/// Policy and Governance page - configure security policies and access controls.
/// </summary>
public sealed partial class PolicyPage : UserControl
{
    public PolicyPage()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    /// <summary>
    /// Event raised when user wants to go back.
    /// </summary>
    public event EventHandler? BackRequested;

    /// <summary>
    /// Event raised when offline mode changes.
    /// </summary>
    public event EventHandler<bool>? OfflineModeChanged;

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        ConfigureAllowListButton.Click += OnConfigureAllowListClick;
        ConfigureBlockListButton.Click += OnConfigureBlockListClick;
        ExportAuditLogButton.Click += OnExportAuditLogClick;
        OfflineModeToggle.Toggled += OnOfflineModeToggled;
    }

    private void OnOfflineModeToggled(object sender, RoutedEventArgs e)
    {
        OfflineModeChanged?.Invoke(this, OfflineModeToggle.IsOn);
    }

    private async void OnConfigureAllowListClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Allowed Domains",
            Content = CreateDomainListContent("allowed"),
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void OnConfigureBlockListClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Blocked Domains",
            Content = CreateDomainListContent("blocked"),
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private UIElement CreateDomainListContent(string type)
    {
        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(new TextBlock
        {
            Text = type == "allowed"
                ? "Enter domains that extensions and tools are allowed to access."
                : "Enter domains that should be blocked from all connections.",
            TextWrapping = TextWrapping.Wrap
        });

        var textBox = new TextBox
        {
            PlaceholderText = "example.com\napi.example.com",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 200
        };
        panel.Children.Add(textBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Enter one domain per line. Wildcards (*) are supported.",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        return panel;
    }

    private async void OnExportAuditLogClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("JSON", new[] { ".json" });
        picker.FileTypeChoices.Add("CSV", new[] { ".csv" });
        picker.SuggestedFileName = $"incontrol-audit-{DateTime.Now:yyyyMMdd}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            // Export audit log
            await Windows.Storage.FileIO.WriteTextAsync(file, "[]");
        }
    }

    /// <summary>
    /// Sets the current policy status display.
    /// </summary>
    public void SetPolicyStatus(bool isActive, string statusText)
    {
        PolicyStatusText.Text = statusText;
        PolicyLockIcon.Glyph = isActive ? "\uE72E" : "\uE785";
    }
}
