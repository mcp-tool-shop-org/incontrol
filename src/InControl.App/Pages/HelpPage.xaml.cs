using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using InControl.App.Services;

namespace InControl.App.Pages;

/// <summary>
/// Built-in Help Center with Troubleshooting Assistant.
/// Provides searchable documentation, live diagnostics, and support bundle export.
/// </summary>
public sealed partial class HelpPage : UserControl
{
    private DiagnosticsReport? _lastDiagnosticsReport;

    public HelpPage()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    /// <summary>
    /// Event raised when user wants to go back.
    /// </summary>
    public event EventHandler? BackRequested;

    /// <summary>
    /// Event raised when Model Manager should open.
    /// </summary>
    public event EventHandler? ModelManagerRequested;

    /// <summary>
    /// Navigate to a specific help section via deep link.
    /// Format: help://section/topic
    /// </summary>
    public void NavigateToSection(string deepLink)
    {
        if (string.IsNullOrEmpty(deepLink)) return;

        // Parse deep link: help://troubleshooting/ollama-not-running
        var parts = deepLink.Replace("help://", "").Split('/');
        if (parts.Length < 1) return;

        var section = parts[0].ToLowerInvariant();

        switch (section)
        {
            case "troubleshooting":
                TroubleshootingNav.IsChecked = true;
                // Future: scroll to specific topic if parts[1] exists
                break;
            case "models":
                ModelsNav.IsChecked = true;
                break;
            case "gettingstarted":
                GettingStartedNav.IsChecked = true;
                break;
            case "shortcuts":
                ShortcutsNav.IsChecked = true;
                break;
        }
    }

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);

        // Navigation
        GettingStartedNav.Checked += (s, e) => ShowSection("GettingStarted");
        ModelsNav.Checked += (s, e) => ShowSection("Models");
        AssistantNav.Checked += (s, e) => ShowSection("Assistant");
        ExtensionsNav.Checked += (s, e) => ShowSection("Extensions");
        PoliciesNav.Checked += (s, e) => ShowSection("Policies");
        ConnectivityNav.Checked += (s, e) => ShowSection("Connectivity");
        TroubleshootingNav.Checked += (s, e) => ShowSection("Troubleshooting");
        ShortcutsNav.Checked += (s, e) => ShowSection("Shortcuts");

        // Actions
        CopyDiagnosticsButton.Click += OnCopyDiagnosticsClick;
        ExportSupportBundleButton.Click += OnExportSupportBundleClick;
        ExportSupportBundleHelpButton.Click += OnExportSupportBundleClick;
        OpenModelManagerHelpButton.Click += (s, e) => ModelManagerRequested?.Invoke(this, EventArgs.Empty);
        RunDiagnosticsButton.Click += OnRunDiagnosticsClick;

        // Search
        HelpSearch.TextChanged += OnSearchTextChanged;
    }

    private void ShowSection(string sectionName)
    {
        // Hide all sections
        GettingStartedSection.Visibility = Visibility.Collapsed;
        ModelsSection.Visibility = Visibility.Collapsed;
        ShortcutsSection.Visibility = Visibility.Collapsed;
        TroubleshootingSection.Visibility = Visibility.Collapsed;

        // Show requested section
        switch (sectionName)
        {
            case "GettingStarted":
                GettingStartedSection.Visibility = Visibility.Visible;
                break;
            case "Models":
                ModelsSection.Visibility = Visibility.Visible;
                break;
            case "Shortcuts":
                ShortcutsSection.Visibility = Visibility.Visible;
                break;
            case "Troubleshooting":
                TroubleshootingSection.Visibility = Visibility.Visible;
                break;
            // Add more sections as needed
            default:
                GettingStartedSection.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var searchText = sender.Text.ToLowerInvariant();
            // Future: Implement help content search
        }
    }

    private async void OnRunDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunDiagnosticsAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not run diagnostics", ex);
        }
    }

    private async Task RunDiagnosticsAsync()
    {
        // Run diagnostics and show results
        var report = await DiagnosticsService.Instance.RunDiagnosticsAsync();
        _lastDiagnosticsReport = report;

        // Build results display
        var content = new StackPanel { Spacing = 12 };

        foreach (var check in report.Checks)
        {
            var statusIcon = check.Status switch
            {
                DiagnosticStatus.Pass => "\uE73E",    // Checkmark
                DiagnosticStatus.Warning => "\uE7BA", // Warning
                DiagnosticStatus.Fail => "\uE711",    // X
                _ => "\uE946"                          // Info
            };

            var statusColor = check.Status switch
            {
                DiagnosticStatus.Pass => Microsoft.UI.Colors.LimeGreen,
                DiagnosticStatus.Warning => Microsoft.UI.Colors.Orange,
                DiagnosticStatus.Fail => Microsoft.UI.Colors.Red,
                _ => Microsoft.UI.Colors.Gray
            };

            var row = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new FontIcon
            {
                Glyph = statusIcon,
                FontSize = 14,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(statusColor)
            });
            row.Children.Add(new TextBlock { Text = $"{check.Name}: {check.Message}" });
            content.Children.Add(row);
        }

        // Add copy button info
        content.Children.Add(new TextBlock
        {
            Text = "\nClick 'Copy to Clipboard' to copy the full report.",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontStyle = Windows.UI.Text.FontStyle.Italic
        });

        var dialog = new ContentDialog
        {
            Title = $"Diagnostics: {report.OverallStatus}",
            Content = content,
            PrimaryButtonText = "Copy to Clipboard",
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var diagnosticsText = DiagnosticsService.Instance.GenerateTextReport(report);
            if (!TryCopyText(diagnosticsText))
            {
                await ShowMessageAsync("Copy failed", "The clipboard is busy. Try again.");
            }
        }
    }

    private async void OnCopyDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Run live diagnostics
            var report = await DiagnosticsService.Instance.RunDiagnosticsAsync();
            _lastDiagnosticsReport = report;

            var diagnosticsText = DiagnosticsService.Instance.GenerateTextReport(report);

            if (!TryCopyText(diagnosticsText))
            {
                await ShowMessageAsync("Copy failed", "The clipboard is busy. Try again.");
                return;
            }

            // Show confirmation via content dialog
            await ShowMessageAsync("Diagnostics Copied", GetDiagnosticsSummary(report));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not copy diagnostics", ex);
        }
    }

    /// <summary>
    /// A busy clipboard throws out of SetContent, so a failed copy has to stay on this click.
    /// </summary>
    private static bool TryCopyText(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task ShowErrorAsync(string what, Exception ex)
    {
        var detail = ex.Message.Trim();
        var cut = detail.IndexOfAny(['\r', '\n']);
        if (cut >= 0)
            detail = detail[..cut];

        await ShowMessageAsync(what, detail.Length == 0 ? ex.GetType().Name : detail);
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
            // A dialog is already open. Nothing else to show it on.
        }
    }

    private string GetDiagnosticsSummary(DiagnosticsReport report)
    {
        var passCount = report.Checks.Count(c => c.Status == DiagnosticStatus.Pass);
        var warnCount = report.Checks.Count(c => c.Status == DiagnosticStatus.Warning);
        var failCount = report.Checks.Count(c => c.Status == DiagnosticStatus.Fail);

        var summary = $"Diagnostics copied to clipboard.\n\n";
        summary += $"Results: {passCount} passed";
        if (warnCount > 0) summary += $", {warnCount} warnings";
        if (failCount > 0) summary += $", {failCount} issues found";

        if (failCount > 0)
        {
            summary += "\n\nIssues detected:";
            foreach (var check in report.Checks.Where(c => c.Status == DiagnosticStatus.Fail))
            {
                summary += $"\n• {check.Message}";
            }
        }

        return summary;
    }

    private async void OnExportSupportBundleClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ExportSupportBundleAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not export the support bundle", ex);
        }
    }

    private async Task ExportSupportBundleAsync()
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text File", new[] { ".txt" });
        picker.SuggestedFileName = $"incontrol-support-{DateTime.Now:yyyyMMdd-HHmmss}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            // Run fresh diagnostics for support bundle
            var report = await DiagnosticsService.Instance.RunDiagnosticsAsync();
            var diagnosticsText = DiagnosticsService.Instance.GenerateTextReport(report);

            // Add system info
            var bundle = new System.Text.StringBuilder();
            bundle.AppendLine(diagnosticsText);
            bundle.AppendLine();
            bundle.AppendLine("=== System Information ===");
            bundle.AppendLine($"OS: {Environment.OSVersion}");
            bundle.AppendLine($".NET Version: {Environment.Version}");
            bundle.AppendLine($"Machine: {Environment.MachineName}");
            bundle.AppendLine($"Processors: {Environment.ProcessorCount}");
            bundle.AppendLine($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
            bundle.AppendLine();
            bundle.AppendLine("=== Paths ===");
            bundle.AppendLine($"App Data: {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\InControl");
            bundle.AppendLine($"Current Dir: {Environment.CurrentDirectory}");

            await Windows.Storage.FileIO.WriteTextAsync(file, bundle.ToString());

            // Show confirmation
            var dialog = new ContentDialog
            {
                Title = "Support Bundle Exported",
                Content = $"Saved to: {file.Path}\n\nShare this file when reporting issues.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
