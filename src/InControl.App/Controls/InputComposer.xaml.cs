using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using InControl.Core.UX;
using InControl.Core.Attachments;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace InControl.App.Controls;

/// <summary>
/// Input composer control panel for entering prompts and managing execution.
/// Follows the control panel metaphor - this is a command interface, not a chat box.
/// Provides disabled state explanations so users always know what to do next.
/// </summary>
public sealed partial class InputComposer : UserControl
{
    private ExecutionState _executionState = ExecutionState.Idle;
    private bool _isOfflineBlocked;
    private bool _isTransitioning;
    private object? _cachedSelectedModel;
    private readonly List<ChatAttachment> _attachments = [];

    /// <summary>
    /// Most files one message carries.
    /// </summary>
    private const int MaxAttachments = 10;

    public InputComposer()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    #region Properties

    /// <summary>
    /// The current execution state.
    /// </summary>
    public ExecutionState ExecutionState
    {
        get => _executionState;
        set
        {
            _executionState = value;
            UpdateUIForState();
        }
    }

    /// <summary>
    /// The current intent text.
    /// </summary>
    public string IntentText
    {
        get => IntentInput.Text;
        set => IntentInput.Text = value;
    }

    /// <summary>
    /// The selected model name.
    /// </summary>
    public string? SelectedModel => ModelSelector.SelectedItem as string;

    /// <summary>
    /// Whether the Run button is blocked due to offline policy.
    /// </summary>
    public bool IsOfflineBlocked
    {
        get => _isOfflineBlocked;
        set
        {
            _isOfflineBlocked = value;
            UpdateDisabledState();
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the user initiates a run.
    /// </summary>
    public event EventHandler<RunRequestedEventArgs>? RunRequested;

    /// <summary>
    /// Event raised when the user cancels execution.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Event raised when a file attachment is requested.
    /// </summary>
    public event EventHandler? AttachFileRequested;

    /// <summary>
    /// Event raised with a short message when a file could not be attached.
    /// </summary>
    public event EventHandler<string>? AttachmentFailed;

    /// <summary>
    /// Files attached to the next message.
    /// </summary>
    public IReadOnlyList<ChatAttachment> Attachments => _attachments;

    /// <summary>
    /// Event raised when Model Manager should open (from disabled banner).
    /// </summary>
    public event EventHandler? ModelManagerRequested;

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the available models for selection.
    /// </summary>
    public void SetAvailableModels(IEnumerable<string> models)
    {
        ModelSelector.Items.Clear();
        foreach (var model in models)
        {
            ModelSelector.Items.Add(model);
        }

        if (ModelSelector.Items.Count > 0)
        {
            ModelSelector.SelectedIndex = 0;
        }

        UpdateDisabledState();
    }

    /// <summary>
    /// Selects a model by name in the model selector dropdown.
    /// </summary>
    public void SelectModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;

        foreach (var item in ModelSelector.Items)
        {
            if (item is string s && s.Equals(modelName, StringComparison.OrdinalIgnoreCase))
            {
                ModelSelector.SelectedItem = item;
                return;
            }
        }
    }

    /// <summary>
    /// Updates the context count display.
    /// </summary>
    public void SetContextCount(int count)
    {
        if (count > 0)
        {
            ContextCountText.Text = count.ToString();
            ContextCountText.Visibility = Visibility.Visible;
            ClearContextMenuItem.IsEnabled = true;
        }
        else
        {
            ContextCountText.Visibility = Visibility.Collapsed;
            ClearContextMenuItem.IsEnabled = false;
        }
    }

    /// <summary>
    /// Clears the input and resets to idle state.
    /// </summary>
    public void Clear()
    {
        IntentInput.Text = string.Empty;
        ClearAttachments();
        ExecutionState = ExecutionState.Idle;
    }

    /// <summary>
    /// Removes every attached file.
    /// </summary>
    public void ClearAttachments()
    {
        _attachments.Clear();
        RenderAttachments();
    }

    /// <summary>
    /// Focuses the input area.
    /// </summary>
    public void FocusInput()
    {
        IntentInput.Focus(FocusState.Programmatic);
    }

    #endregion

    #region Private Methods

    private void SetupEventHandlers()
    {
        IntentInput.TextChanged += OnIntentTextChanged;
        IntentInput.KeyDown += OnIntentInputKeyDown;
        ActionButton.Click += OnActionButtonClick;
        AttachFileButton.Click += OnAttachFileButtonClick;
        ComposerRoot.DragOver += OnComposerDragOver;
        ComposerRoot.Drop += OnComposerDrop;
        ModelSelector.SelectionChanged += OnModelSelectionChanged;
        DisabledActionButton.Click += OnDisabledActionClick;

        // Context menu items
        AddPreviousOutputMenuItem.Click += OnAddPreviousOutputClick;
        AddFileMenuItem.Click += OnAddFileClick;
        ClearContextMenuItem.Click += OnClearContextClick;

        // Initial tooltip state
        UpdateTooltips();
    }

    /// <summary>
    /// Handles Enter to send and Shift+Enter for new line.
    /// </summary>
    private void OnIntentInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;

        var shiftDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
            Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (shiftDown)
        {
            // Shift+Enter → insert newline manually
            e.Handled = true;
            var textBox = (TextBox)sender;
            var selStart = textBox.SelectionStart;
            var text = textBox.Text;
            textBox.Text = text.Insert(selStart, "\r\n");
            textBox.SelectionStart = selStart + 2;
            return;
        }

        // Bare Enter → send
        e.Handled = true;

        if (CanRun())
        {
            var args = new RunRequestedEventArgs(IntentInput.Text.TrimEnd('\r', '\n'), SelectedModel, _attachments.ToList());
            RunRequested?.Invoke(this, args);
        }
    }

