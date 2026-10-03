using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.Options;
using InControl.App.Controls;
using InControl.App.Pages;
using InControl.App.Services;
using InControl.Core.Assistant;
using InControl.Core.Attachments;
using InControl.Core.Configuration;
using InControl.Core.Models;
using InControl.Core.Storage;
using InControl.Core.Compute;
using InControl.Core.UX;
using InControl.Inference.Interfaces;
using InControl.Services.Interfaces;
using InControl.Services.Voice;
using InControl.Services.Web;
using InControl.ViewModels.ConversationView;
using InControl.ViewModels.Sessions;

namespace InControl.App;

/// <summary>
/// Main application window with page navigation support.
/// Provides global control bar, status strip, and seamless navigation between pages.
/// Uses NavigationService for consistent backstack behavior.
/// </summary>
public sealed partial class MainWindow : Window
{
    private UserControl? _currentPage;
    private bool _isOffline;
    private bool _isSidebarCollapsed = true;
    private readonly NavigationService _navigation = NavigationService.Instance;
    private readonly ConversationViewModel _conversationVm = new();
    private readonly SessionListViewModel _sessionListVm = new();
    private CancellationTokenSource? _runCts;
    private Guid? _answeringId;
    private readonly System.Text.StringBuilder _answerText = new();
    private string? _answerPrompt;
    private string? _answerModel;

