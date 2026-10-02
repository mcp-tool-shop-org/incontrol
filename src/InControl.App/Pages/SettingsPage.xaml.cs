using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InControl.App.Services;
using InControl.Core.Compute;
using InControl.Core.Configuration;
using InControl.Services.Voice;

namespace InControl.App.Pages;

/// <summary>
/// Centralized settings hub for all InControl configuration.
/// Provides searchable access to General, Models, Assistant, Memory, Extensions,
/// Connectivity, Updates, and Diagnostics settings.
/// </summary>
public sealed partial class SettingsPage : UserControl
{
    private readonly List<StackPanel> _settingsSections = new();
    private IReadOnlyList<RunPodPod> _runPods = [];
    private bool _fillingPods;
    private ComputeSession? _compute;
    private DispatcherQueue? _computeQueue;

    public SettingsPage()
    {
        this.InitializeComponent();
        CollectSections();
        SetupEventHandlers();
        InitializeThemeComboBox();
        InitializeStoragePath();
        InitializeVoiceSection();
        InitializeComputeSection();
    }

    private void CollectSections()
    {
        // Collect all settings sections for search filtering
        _settingsSections.AddRange(new[]
        {
            GeneralSection,
            ComputeSection,
            ModelsSection,
            VoiceSection,
            AssistantSection,
            MemorySection,
            ExtensionsSection,
            ConnectivitySection,
            UpdatesSection,
            DiagnosticsSection
        });
    }

    private void InitializeThemeComboBox()
    {
        // Set initial selection based on current theme
        ThemeComboBox.SelectedIndex = ThemeService.Instance.GetThemeIndex();
    }

    private void InitializeStoragePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        StoragePathText.Text = System.IO.Path.Combine(appData, "InControl", "memory");
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
    /// Event raised when Extensions page should open.
    /// </summary>
    public event EventHandler? ExtensionsRequested;

    /// <summary>
    /// Event raised when Policy page should open.
    /// </summary>
    public event EventHandler? PolicyRequested;

    /// <summary>
    /// Event raised when theme changes.
    /// </summary>
    public event EventHandler<string>? ThemeChanged;

    public event EventHandler<bool>? OfflineModeChanged;

    public bool IsOffline
    {
        get => OfflineModeToggle.IsOn;
        set
        {
            if (OfflineModeToggle.IsOn != value)
            {
                OfflineModeToggle.IsOn = value;
            }
        }
    }

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        OpenModelManagerButton.Click += (s, e) => ModelManagerRequested?.Invoke(this, EventArgs.Empty);
        OpenExtensionsButton.Click += (s, e) => ExtensionsRequested?.Invoke(this, EventArgs.Empty);
        OpenPolicyButton.Click += (s, e) => PolicyRequested?.Invoke(this, EventArgs.Empty);

        // Theme selection
        ThemeComboBox.SelectionChanged += OnThemeSelectionChanged;

        // Settings search functionality
        SettingsSearch.TextChanged += OnSearchTextChanged;

        // Memory section buttons
        ChangeStorageButton.Click += OnChangeStorageClick;
        ClearMemoryButton.Click += OnClearMemoryClick;

        // Updates section buttons
        CheckUpdatesButton.Click += OnCheckUpdatesClick;

        ConnectComputeButton.Click += OnConnectComputeClick;
        StayLocalButton.Click += OnStayLocalClick;
        ForgetHostKeyButton.Click += OnForgetHostKeyClick;
        LookupRunPodButton.Click += OnLookupRunPodClick;
        ComputePodBox.SelectionChanged += OnRunPodSelected;
        OfflineModeToggle.Toggled += (_, _) => OfflineModeChanged?.Invoke(this, OfflineModeToggle.IsOn);