    private void OnIntentTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isTransitioning || _executionState.IsExecuting()) return;

        var text = IntentInput.Text;
        CharacterCount.Text = $"{text.Length} characters";

        UpdateActionButtonForRun();
        UpdateDisabledState();
    }

    private void OnActionButtonClick(object sender, RoutedEventArgs e)
    {
        if (_executionState.IsExecuting())
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (CanRun())
        {
            var args = new RunRequestedEventArgs(IntentInput.Text, SelectedModel, _attachments.ToList());
            RunRequested?.Invoke(this, args);
        }
    }

    private async void OnAttachFileButtonClick(object sender, RoutedEventArgs e)
    {
        AttachFileRequested?.Invoke(this, EventArgs.Empty);
        await PickFilesAsync();
    }

    private async System.Threading.Tasks.Task PickFilesAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");

            // A desktop WinUI window has to hand its handle to the picker.
            if (App.MainWindow is { } window)
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));

            var files = await picker.PickMultipleFilesAsync();
            if (files is null)
                return;

            foreach (var file in files)
                await AddFileAsync(file);
        }
        catch (Exception ex)
        {
            AttachmentFailed?.Invoke(this, $"Could not open the file picker: {ex.Message}");
        }
    }

    private void OnComposerDragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems) || !_executionState.AllowsInput())
            return;

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Attach";
    }

    private async void OnComposerDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                    await AddFileAsync(file);
                else
                    AttachmentFailed?.Invoke(this, $"{item.Name} is a folder. Attach the files inside it.");
            }
        }
        catch (Exception ex)
        {
            AttachmentFailed?.Invoke(this, $"Could not attach the dropped files: {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async System.Threading.Tasks.Task AddFileAsync(StorageFile file)
    {
        if (_attachments.Count >= MaxAttachments)
        {
            AttachmentFailed?.Invoke(this, $"A message can carry {MaxAttachments} files. {file.Name} was not attached.");
            return;
        }

        if (string.IsNullOrEmpty(file.Path))
        {
            AttachmentFailed?.Invoke(this, $"{file.Name} is not a file on this PC, so it could not be read.");
            return;
        }

        var path = file.Path;
        var result = await System.Threading.Tasks.Task.Run(() => AttachmentReader.ReadFile(path));
        if (result.Attachment is not { } attachment)
        {
            AttachmentFailed?.Invoke(this, result.Error ?? $"{file.Name} could not be attached.");
            return;
        }

        _attachments.Add(attachment);
        RenderAttachments();
    }

    private void RenderAttachments()
    {
        AttachmentChips.Children.Clear();
        foreach (var attachment in _attachments)
        {
            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            label.Children.Add(new FontIcon
            {
                Glyph = attachment.Kind == AttachmentKind.Image ? "\uEB9F" : "\uE8A5",
                FontSize = 12
            });
            label.Children.Add(new TextBlock
            {
                Text = attachment.Name,
                MaxWidth = 220,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            label.Children.Add(new FontIcon { Glyph = "\uE711", FontSize = 10 });

            var chip = new Button { Content = label, Padding = new Thickness(8, 4, 8, 4), Tag = attachment };
            ToolTipService.SetToolTip(chip, $"Remove {attachment.Name}");
            AutomationProperties.SetName(chip, $"Remove attached file {attachment.Name}");
            chip.Click += (s, _) =>
            {
                if (s is Button { Tag: ChatAttachment item })
                {
                    _attachments.Remove(item);
                    RenderAttachments();
                }
            };
            AttachmentChips.Children.Add(chip);
        }

        UpdateActionButtonForRun();
        UpdateDisabledState();
    }

    private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isTransitioning || _executionState.IsExecuting()) return;

        UpdateActionButtonForRun();
        UpdateDisabledState();
    }

    private void OnDisabledActionClick(object sender, RoutedEventArgs e)
    {
        ModelManagerRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnAddPreviousOutputClick(object sender, RoutedEventArgs e)
    {
        await ShowNotImplementedDialog("Add Previous Output");
    }

    private async void OnAddFileClick(object sender, RoutedEventArgs e)
    {
        AttachFileRequested?.Invoke(this, EventArgs.Empty);
        await PickFilesAsync();
    }

    private void OnClearContextClick(object sender, RoutedEventArgs e)
    {
        // Clear context logic - will reset context count
        SetContextCount(0);
    }

    private async System.Threading.Tasks.Task ShowNotImplementedDialog(string feature)
    {
        var dialog = new ContentDialog
        {
            Title = "Coming Soon",
            Content = $"{feature} will be available in a future update.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void UpdateUIForState()
    {
        var isExecuting = _executionState.IsExecuting();
        var allowsInput = _executionState.AllowsInput();

        _isTransitioning = true;
        try
        {
            if (isExecuting)
            {
                SetCancelMode();
            }
            else
            {
                SetRunMode();
            }

            _cachedSelectedModel = ModelSelector.SelectedItem;

            IntentInput.IsEnabled = allowsInput;
            ModelSelector.IsEnabled = allowsInput;

            if (!allowsInput && ModelSelector.SelectedItem == null && _cachedSelectedModel != null)
            {
                ModelSelector.SelectedItem = _cachedSelectedModel;
            }

            AttachFileButton.IsEnabled = allowsInput;
            ContextMenuButton.IsEnabled = allowsInput;
        }
        finally
        {
            _isTransitioning = false;
        }

        if (!isExecuting)
        {
            UpdateActionButtonForRun();
        }

        UpdateDisabledState();
    }

    private void SetCancelMode()
    {
        ActionButtonIcon.Glyph = "\uE711"; // Cancel X
        ActionButtonIcon.FontSize = 12;
        ActionButtonText.Text = "Cancel";
        ActionButton.IsEnabled = true;
        ToolTipService.SetToolTip(ActionButton, "Cancel execution (Escape)");
        AutomationProperties.SetName(ActionButton, "Cancel");

        // Force WinUI to process the visual change
        ActionButton.InvalidateArrange();
        ActionButton.InvalidateMeasure();
        ActionButton.UpdateLayout();
    }

    private void SetRunMode()
    {
        ActionButtonIcon.Glyph = "\uE768"; // Play
        ActionButtonIcon.FontSize = 14;
        ActionButtonText.Text = "Run";
        AutomationProperties.SetName(ActionButton, "Run");
    }

    private void UpdateActionButtonForRun()
    {
        var hasText = HasContent();
        var hasModel = ModelSelector.SelectedItem != null;
        var allowsInput = _executionState.AllowsInput();

        ActionButton.IsEnabled = hasText && hasModel && allowsInput && !_isOfflineBlocked;
        UpdateTooltips();
    }

    private void UpdateDisabledState()
    {
        string? reason = null;
        string? actionText = null;
        bool showAction = false;

        if (_isOfflineBlocked)
        {
            reason = "Run is blocked by connectivity policy";
        }
        else if (ModelSelector.SelectedItem == null && ModelSelector.Items.Count == 0)
        {
            reason = "No models available. Add a model to get started.";
            actionText = "Open Model Manager";
            showAction = true;
        }

        if (reason != null)
        {
            DisabledBanner.Visibility = Visibility.Visible;
            DisabledReasonText.Text = reason;
            DisabledActionButton.Content = actionText;
            DisabledActionButton.Visibility = showAction ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            DisabledBanner.Visibility = Visibility.Collapsed;
        }

        UpdateTooltips();
    }

    private void UpdateTooltips()
    {
        if (_executionState.IsExecuting() || _isTransitioning) return;

        if (ActionButton.IsEnabled)
        {
            ToolTipService.SetToolTip(ActionButton, "Run inference (Enter)");
        }
        else
        {
            var hasModel = ModelSelector.SelectedItem != null;
            var hasText = HasContent();

            if (_isOfflineBlocked)
            {
                ToolTipService.SetToolTip(ActionButton, "Blocked by connectivity policy");
            }
            else if (!hasModel)
            {
                ToolTipService.SetToolTip(ActionButton, "Select a model to enable");
            }
            else if (!hasText)
            {
                ToolTipService.SetToolTip(ActionButton, "Type a prompt to enable");
            }
            else
            {
                ToolTipService.SetToolTip(ActionButton, "Waiting for current operation");
            }
        }

        if (ClearContextMenuItem.IsEnabled)
        {
            ToolTipService.SetToolTip(ClearContextMenuItem, "Remove all context items");
        }
        else
        {
            ToolTipService.SetToolTip(ClearContextMenuItem, "No context items to clear");
        }
    }

    /// <summary>
    /// A prompt, or at least one attached file, is something to send.
    /// </summary>
    private bool HasContent() => !string.IsNullOrWhiteSpace(IntentInput.Text) || _attachments.Count > 0;

    private bool CanRun()
    {
        return HasContent()
            && ModelSelector.SelectedItem != null
            && _executionState.AllowsInput()
            && !_isOfflineBlocked;
    }

    #endregion
}

/// <summary>
/// Event arguments for run requests.
/// </summary>
public sealed class RunRequestedEventArgs : EventArgs
{
    public RunRequestedEventArgs(string intent, string? model, IReadOnlyList<ChatAttachment>? attachments = null)
    {
        Intent = intent;
        Model = model;
        Attachments = attachments ?? [];
    }

    /// <summary>
    /// Files attached to this message.
    /// </summary>
    public IReadOnlyList<ChatAttachment> Attachments { get; }

    /// <summary>
    /// The user's intent text.
    /// </summary>
    public string Intent { get; }

    /// <summary>
    /// The selected model name.
    /// </summary>
    public string? Model { get; }
}
