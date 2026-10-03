using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using InControl.App.Services;
using InControl.Core.Compute;
using InControl.Core.Configuration;
using InControl.Services.Interfaces;
using InControl.Services.Voice;
using InControl.ViewModels.Sessions;

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
        CurrentVersionText.Text = InformationalVersion();
        CollectSections();
        SetupEventHandlers();
        InitializeThemeComboBox();
        InitializeStoragePath();
        InitializeVoiceSection();
        InitializeComputeSection();
        InitializeGeneralToggles();
    }

    private const string StartupTaskId = "InControlStartup";

    /// <summary>
    /// Launch at startup is a Windows startup task, so Windows keeps that choice. Minimize to
    /// tray is a saved setting.
    /// </summary>
    private async void InitializeGeneralToggles()
    {
        TrayToggle.IsOn = App.GetService<IOptions<AppOptions>>().Value.MinimizeToTray;
        TrayToggle.Toggled += (_, _) => WriteApp(options => options.MinimizeToTray = TrayToggle.IsOn);

        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
            ShowStartupState(task.State);
            StartupToggle.Toggled += OnStartupToggled;
        }
        catch (Exception)
        {
            // Only the installed package has a startup task. A source build does not.
            StartupToggle.IsEnabled = false;
            StartupCaption.Text = "Available when InControl is installed from the Microsoft Store";
        }
    }

    private bool _settingStartup;

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_settingStartup)
            return;

        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
            if (StartupToggle.IsOn)
            {
                ShowStartupState(await task.RequestEnableAsync());
            }
            else
            {
                task.Disable();
                ShowStartupState(task.State);
            }
        }
        catch (Exception ex)
        {
            StartupCaption.Text = $"Windows did not change the startup setting: {ex.Message}";
        }
    }

    private void ShowStartupState(Windows.ApplicationModel.StartupTaskState state)
    {
        _settingStartup = true;
        StartupToggle.IsOn = state is Windows.ApplicationModel.StartupTaskState.Enabled
            or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
        _settingStartup = false;

        StartupToggle.IsEnabled = state is not (Windows.ApplicationModel.StartupTaskState.DisabledByPolicy
            or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy);

        StartupCaption.Text = state switch
        {
            Windows.ApplicationModel.StartupTaskState.DisabledByUser =>
                "Turned off in Windows Settings, Apps, Startup. Turn it back on there.",
            Windows.ApplicationModel.StartupTaskState.DisabledByPolicy =>
                "Your organization does not allow apps to start with Windows.",
            Windows.ApplicationModel.StartupTaskState.EnabledByPolicy =>
                "Your organization starts InControl with Windows.",
            _ => "Start InControl when Windows starts"
        };
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

    /// <summary>
    /// Event raised after every remembered note was cleared.
    /// </summary>
    public event EventHandler? MemoryCleared;

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
        catch (Exception ex)
        {
            _fillingPods = false;
            ComputeMessageText.Text = "Could not look up pods: " + Brief(ex);
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
        catch (Exception ex)
        {
            ComputeMessageText.Text = "Could not connect: " + Brief(ex);
        }
        finally
        {
            ConnectComputeButton.IsEnabled = true;
        }
    }

    private async void OnStayLocalClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var session = App.GetService<ComputeSession>();
            await session.UseThisPcAsync();
            ComputeStatusText.Text = session.Notice;
            ComputeMessageText.Text = ComputeNotice.OnThisPc;
        }
        catch (Exception ex)
        {
            ComputeMessageText.Text = "Could not switch back to this PC: " + Brief(ex);
        }
    }

    private static string Brief(Exception ex)
    {
        var text = ex.Message.Trim();
        var cut = text.IndexOfAny(['\r', '\n']);
        if (cut >= 0)
        {
            text = text[..cut];
        }

        if (text.Length > 160)
        {
            text = text[..160] + "...";
        }

        return text.Length == 0 ? ex.GetType().Name : text;
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
            var voiceOpts = App.GetService<IOptions<VoiceOptions>>().Value;
            AutoSpeakToggle.IsOn = voiceOpts.AutoSpeak;
            VoiceVolumeSlider.Value = voiceOpts.Volume;
            VoiceSpeedSlider.Value = voiceOpts.Speed;
        }
        catch
        {
            // Leave the XAML defaults when options are not available yet.
        }

        AutoSpeakToggle.Toggled += OnAutoSpeakToggled;
        VoiceVolumeSlider.ValueChanged += OnVoiceVolumeChanged;
        VoiceSpeedSlider.ValueChanged += OnVoiceSpeedChanged;

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
            // Takes effect on the next SpeakAsync call, and is saved for next time.
            WriteVoice(options => options.DefaultVoice = selectedVoice);
        }
    }

    private void OnAutoSpeakToggled(object sender, RoutedEventArgs e)
    {
        WriteVoice(options => options.AutoSpeak = AutoSpeakToggle.IsOn);
    }

    private void OnVoiceVolumeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        WriteVoice(options => options.Volume = (float)e.NewValue);
    }

    private void OnVoiceSpeedChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        WriteVoice(options => options.Speed = (float)e.NewValue);
    }

    /// <summary>
    /// Changes the voice options SpeakAsync and auto-speak read, through the settings service so the change is saved.
    /// </summary>
    private static void WriteVoice(Action<VoiceOptions> configure)
    {
        // The settings service changes the live options and records the change. Changing them
        // first would leave it nothing to record.
        if (App.Services.GetService(typeof(ISettingsService)) is ISettingsService settings)
        {
            _ = PersistVoiceAsync(settings, configure);
        }
        else
        {
            configure(App.GetService<IOptions<VoiceOptions>>().Value);
        }
    }

    /// <summary>
    /// Changes an app option now and saves it.
    /// </summary>
    private static void WriteApp(Action<AppOptions> configure)
    {
        if (App.Services.GetService(typeof(ISettingsService)) is ISettingsService settings)
        {
            _ = SaveQuietlyAsync(() => settings.UpdateAppOptionsAsync(configure));
        }
        else
        {
            configure(App.GetService<IOptions<AppOptions>>().Value);
        }
    }

    private static async Task SaveQuietlyAsync(Func<Task> save)
    {
        try
        {
            await save();
        }
        catch
        {
            // The live option already changed. A failed save must not break the page.
        }
    }

    private static async Task PersistVoiceAsync(ISettingsService settings, Action<VoiceOptions> configure)
    {
        try
        {
            await settings.UpdateVoiceOptionsAsync(configure);
        }
        catch
        {
            // The live options already changed. A failed save must not break the page.
        }
    }

    private static string InformationalVersion()
    {
        var informational = typeof(SettingsPage).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "2.0.1";
        }

        var plus = informational.IndexOf('+');
        var version = plus >= 0 ? informational[..plus] : informational;
        return string.IsNullOrWhiteSpace(version) ? "2.0.1" : version.Trim();
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
        WriteApp(options => options.Theme = ThemeService.Instance.CurrentThemeString);
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

    private async void OnClearMemoryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear All Memory",
            Content = "This permanently deletes every note you asked InControl to remember, in every project and session. Your chats are not deleted. This cannot be undone.",
            PrimaryButtonText = "Clear All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
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
            MemoryCleared?.Invoke(this, EventArgs.Empty);
            await ShowMessageAsync("Memory cleared", "Every remembered note was deleted.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Could not clear memory", Brief(ex));
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

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Check for Updates",
            Content = $"This build does not check for updates. The version is {InformationalVersion()}.",
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
            Title = "Reset appearance",
            Content = "This switches the theme back to follow Windows. Your chats and notes are not affected.",
            PrimaryButtonText = "Reset theme",
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
        }
    }
}