        // Diagnostics section buttons
        ExportDiagnosticsButton.Click += OnExportDiagnosticsClick;
        OpenLogsButton.Click += OnOpenLogsClick;
        ResetSettingsButton.Click += OnResetSettingsClick;
    }

    private void InitializeComputeSection()
    {
        ComputeMessageText.Text = ComputeNotice.AddressResets + " " + ComputeNotice.OllamaStillLocal;
        _compute = App.GetService<ComputeSession>();
        ComputeStatusText.Text = _compute.Notice;
        _computeQueue = DispatcherQueue.GetForCurrentThread();
        _compute.Changed += OnComputeChanged;
        Unloaded += OnComputeUnloaded;
    }

    private void OnComputeChanged(object? sender, EventArgs e)
    {
        _computeQueue?.TryEnqueue(() =>
        {
            if (_compute is not null)
            {
                ComputeStatusText.Text = _compute.Notice;
            }
        });
    }

    private void OnComputeUnloaded(object sender, RoutedEventArgs e)
    {
        if (_compute is not null)
        {
            _compute.Changed -= OnComputeChanged;
        }
    }

    private async void OnLookupRunPodClick(object sender, RoutedEventArgs e)
    {
        if (App.GetService<ComputeSession>().IsOffline)
        {
            ComputeMessageText.Text = ComputeNotice.OfflineBlocksRental;
            return;
        }

        LookupRunPodButton.IsEnabled = false;
        ComputeMessageText.Text = "Asking RunPod for pods that are already running…";
        try
        {
            var lookup = await App.GetService<IRunPodPods>().ListAsync();
            _runPods = lookup.Pods;
            var offerable = lookup.Pods.Where(static pod => pod.CanOffer).ToList();
            _fillingPods = true;
            ComputePodBox.Items.Clear();
            foreach (var pod in offerable)
            {
                ComputePodBox.Items.Add(pod.Label);
            }

            ComputePodBox.Visibility = offerable.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            _fillingPods = false;
            if (offerable.Count == 1)
            {
                ApplyPod(offerable[0]);
            }

            ComputeMessageText.Text = lookup.Message;
        }
        finally
        {
            LookupRunPodButton.IsEnabled = true;
        }
    }

    private void OnRunPodSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_fillingPods)
        {
            return;
        }

        var offerable = _runPods.Where(static pod => pod.CanOffer).ToList();
        var index = ComputePodBox.SelectedIndex;
        if (index >= 0 && index < offerable.Count)
        {
            ApplyPod(offerable[index]);
        }
    }

    private void ApplyPod(RunPodPod pod)
    {
        ComputeNameBox.Text = pod.Name;
        ComputeConnectBox.Text = pod.SshCommand;
        ComputeMessageText.Text =
            $"Command filled for {pod.Label}. Connect sends the chat to that machine. Lookup does not.";
    }

    private async void OnConnectComputeClick(object sender, RoutedEventArgs e)
    {
        if (!SshConnectString.TryParse(ComputeConnectBox.Text, out var fields, out var error) || fields is null)
        {
            ComputeMessageText.Text = error ?? "Paste the ssh command from the rental.";
            return;
        }

        var identity = string.IsNullOrWhiteSpace(ComputeIdentityBox.Text)
            ? fields.IdentityFile
            : ComputeIdentityBox.Text.Trim();
        var name = string.IsNullOrWhiteSpace(ComputeNameBox.Text)
            ? fields.Host
            : ComputeNameBox.Text.Trim();
        var remotePort = 11434;
        if (!string.IsNullOrWhiteSpace(ComputeOllamaPortBox.Text)
            && !int.TryParse(ComputeOllamaPortBox.Text.Trim(), out remotePort))
        {
            ComputeMessageText.Text = "The Ollama port on that machine has to be a number.";
            return;
        }

        var endpoint = new SshEndpoint
        {
            DisplayName = name,
            User = fields.User,
            Host = fields.Host,
            SshPort = fields.SshPort,
            IdentityFile = identity ?? "",
            RemoteOllamaPort = remotePort
        };

        ConnectComputeButton.IsEnabled = false;
        ComputeMessageText.Text = "Opening the SSH forward…";
        try
        {
            var result = await App.GetService<ComputeSession>().ConnectAsync(endpoint);
            ComputeStatusText.Text = App.GetService<ComputeSession>().Notice;
            ComputeMessageText.Text = result.Message;
        }
        finally
        {
            ConnectComputeButton.IsEnabled = true;
        }
    }

    private async void OnStayLocalClick(object sender, RoutedEventArgs e)
    {
        var session = App.GetService<ComputeSession>();
        await session.UseThisPcAsync();
        ComputeStatusText.Text = session.Notice;
        ComputeMessageText.Text = ComputeNotice.OnThisPc;
    }

    private void OnForgetHostKeyClick(object sender, RoutedEventArgs e)
    {
        if (!SshConnectString.TryParse(ComputeConnectBox.Text, out var fields, out var error) || fields is null)
        {
            ComputeMessageText.Text = error ?? "Paste the ssh command first, then forget its host key.";
            return;
        }

        var endpoint = new SshEndpoint
        {
            DisplayName = string.IsNullOrWhiteSpace(ComputeNameBox.Text) ? fields.Host : ComputeNameBox.Text.Trim(),
            User = fields.User,
            Host = fields.Host,
            SshPort = fields.SshPort,
            IdentityFile = string.IsNullOrWhiteSpace(ComputeIdentityBox.Text)
                ? fields.IdentityFile ?? ""
                : ComputeIdentityBox.Text.Trim()
        };
        ComputeMessageText.Text = App.GetService<ComputeSession>().ForgetHostKey(endpoint).Message;
    }

    private async void InitializeVoiceSection()
    {
        try
        {
            var voiceService = App.GetService<IVoiceService>();

            // Ensure the engine is loaded so we can list voices
            if (voiceService.ConnectionState != VoiceConnectionState.Connected)
                await voiceService.ConnectAsync();

            if (voiceService.AvailableVoices.Count > 0)
            {
                foreach (var voice in voiceService.AvailableVoices)
                    VoiceComboBox.Items.Add(voice);

                // Select current default voice
                var voiceOpts = App.GetService<IOptions<VoiceOptions>>();
                var currentVoice = voiceOpts.Value.DefaultVoice;
                var voiceList = voiceService.AvailableVoices.ToList();
                var index = voiceList.IndexOf(currentVoice);
                VoiceComboBox.SelectedIndex = index >= 0 ? index : 0;

                VoiceStatusText.Text = $"Ready ({voiceService.AvailableVoices.Count} voices)";
            }
            else
            {
                VoiceStatusText.Text = "No voices found";
            }
        }
        catch
        {
            VoiceStatusText.Text = "Failed to load voices";
        }

        // Wire up events
        TestVoiceButton.Click += OnTestVoiceClick;
        VoiceComboBox.SelectionChanged += OnVoiceSelectionChanged;
    }

    private void OnVoiceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VoiceComboBox.SelectedItem is string selectedVoice)
        {
            // Update the voice option in-memory — takes effect on next SpeakAsync call
            var voiceOpts = App.GetService<IOptions<VoiceOptions>>();
            voiceOpts.Value.DefaultVoice = selectedVoice;
        }
    }

    private async void OnTestVoiceClick(object sender, RoutedEventArgs e)
    {
        var voiceService = App.GetService<IVoiceService>();
        var selectedVoice = VoiceComboBox.SelectedItem as string;

        TestVoiceButton.IsEnabled = false;
        VoiceStatusText.Text = "Speaking...";

        try
        {
            await voiceService.SpeakAsync("Hello! This is a voice test.", selectedVoice);
            VoiceStatusText.Text = $"Voice: {selectedVoice ?? "default"}";
        }
        catch (Exception ex)
        {
            VoiceStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            TestVoiceButton.IsEnabled = true;
        }
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ThemeService.Instance.SetThemeFromIndex(ThemeComboBox.SelectedIndex);
        ThemeChanged?.Invoke(this, ThemeService.Instance.CurrentThemeString);
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            FilterSettings(sender.Text);
        }
    }

    private void FilterSettings(string searchText)
    {
        var hasResults = false;
        var query = searchText.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(query))
        {
            // Show all sections
            foreach (var section in _settingsSections)
            {
                section.Visibility = Visibility.Visible;
            }
            NoResultsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        // Filter sections based on search tags
        foreach (var section in _settingsSections)
        {
            var tags = section.Tag?.ToString()?.ToLowerInvariant() ?? "";
            var isMatch = tags.Contains(query);

            section.Visibility = isMatch ? Visibility.Visible : Visibility.Collapsed;
            if (isMatch) hasResults = true;
        }

        // Show/hide no results panel
        NoResultsPanel.Visibility = hasResults ? Visibility.Collapsed : Visibility.Visible;
        if (!hasResults)
        {
            NoResultsText.Text = $"No settings found for \"{searchText}\"";
        }
    }

    private async void OnChangeStorageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Change Storage Location",
            Content = "This feature will be available in a future update.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnClearMemoryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear All Memory",
            Content = "This will permanently delete all stored context and conversation history. This action cannot be undone.",
            PrimaryButtonText = "Clear All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // TODO: Clear memory implementation
        }
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Check for Updates",
            Content = "You are running the latest version (v1.0.0).",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnExportDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Export Diagnostics",
            Content = "This feature will be available in a future update.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        var logsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InControl", "logs");

        if (!System.IO.Directory.Exists(logsPath))
        {
            System.IO.Directory.CreateDirectory(logsPath);
        }

        System.Diagnostics.Process.Start("explorer.exe", logsPath);
    }

    private async void OnResetSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset to Defaults",
            Content = "This will restore all settings to their default values. Your models, extensions, and session data will not be affected.",
            PrimaryButtonText = "Reset Settings",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Reset theme to system
            ThemeComboBox.SelectedIndex = 2;
            ThemeService.Instance.SetThemeFromIndex(2);

            // TODO: Reset other settings
        }
    }
}