    /// <summary>
    /// Model families that are embedding-only and cannot be used for chat.
    /// </summary>
    private static readonly HashSet<string> EmbeddingFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "nomic-bert", "bert"
    };

    /// <summary>
    /// Model name prefixes that indicate embedding-only models.
    /// </summary>
    private static readonly string[] EmbeddingPrefixes =
    [
        "nomic-embed", "all-minilm", "mxbai-embed", "snowflake-arctic-embed",
        "bge-", "gte-", "e5-"
    ];

    public MainWindow()
    {
        InitializeComponent();
        SetupWindow();
        InitializeTheme();
        SetupNavigation();
        SetupEventHandlers();
        InitializeStatusStrip();
        InitializeSidebar();

        // Ensure data directories exist
        DataPaths.EnsureDirectoriesExist();

        // Reload models when settings change (e.g., Ollama URL or default model)
        var settingsService = App.Services.GetService(typeof(ISettingsService)) as ISettingsService;
        if (settingsService != null)
        {
            settingsService.SettingsChanged += async (_, _) => await LoadModelsAsync();
        }

        _ = LoadModelsAsync();
        _ = InitializeVoiceAsync();
        _ = LoadSessionsAsync();
        ShowRecoveryNotice();
    }

    /// <summary>
    /// Says so when the last run of the app ended without a clean exit.
    /// </summary>
    private void ShowRecoveryNotice()
    {
        var recovery = CrashRecoveryService.Instance;
        if (!recovery.IsRecoveryMode)
            return;

        ShowNotice(recovery.GetRecoveryMessage(), InfoBarSeverity.Informational);
        recovery.AcknowledgeRecovery();
    }

    /// <summary>
    /// Shows a short closable message over the content area.
    /// Safe to call from any thread.
    /// </summary>
    public void ShowNotice(string message, InfoBarSeverity severity = InfoBarSeverity.Error)
    {
        void Show()
        {
            foreach (var child in NoticeHost.Children)
            {
                if (child is InfoBar existing && existing.Message == message)
                    return;
            }

            var bar = new InfoBar
            {
                Message = message,
                Severity = severity,
                IsClosable = true,
                IsOpen = true
            };
            bar.Closed += (sender, _) =>
            {
                if (sender is UIElement element)
                    NoticeHost.Children.Remove(element);
            };

            NoticeHost.Children.Add(bar);
            while (NoticeHost.Children.Count > 3)
                NoticeHost.Children.RemoveAt(0);
        }

        if (DispatcherQueue.HasThreadAccess)
            Show();
        else
            DispatcherQueue.TryEnqueue(Show);
    }

    private void ShowFailure(string what, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"{what}: {ex.Message}");
        ShowNotice($"{what}: {Brief(ex)}");
    }

    private static string Brief(Exception ex)
    {
        var text = ex.Message.Trim();

        // Ollama errors arrive as JSON. Show the sentence inside, not the braces.
        text = InControl.Core.Errors.ErrorText.Readable(text);
        var cut = text.IndexOfAny(['\r', '\n']);
        if (cut >= 0)
            text = text[..cut];

        if (text.Length > 160)
            text = text[..160] + "...";

        return text.Length == 0 ? ex.GetType().Name : text;
    }

    private void InitializeTheme()
    {
        // Initialize theme service with the root content element
        if (this.Content is FrameworkElement rootElement)
        {
            // Load saved theme preference (default to System)
            ThemeService.Instance.Initialize(rootElement, "System");
        }
    }

    private void SetupWindow()
    {
        var appWindow = this.AppWindow;
        appWindow.Title = "InControl - Local AI Chat";
        appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
    }

    private void SetupNavigation()
    {
        _navigation.Navigated += OnNavigated;
        _navigation.NavigatedBack += OnNavigatedBack;
        _navigation.NavigatedHome += (s, e) => NavigateHomeInternal();
    }

    private void InitializeSidebar()
    {
        SessionSidebar.SetViewModel(_sessionListVm);
    }

    /// <summary>
    /// Loads persisted sessions into the sidebar.
    /// </summary>
    private async Task LoadSessionsAsync()
    {
        try
        {
            var chatService = App.GetService<IChatService>();
            var projects = App.GetService<IProjectLibrary>();
            var memory = App.GetService<ISessionMemory>();
            await projects.EnsureAsync();
            await memory.EnsureAsync();

            var projectList = await projects.AllAsync();
            var conversations = await chatService.GetConversationsAsync();
            var notes = await memory.ListAsync(ChatProject.GeneralId, null);
            var instructions = (await projects.FindAsync(ChatProject.GeneralId))?.Instructions;
            var noteItems = ToNoteItems(notes);

            DispatcherQueue.TryEnqueue(() =>
            {
                _sessionListVm.SetProjects(projectList);
                foreach (var conversation in conversations)
                    _sessionListVm.AddSession(conversation);

                _sessionListVm.ApplyFilter();
                SessionSidebar.RefreshVisualState();
                SessionSidebar.SelectProject(ChatProject.GeneralId);
                SessionSidebar.ShowInstructions(ChatProject.GeneralId, instructions);
                SessionSidebar.ShowMemory(noteItems);
                SessionSidebar.SetRememberSessionEnabled(false);
            });
        }
        catch (Exception ex)
        {
            ShowFailure("Could not load your chats", ex);
        }
    }

    private void SetupEventHandlers()
    {
        // AppBar events - use NavigationService
        AppBar.HomeRequested += (s, e) => _navigation.GoHome();
        AppBar.SettingsRequested += (s, e) => _navigation.Navigate<SettingsPage>();
        AppBar.AssistantRequested += (s, e) => _navigation.Navigate<AssistantPage>();
        AppBar.ExtensionsRequested += (s, e) => _navigation.Navigate<ExtensionsPage>();
        AppBar.PolicyRequested += (s, e) => _navigation.Navigate<PolicyPage>();
        AppBar.ConnectivityRequested += (s, e) => _navigation.Navigate<ConnectivityPage>();
        AppBar.HelpRequested += (s, e) => _navigation.Navigate<HelpPage>();
        AppBar.ModelManagerRequested += (s, e) => _navigation.Navigate<ModelManagerPage>();
        AppBar.CommandPaletteRequested += (s, e) => ShowCommandPalette();

        var compute = App.GetService<ComputeSession>();
        void ShowComputeNotice()
        {
            DispatcherQueue.TryEnqueue(() =>
                AppBar.SetComputeNotice(compute.PromptsLeaveThisPc, compute.Notice));
        }

        compute.Changed += (_, _) => ShowComputeNotice();
        ShowComputeNotice();

        // Command Palette events
        CommandPalette.CommandExecuted += OnCommandExecuted;
        CommandPalette.CloseRequested += (s, e) => HideCommandPalette();

        // StatusStrip events - use NavigationService
        StatusStrip.ModelClicked += (s, e) => _navigation.Navigate<ModelManagerPage>();
        StatusStrip.DeviceClicked += (s, e) => _navigation.Navigate<ModelManagerPage>();
        StatusStrip.ConnectivityClicked += (s, e) => _navigation.Navigate<ConnectivityPage>();
        StatusStrip.PolicyClicked += (s, e) => _navigation.Navigate<PolicyPage>();
        StatusStrip.AssistantClicked += (s, e) => _navigation.Navigate<AssistantPage>();
        StatusStrip.MemoryClicked += (s, e) => _navigation.Navigate<SettingsPage>();

        // SessionSidebar events
        SessionSidebar.NewSessionRequested += OnNewSessionRequested;
        SessionSidebar.NewProjectRequested += OnNewProjectRequested;
        SessionSidebar.ProjectSelected += OnProjectSelected;
        SessionSidebar.SessionSelected += OnSessionSelected;
        SessionSidebar.SessionRenamed += OnSessionRenamed;
        SessionSidebar.SessionDeleteRequested += OnSessionDeleteRequested;
        SessionSidebar.SessionDuplicateRequested += OnSessionDuplicateRequested;
        SessionSidebar.SessionExportRequested += OnSessionExportRequested;
        SessionSidebar.RememberForProjectRequested += OnRememberForProject;
        SessionSidebar.RememberForSessionRequested += OnRememberForSession;
        SessionSidebar.ForgetMemoryRequested += OnForgetMemory;
        SessionSidebar.InstructionsChanged += OnInstructionsChanged;

        App.GetService<IChatService>().PersistenceFailed += (_, e) => ShowNotice(
            e.Operation == ConversationPersistenceOperation.Delete
                ? "That session could not be deleted. It is still on this PC."
                : "This chat could not be saved to disk. What is on screen will be missing after a restart.");

        // ConversationView InputComposer events
        ConversationView.Composer.ModelManagerRequested += (s, e) => _navigation.Navigate<ModelManagerPage>();
        ConversationView.Composer.RunRequested += OnRunRequested;
        ConversationView.Composer.CancelRequested += OnCancelRequested;
        ConversationView.Composer.AttachmentFailed += (_, message) => ShowNotice(message, InfoBarSeverity.Warning);
        ConversationView.Composer.WebSearchEnabled = WebSearchPreference.Load(App.GetService<IOptions<InferenceOptions>>().Value.WebSearch);
        ConversationView.Composer.WebSearchChanged += OnWebSearchChanged;

        // ConversationView speak events
        ConversationView.SpeakRequested += OnSpeakRequested;
        ConversationView.StopSpeakRequested += OnStopSpeakRequested;

        // ConversationView message delete events
        ConversationView.MessageDeleteRequested += OnMessageDeleteRequested;

        // Global keyboard shortcuts
        this.Content.KeyDown += OnGlobalKeyDown;

        // Escape accelerator — fires before control-level KeyDown so TextBox can't swallow it
        var escAccelerator = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escAccelerator.Invoked += OnEscapeAccelerator;
        if (this.Content is UIElement rootElement)
        {
            rootElement.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
            rootElement.KeyboardAccelerators.Add(escAccelerator);
        }
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_runCts != null && !_runCts.IsCancellationRequested)
        {
            OnCancelRequested(this, EventArgs.Empty);
            args.Handled = true;
        }
    }

    private void InitializeStatusStrip()
    {
        // Set initial status strip values
        StatusStrip.SetModelStatus(null, false);
        StatusStrip.SetDeviceStatus("GPU", true);
        StatusStrip.SetConnectivityStatus(false);
        StatusStrip.SetPolicyStatus(false);
        StatusStrip.SetAssistantStatus(true, "Assistant");
    }

    /// <summary>
    /// Loads available models from Ollama and populates the InputComposer dropdown.
    /// Filters out embedding-only models that cannot be used for chat.
    /// </summary>
    private async Task LoadModelsAsync()
    {
        try
        {
            var modelManager = App.GetService<IModelManager>();
            var allModels = await modelManager.ListModelsAsync();

            // Filter out embedding models — they can't be used for chat
            var chatModels = allModels
                .Where(m => !IsEmbeddingModel(m))
                .Select(m => m.Name)
                .ToList();

            // Update UI on dispatcher thread
            DispatcherQueue.TryEnqueue(() =>
            {
                ConversationView.SetAvailableModels(chatModels);

                // Select preferred model from settings, or fall back to first
                var settings = App.Services.GetService(typeof(ISettingsService)) as ISettingsService;
                var preferred = settings?.InferenceOptions.DefaultModel;
                if (!string.IsNullOrWhiteSpace(preferred))
                {
                    ConversationView.Composer.SelectModel(preferred);
                }

                var selected = ConversationView.Composer.SelectedModel
                    ?? (chatModels.Count > 0 ? chatModels[0] : null);
                if (selected != null)
                {
                    StatusStrip.SetModelStatus(selected, true);
                    StatusStrip.SetConnectivityStatus(_isOffline);
                }
            });
        }
        catch (Exception)
        {
            // Ollama not running — that's OK, user can open Model Manager
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusStrip.SetModelStatus(null, false);
                StatusStrip.SetConnectivityStatus(true);
            });
        }
    }

    /// <summary>
    /// Attempts to connect the voice service on startup.
    /// Voice is optional — failures are silently handled.
    /// </summary>
    private async Task InitializeVoiceAsync()
    {
        try
        {
            // Load the engine early only when its model is already here. A first run downloads
            // the model when a reply is spoken or Settings lists voices, not at launch.
            if (!File.Exists(KokoroVoiceService.CachedModelPath))
                return;

            var voiceService = App.GetService<IVoiceService>();
            await voiceService.ConnectAsync();
        }
        catch
        {
            // Voice engine not available — that's fine, it's optional
        }
    }

    /// <summary>
    /// Determines whether a model is an embedding-only model.
    /// </summary>
    private static bool IsEmbeddingModel(InControl.Core.Models.ModelInfo model)
    {
        // Check by family
        if (!string.IsNullOrEmpty(model.Family) && EmbeddingFamilies.Contains(model.Family))
        {
            return true;
        }

        // Check by name prefix
        var name = model.Name.ToLowerInvariant();
        foreach (var prefix in EmbeddingPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handles the Run button click from the InputComposer.
    /// Creates a conversation if needed, sends the message, and streams the response.
    /// </summary>
    private async void OnRunRequested(object? sender, RunRequestedEventArgs e)
    {
        if ((string.IsNullOrWhiteSpace(e.Intent) && e.Attachments.Count == 0) || string.IsNullOrWhiteSpace(e.Model))
            return;

        // One reply at a time in this window. The text they typed stays put.
        if (_runCts != null && !_runCts.IsCancellationRequested)
        {
            StatusStrip.SetAssistantStatus(true, "Another session is still answering");
            return;
        }

        // Attached text files go into the message. Images travel beside it for vision models.
        var content = AttachmentReader.Compose(e.Intent, e.Attachments);
        var images = AttachmentReader.ImagesOf(e.Attachments);
        var title = !string.IsNullOrWhiteSpace(e.Intent)
            ? e.Intent
            : string.Join(", ", e.Attachments.Select(a => a.Name));

        // Ask before sending. A text-only model refuses images, and the files stay attached.
        if (images is not null)
        {
            bool? sees;
            try
            {
                sees = await App.GetService<IInferenceClient>().SupportsImagesAsync(e.Model!);
            }
            catch (Exception)
            {
                sees = null;
            }

            if (sees == false)
            {
                ShowNotice($"{e.Model} can't read images. Pick a vision model, such as gemma3 or llama3.2-vision, or remove the image.", InfoBarSeverity.Warning);
                return;
            }
        }

        ConversationView.Composer.ClearAttachments();

        // Web search follows the toggle, and offline mode wins.
        var useWeb = ConversationView.Composer.WebSearchEnabled && !_isOffline;
        if (ConversationView.Composer.WebSearchEnabled && _isOffline)
            ShowNotice("Offline mode is on, so this reply can't search the web.", InfoBarSeverity.Informational);

        var runCts = new CancellationTokenSource();
        _runCts = runCts;
        _answerText.Clear();
        _answerPrompt = content;
        _answerModel = e.Model;

        var chatService = App.GetService<IChatService>();
        var model = e.Model;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Microsoft.UI.Dispatching.DispatcherQueueTimer? timer = null;
        string? completedContent = null;

        bool Viewing() => _answeringId is Guid answering
            && _conversationVm.GetConversation()?.Id == answering;

        try
        {
            if (ConversationView.ViewModel is null)
                ConversationView.ViewModel = _conversationVm;

            var conversation = _conversationVm.GetConversation();
            if (conversation is null)
            {
                var chatOptions = App.GetService<IOptions<ChatOptions>>();
                var systemPrompt = chatOptions?.Value.DefaultSystemPrompt;

                conversation = await chatService.CreateConversationAsync(
                    title: title.Length > 50 ? title[..50] + "..." : title,
                    model: model,
                    systemPrompt: systemPrompt,
                    projectId: _sessionListVm.SelectedProjectId,
                    ct: runCts.Token);

                _answeringId = conversation.Id;
                SetAnswering(conversation.Id);
                _sessionListVm.AddSession(conversation);
                SessionSidebar.RefreshVisualState();

                if (_conversationVm.GetConversation() is null)
                {
                    _conversationVm.LoadConversation(conversation);
                    SessionSidebar.SelectSession(conversation.Id);
                    _ = RefreshProjectMemoryAsync();
                }
            }
            else
            {
                _answeringId = conversation.Id;
                SetAnswering(conversation.Id);
            }

            if (Viewing())
            {
                _conversationVm.AddUserIntent(content);
                _conversationVm.ExecutionState = ExecutionState.Running;
                _conversationVm.CurrentModel = model;
                ConversationView.Composer.ExecutionState = ExecutionState.Running;
                ConversationView.Composer.IntentText = string.Empty;
                StatusStrip.SetModelStatus(model, true);
                await Task.Yield();
                if (Viewing())
                {
                    if (!_conversationVm.IsStreamingModelOutput)
                        _conversationVm.BeginModelOutput(model);

                    ConversationView.ShowMessages();
                    _conversationVm.ExecutionState = ExecutionState.Streaming;
                    ConversationView.Composer.ExecutionState = ExecutionState.Streaming;
                }
            }

            timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (_, _) =>
            {
                if (Viewing())
                    _conversationVm.ElapsedTime = stopwatch.Elapsed;
            };
            timer.Start();

            await Task.Yield();

            var yieldCounter = 0;

            var options = new SendOptions
            {
                Images = images,
                Tools = useWeb ? App.GetService<WebTools>().Tools : null,
                OnThinking = chunk => DispatcherQueue.TryEnqueue(() =>
                {
                    if (Viewing())
                        _conversationVm.AppendThinking(chunk);
                }),
                OnActivity = line => DispatcherQueue.TryEnqueue(() =>
                {
                    if (Viewing())
                        _conversationVm.AddActivity(line);
                })
            };

            await foreach (var token in chatService.SendMessageAsync(
                conversation.Id, content, options, runCts.Token))
            {
                _answerText.Append(token);
                if (!Viewing())
                    continue;

                _conversationVm.AppendToModelOutput(token);
                if (++yieldCounter % 3 == 0)
                {
                    ConversationView.ScrollToBottom();
                    await Task.Yield();
                }
            }

            completedContent = _answerText.ToString();

            if (Viewing())
            {
                var updated = await chatService.GetConversationAsync(conversation.Id);
                if (updated is not null && Viewing())
                {
                    _conversationVm.LoadConversationKeepingReplyExtras(updated);
                    ConversationView.ShowMessages();
                    ConversationView.ScrollToBottom();
                }

                _conversationVm.ExecutionState = ExecutionState.Complete;
                ConversationView.Composer.ExecutionState = ExecutionState.Idle;
            }

            UpdateSidebarSession(conversation.Id);
        }
        catch (OperationCanceledException)
        {
            if (Viewing())
            {
                _conversationVm.FinalizeModelOutput();
                _conversationVm.ExecutionState = ExecutionState.Cancelled;
                ConversationView.Composer.ExecutionState = ExecutionState.Idle;
            }
        }
        catch (Exception ex)
        {
            if (Viewing())
            {
                // A reply that never started leaves no empty card behind.
                if (_answerText.Length == 0)
                    _conversationVm.CancelModelOutput();
                else
                    _conversationVm.FinalizeModelOutput();
                _conversationVm.ExecutionState = ExecutionState.Issue;
                ConversationView.Composer.ExecutionState = ExecutionState.Idle;
            }

            ShowFailure("The reply failed", ex);
        }
        finally
        {
            timer?.Stop();
            stopwatch.Stop();
            var stillHere = Viewing();
            var finishedId = _answeringId;
            if (stillHere)
                _conversationVm.ElapsedTime = stopwatch.Elapsed;

            if (ReferenceEquals(_runCts, runCts))
            {
                _runCts.Dispose();
                _runCts = null;
            }
            else
            {
                runCts.Dispose();
            }

            _answeringId = null;
            SetAnswering(null);
            StatusStrip.SetAssistantStatus(true, "Assistant");
            AutoSpeakIfEnabled(completedContent);

            if (stillHere && finishedId is Guid id)
            {
                await Task.Delay(1500);
                if (_conversationVm.GetConversation()?.Id == id
                    && _conversationVm.ExecutionState is ExecutionState.Complete
                        or ExecutionState.Cancelled
                        or ExecutionState.Issue)
                {
                    _conversationVm.ExecutionState = ExecutionState.Idle;
                }
            }
        }
    }

    /// <summary>
    /// Updates a session item in the sidebar with the latest conversation data from ChatService.
    /// </summary>
    private async void UpdateSidebarSession(Guid conversationId)
    {
        try
        {
            var chatService = App.GetService<IChatService>();
            var updated = await chatService.GetConversationAsync(conversationId);
            if (updated is null) return;

            _sessionListVm.FindSession(conversationId)?.UpdateConversation(updated);
        }
        catch
        {
            // Non-critical — sidebar won't update but that's OK
        }
    }

    /// <summary>
    /// Auto-speaks the completed response if voice is configured and connected.
    /// </summary>
    private void AutoSpeakIfEnabled(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            var voiceOptions = App.GetService<IOptions<VoiceOptions>>();
            var voiceService = App.GetService<IVoiceService>();

            if (voiceOptions?.Value.AutoSpeak == true)
            {
                _ = voiceService.SpeakAsync(content);
            }
        }
        catch
        {
            // Voice is optional — never crash the app
        }
    }

    /// <summary>
    /// Handles the speak button click on a message card.
    /// Triggers lazy voice engine initialization if needed.
    /// </summary>
    private async void OnSpeakRequested(object? sender, InControl.ViewModels.MessageViewModel msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Content)) return;

        try
        {
            var voiceService = App.GetService<IVoiceService>();
            await voiceService.SpeakAsync(msg.Content);
        }
        catch (Exception ex)
        {
            ShowFailure("Could not read the reply aloud", ex);
        }
    }

    /// <summary>
    /// Handles the stop speaking button click.
    /// </summary>
    private async void OnStopSpeakRequested(object? sender, EventArgs e)
    {
        try
        {
            var voiceService = App.GetService<IVoiceService>();
            await voiceService.StopSpeakingAsync();
        }
        catch
        {
            // Voice is optional
        }
    }

    /// <summary>
    /// Handles the Cancel button click — stops the current generation.
    /// </summary>
    private void OnCancelRequested(object? sender, EventArgs e)
    {
        _runCts?.Cancel();

        if (_answeringId is Guid answeringId)
        {
            var chatService = App.GetService<IChatService>();
            chatService.StopGeneration(answeringId);
        }
    }

    /// <summary>
    /// Handles a session being selected in the sidebar.
    /// Loads the conversation into the conversation view.
    /// </summary>
    private async void OnSessionSelected(object? sender, Guid conversationId)
    {
        try
        {
            var chatService = App.GetService<IChatService>();
            var conversation = await chatService.GetConversationAsync(conversationId);

            if (conversation is null) return;

            // Navigate home if we're on a page
            _navigation.GoHome();

            // Load conversation into the view
            if (ConversationView.ViewModel is null)
            {
                ConversationView.ViewModel = _conversationVm;
            }

            ShowConversation(conversation);

            if (_answeringId == conversation.Id)
                StatusStrip.SetAssistantStatus(true, "This session is still answering");
            else if (_runCts != null && !_runCts.IsCancellationRequested)
                StatusStrip.SetAssistantStatus(true, "Another session is still answering");

            _ = RefreshProjectMemoryAsync();
        }
        catch (Exception ex)
        {
            ShowFailure("Could not open the chat", ex);
        }
    }

    /// <summary>
    /// Puts a stored conversation in the transcript. When that conversation is the one
    /// still answering, the reply so far comes back and new tokens keep painting.
    /// </summary>
    private void ShowConversation(Conversation conversation)
    {
        _conversationVm.LoadConversation(conversation);

        if (_answeringId == conversation.Id && _runCts != null && !_runCts.IsCancellationRequested)
        {
            var messages = conversation.Messages;
            if (_answerPrompt is not null
                && (messages.Count == 0 || messages[^1].Role != MessageRole.User))
            {
                _conversationVm.AddUserIntent(_answerPrompt);
            }

            _conversationVm.ReattachModelOutput(_answerModel, _answerText.ToString());
            _conversationVm.CurrentModel = _answerModel;
            _conversationVm.ExecutionState = ExecutionState.Streaming;
            ConversationView.Composer.ExecutionState = ExecutionState.Streaming;
            ConversationView.ShowMessages();
            ConversationView.ScrollToBottom();
            return;
        }

        _conversationVm.ExecutionState = ExecutionState.Idle;
        ConversationView.Composer.ExecutionState = ExecutionState.Idle;

        if (conversation.Messages.Count > 0)
            ConversationView.ShowMessages();
    }

    /// <summary>
    /// Handles a session rename request from the sidebar.
    /// </summary>
    private async void OnSessionRenamed(object? sender, (Guid Id, string NewTitle) args)
    {
        try
        {
            var chatService = App.GetService<IChatService>();
            var updated = await chatService.UpdateConversationAsync(args.Id, title: args.NewTitle);

            // Update sidebar item
            UpdateSidebarSession(args.Id);

            // Update current conversation view title if this is the active conversation
            var current = _conversationVm.GetConversation();
            if (current?.Id == args.Id)
            {
                ShowConversation(updated);
            }
        }
        catch (Exception ex)
        {
            ShowFailure("Could not rename the chat", ex);
        }
    }

    /// <summary>
    /// Remembers the web search toggle, in the live options and in saved settings.
    /// </summary>
    private void OnWebSearchChanged(object? sender, bool enabled)
    {
        App.GetService<IOptions<InferenceOptions>>().Value.WebSearch = enabled;
        if (!WebSearchPreference.Save(enabled))
            ShowNotice("The web search setting could not be saved. It applies until InControl closes.", InfoBarSeverity.Warning);

        if (enabled)
            ShowNotice("Web search is on. When the model searches, its query goes to DuckDuckGo. The reply lists each search.", InfoBarSeverity.Informational);
    }

    /// <summary>
    /// Stores a copy of a session and shows it at the top of the list.
    /// </summary>
    private async void OnSessionDuplicateRequested(object? sender, Guid conversationId)
    {
        try
        {
            var copy = await App.GetService<IChatService>().DuplicateConversationAsync(conversationId);
            if (copy is null)
            {
                ShowNotice("That session could not be copied.");
                return;
            }

            _sessionListVm.AddCopy(copy);
            SessionSidebar.RefreshVisualState();
        }
        catch (Exception ex)
        {
            ShowFailure("Could not copy that session", ex);
        }
    }

    /// <summary>
    /// Handles a session delete request from the sidebar.
    /// </summary>
    private async void OnSessionDeleteRequested(object? sender, Guid conversationId)
    {
        try
        {
            if (_answeringId == conversationId)
                OnCancelRequested(this, EventArgs.Empty);

            var chatService = App.GetService<IChatService>();
            // A false result keeps the session. PersistenceFailed has already told the user.
            if (!await chatService.DeleteConversationAsync(conversationId))
                return;

            // Remove from sidebar ViewModel
            SessionItemViewModel? toRemove = null;
            foreach (var s in _sessionListVm.Sessions)
            {
                if (s.Id == conversationId) { toRemove = s; break; }
            }
            if (toRemove is null)
            {
                foreach (var s in _sessionListVm.PinnedSessions)
                {
                    if (s.Id == conversationId) { toRemove = s; break; }
                }
            }
            if (toRemove is not null)
            {
                _sessionListVm.RemoveSession(toRemove);
                SessionSidebar.RefreshVisualState();
            }

            // If this was the active conversation, clear it
            var current = _conversationVm.GetConversation();
            if (current?.Id == conversationId)
            {
                _conversationVm.ClearConversation();
                ConversationView.Composer.Clear();
            }

            _ = RefreshProjectMemoryAsync();
        }
        catch (Exception ex)
        {
            ShowFailure("Could not delete the chat", ex);
        }
    }

    /// <summary>
    /// Handles a message delete request from the conversation view.
    /// </summary>
    private async void OnMessageDeleteRequested(object? sender, InControl.ViewModels.MessageViewModel msg)
    {
        var conversation = _conversationVm.GetConversation();
        if (conversation is null) return;

        try
        {
            var chatService = App.GetService<IChatService>();
            var updated = await chatService.RemoveMessageAsync(conversation.Id, msg.Id);

            // Remove from UI
            _conversationVm.RemoveMessage(msg.Id);

            // Update sidebar message count
            UpdateSidebarSession(conversation.Id);
        }
        catch (Exception ex)
        {
            ShowFailure("Could not delete the message", ex);
        }
    }

    /// <summary>
    /// Handles a session export request from the sidebar.
    /// </summary>
    private async void OnSessionExportRequested(object? sender, Guid conversationId)
    {
        try
        {
            var storage = App.GetService<IConversationStorage>();
            var json = await storage.ExportAsync(conversationId);

            // Copy to clipboard
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(json);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

            // Show confirmation
            if (this.Content is FrameworkElement root)
            {
                var dialog = new ContentDialog
                {
                    Title = "Exported",
                    Content = "Session JSON copied to clipboard.",
                    CloseButtonText = "OK",
                    XamlRoot = root.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            ShowFailure("Could not export the chat", ex);
        }
    }

    private void OnGlobalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+K - Command Palette
        if (e.Key == Windows.System.VirtualKey.K &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            ShowCommandPalette();
            e.Handled = true;
            return;
        }

        // Ctrl+B - Toggle Sidebar
        if (e.Key == Windows.System.VirtualKey.B &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            ToggleSidebar();
            e.Handled = true;
            return;
        }

        // Escape - Cancel execution, close overlays, or go back
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (_runCts != null && !_runCts.IsCancellationRequested)
            {
                // Cancel active generation first
                OnCancelRequested(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (CommandPaletteOverlay.Visibility == Visibility.Visible)
            {
                HideCommandPalette();
                e.Handled = true;
            }
            else if (_currentPage != null)
            {
                // Use backstack navigation instead of always going home
                _navigation.GoBack();
                e.Handled = true;
            }
        }

        // Alt+Left - Navigate back (standard Windows behavior)
        if (e.Key == Windows.System.VirtualKey.Left &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            if (_navigation.CanGoBack)
            {
                _navigation.GoBack();
                e.Handled = true;
            }
        }
    }

    private void OnCommandExecuted(object? sender, string commandId)
    {
        switch (commandId)
        {
            case "new-session":
                _navigation.GoHome();
                break;
            case "open-settings":
                _navigation.Navigate<SettingsPage>();
                break;
            case "open-model-manager":
                _navigation.Navigate<ModelManagerPage>();
                break;
            case "toggle-offline":
                ToggleOfflineMode();
                break;
            case "open-extensions":
                _navigation.Navigate<ExtensionsPage>();
                break;
            case "open-assistant":
                _navigation.Navigate<AssistantPage>();
                break;
            case "view-policy":
                _navigation.Navigate<PolicyPage>();
                break;
            case "open-connectivity":
                _navigation.Navigate<ConnectivityPage>();
                break;
            case "open-help":
                _navigation.Navigate<HelpPage>();
                break;
            case "toggle-sidebar":
                ToggleSidebar();
                break;
            case "search-sessions":
                _navigation.GoHome();
                // Focus session search
                break;
        }
    }

    private void OnNavigated(object? sender, NavigationEventArgs e)
    {
        if (e.PageType == null) return;
        NavigateToPageInternal(e.PageType);
    }

    private void OnNavigatedBack(object? sender, NavigationEventArgs e)
    {
        if (e.PageType == null)
        {
            NavigateHomeInternal();
            return;
        }
        NavigateToPageInternal(e.PageType);
    }

    private void NavigateToPageInternal(Type pageType)
    {
        // Remove current page if any
        if (_currentPage != null)
        {
            PageHost.Children.Remove(_currentPage);
        }

        // Create and add new page
        var page = (UserControl)Activator.CreateInstance(pageType)!;
        _currentPage = page;

        // Wire up back navigation and events
        WirePageEvents(page);

        PageHost.Children.Add(page);

        // Show page host, hide home view
        HomeView.Visibility = Visibility.Collapsed;
        PageHost.Visibility = Visibility.Visible;
    }

    private void WirePageEvents(UserControl page)
    {
        // All pages use NavigationService.GoBack() for consistent behavior
        if (page is SettingsPage settings)
        {
            settings.BackRequested += (s, e) => _navigation.GoBack();
            settings.ModelManagerRequested += (s, e) => _navigation.Navigate<ModelManagerPage>();
            settings.ExtensionsRequested += (s, e) => _navigation.Navigate<ExtensionsPage>();
            settings.PolicyRequested += (s, e) => _navigation.Navigate<PolicyPage>();
            settings.OfflineModeChanged += (_, isOffline) => SetOfflineMode(isOffline);
            settings.MemoryCleared += (_, _) => _ = RefreshProjectMemoryAsync();
            settings.IsOffline = _isOffline;
        }
        else if (page is ModelManagerPage modelManager)
        {
            modelManager.BackRequested += (s, e) =>
            {
                _navigation.GoBack();
                // Refresh model list when coming back from Model Manager
                _ = LoadModelsAsync();
            };
            modelManager.ModelSelected += OnModelSelected;
            modelManager.SetCurrentModel(ConversationView.Composer.SelectedModel);
        }
        else if (page is AssistantPage assistant)
        {
            assistant.BackRequested += (s, e) => _navigation.GoBack();
            assistant.MemoryCleared += (_, _) => _ = RefreshProjectMemoryAsync();
        }
        else if (page is ExtensionsPage extensions)
        {
            extensions.BackRequested += (s, e) => _navigation.GoBack();
        }
        else if (page is PolicyPage policy)
        {
            policy.BackRequested += (s, e) => _navigation.GoBack();
            policy.OfflineModeChanged += (s, isOffline) => SetOfflineMode(isOffline);
            policy.IsOffline = _isOffline;
        }
        else if (page is ConnectivityPage connectivity)
        {
            connectivity.BackRequested += (s, e) => _navigation.GoBack();
            connectivity.OfflineModeChanged += (s, isOffline) => SetOfflineMode(isOffline);
            connectivity.IsOffline = _isOffline;
        }
        else if (page is HelpPage help)
        {
            help.BackRequested += (s, e) => _navigation.GoBack();
            help.ModelManagerRequested += (s, e) => _navigation.Navigate<ModelManagerPage>();
        }
    }

    private void OnSidebarToggleClick(object sender, RoutedEventArgs e)
    {
        ToggleSidebar();
    }

    private void ToggleSidebar()
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;

        if (_isSidebarCollapsed)
        {
            SidebarColumn.MinWidth = 0;
            SidebarColumn.MaxWidth = 0;
            SidebarColumn.Width = new GridLength(0);
            SessionSidebar.Visibility = Visibility.Collapsed;
            SidebarToggleIcon.Glyph = "\uE76C"; // ChevronRight
            ToolTipService.SetToolTip(SidebarToggleButton, "Expand sidebar (Ctrl+B)");
        }
        else
        {
            SidebarColumn.MinWidth = 200;
            SidebarColumn.MaxWidth = 400;
            SidebarColumn.Width = new GridLength(280);
            SessionSidebar.Visibility = Visibility.Visible;
            SidebarToggleIcon.Glyph = "\uE76B"; // ChevronLeft
            ToolTipService.SetToolTip(SidebarToggleButton, "Collapse sidebar (Ctrl+B)");
        }
    }

    private void ToggleOfflineMode()
    {
        SetOfflineMode(!_isOffline);
    }

    private void SetOfflineMode(bool isOffline)
    {
        _isOffline = isOffline;
        AppBar.IsOffline = _isOffline;
        StatusStrip.SetConnectivityStatus(_isOffline);
        _ = App.GetService<ComputeSession>().SetOfflineAsync(isOffline);
    }

    private void ShowCommandPalette()
    {
        CommandPaletteOverlay.Visibility = Visibility.Visible;
        CommandPalette.Reset();
        CommandPalette.Focus();
    }

    private void HideCommandPalette()
    {
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnModelSelected(object? sender, string modelName)
    {
        AppBar.SetSelectedModel(modelName);
        ConversationView.Composer.SelectModel(modelName);
        StatusStrip.SetModelStatus(modelName, true);
    }

    private void OnNewSessionRequested(object? sender, EventArgs e)
    {
        // Clear the view only. The next send files a session into the selected project.
        _conversationVm.ClearConversation();
        ConversationView.Composer.Clear();
        SessionSidebar.ClearSessionSelection();
        _navigation.GoHome();
        _ = RefreshProjectMemoryAsync();

        // A reply that is still running keeps going. Escape stops it.
        if (_runCts != null && !_runCts.IsCancellationRequested)
            StatusStrip.SetAssistantStatus(true, "Another session is still answering");
    }

    private async void OnNewProjectRequested(object? sender, string name)
    {
        try
        {
            var library = App.GetService<IProjectLibrary>();
            var project = await library.CreateAsync(name);
            _sessionListVm.AddProject(project);
            SessionSidebar.SelectProject(project.Id);
            await RefreshProjectMemoryAsync();
        }
        catch (Exception ex)
        {
            ShowFailure("Could not create the project", ex);
        }
    }

    private void OnProjectSelected(object? sender, Guid id)
    {
        _ = RefreshProjectMemoryAsync();
    }

    private async void OnRememberForProject(object? sender, string text)
    {
        try
        {
            var memory = App.GetService<ISessionMemory>();
            var item = AssistantMemoryItem.Create(
                MemoryType.Fact,
                MemoryScope.User,
                MemorySource.ExplicitUser,
                NoteKey(text),
                text,
                projectId: _sessionListVm.SelectedProjectId);
            await memory.RememberAsync(item);
            await RefreshProjectMemoryAsync();
        }
        catch (Exception ex)
        {
            ShowFailure("Could not save the note", ex);
        }
    }

    private async void OnRememberForSession(object? sender, string text)
    {
        var session = _sessionListVm.SelectedSession;
        if (session is null)
            return;

        try
        {
            var memory = App.GetService<ISessionMemory>();
            var item = AssistantMemoryItem.Create(
                MemoryType.Fact,
                MemoryScope.Session,
                MemorySource.ExplicitUser,
                NoteKey(text),
                text,
                projectId: session.EffectiveProjectId,
                sessionId: session.Id);
            await memory.RememberAsync(item);
            await RefreshProjectMemoryAsync();
        }
        catch (Exception ex)
        {
            ShowFailure("Could not save the note", ex);
        }
    }

    private async void OnForgetMemory(object? sender, Guid id)
    {
        try
        {
            await App.GetService<ISessionMemory>().ForgetAsync(id);
            await RefreshProjectMemoryAsync();
        }
        catch (Exception ex)
        {
            ShowFailure("Could not forget the note", ex);
        }
    }

    private async void OnInstructionsChanged(object? sender, string text)
    {
        try
        {
            var library = App.GetService<IProjectLibrary>();
            await library.UpdateInstructionsAsync(_sessionListVm.SelectedProjectId, text);
        }
        catch (Exception ex)
        {
            ShowFailure("Could not save the instructions", ex);
        }
    }

    private async Task RefreshProjectMemoryAsync()
    {
        try
        {
            var library = App.GetService<IProjectLibrary>();
            var memory = App.GetService<ISessionMemory>();
            var projectId = _sessionListVm.SelectedProjectId;
            var sessionId = _sessionListVm.SelectedSession?.Id;
            var project = await library.FindAsync(projectId);
            var notes = await memory.ListAsync(projectId, sessionId);
            var items = ToNoteItems(notes);
            var instructions = project?.Instructions;
            var rememberSession = sessionId is not null;

            DispatcherQueue.TryEnqueue(() =>
            {
                SessionSidebar.ShowInstructions(projectId, instructions);
                SessionSidebar.ShowMemory(items);
                SessionSidebar.SetRememberSessionEnabled(rememberSession);
            });
        }
        catch (Exception ex)
        {
            ShowFailure("Could not load the notes", ex);
        }
    }

    private void SetAnswering(Guid? id)
    {
        foreach (var session in _sessionListVm.Sessions)
            session.IsAnswering = id is not null && session.Id == id;

        foreach (var session in _sessionListVm.PinnedSessions)
            session.IsAnswering = id is not null && session.Id == id;
    }

    private static string NoteKey(string text)
    {
        var line = text.Trim();
        var cut = line.IndexOfAny(['\r', '\n']);
        if (cut >= 0)
            line = line[..cut];

        if (line.Length > 48)
            line = line[..48];

        return line.Length == 0 ? "Note" : line;
    }

    private static IReadOnlyList<MemoryNoteItem> ToNoteItems(IReadOnlyList<AssistantMemoryItem> notes)
    {
        var items = new List<MemoryNoteItem>(notes.Count);
        foreach (var note in notes)
        {
            var scope = note.SessionId is null ? "Project" : "Session";
            items.Add(new MemoryNoteItem
            {
                Id = note.Id,
                Title = scope + " · " + note.Key,
                Detail = note.Value
            });
        }

        return items;
    }

    private void NavigateHomeInternal()
    {
        // Remove current page
        if (_currentPage != null)
        {
            PageHost.Children.Remove(_currentPage);
            _currentPage = null;
        }

        // Show home view, hide page host
        HomeView.Visibility = Visibility.Visible;
        PageHost.Visibility = Visibility.Collapsed;
    }
}
