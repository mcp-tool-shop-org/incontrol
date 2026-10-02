using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using InControl.Core.UX;
using InControl.App.Services;

namespace InControl.App.Controls;

/// <summary>
/// Top application bar with app name, model selector, status capsule, global search, and control bar.
/// Provides one-click access to all major subsystems.
/// </summary>
public sealed partial class AppBar : UserControl
{
    private ExecutionState _executionState = ExecutionState.Idle;
    private string? _selectedModel;
    private bool _isOffline;

    public AppBar()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    #region Events

    /// <summary>
    /// Event raised when cancel is requested.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Event raised when command palette (Ctrl+K) is requested.
    /// </summary>
    public event EventHandler? CommandPaletteRequested;

    /// <summary>
    /// Event raised when Model Manager should be opened.
    /// </summary>
    public event EventHandler? ModelManagerRequested;

    /// <summary>
    /// Event raised when Settings should be opened.
    /// </summary>
    public event EventHandler? SettingsRequested;

    /// <summary>
    /// Event raised when Assistant panel should be opened.
    /// </summary>
    public event EventHandler? AssistantRequested;

    /// <summary>
    /// Event raised when Extensions panel should be opened.
    /// </summary>
    public event EventHandler? ExtensionsRequested;

    /// <summary>
    /// Event raised when Policy panel should be opened.
    /// </summary>
    public event EventHandler? PolicyRequested;

    /// <summary>
    /// Event raised when Connectivity panel should be opened.
    /// </summary>
    public event EventHandler? ConnectivityRequested;

    /// <summary>
    /// Event raised when Help should be opened.
    /// </summary>
    public event EventHandler? HelpRequested;

    /// <summary>
    /// Event raised when the app name/logo is clicked to navigate home.
    /// </summary>
    public event EventHandler? HomeRequested;

    #endregion

    #region Properties

    /// <summary>
    /// The current execution state.
    /// </summary>
    public ExecutionState ExecutionState
    {
        get => _executionState;
        set
        {
            if (_executionState != value)
            {
                _executionState = value;
                UpdateStatusDisplay();
            }
        }
    }

    /// <summary>
    /// Gets the currently selected model.
    /// </summary>
    public string? SelectedModel => _selectedModel;

    /// <summary>
    /// Gets or sets whether the app is in offline mode.
    /// </summary>
    public bool IsOffline
    {
        get => _isOffline;
        set
        {
            _isOffline = value;
            UpdateConnectivityIcon();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the selected model display.
    /// </summary>
    /// <summary>
    /// Shows the leave-the-PC line while a rented GPU is the chat target.
    /// Local stays unmarked here. Settings still says prompts stay on this PC.
    /// </summary>
    public void SetComputeNotice(bool promptsLeaveThisPc, string notice)
    {
        RemoteBannerText.Text = notice;
        RemoteBanner.Visibility = promptsLeaveThisPc ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetSelectedModel(string? modelName)
    {
        _selectedModel = modelName;
        ModelDisplayText.Text = string.IsNullOrEmpty(modelName)
            ? "Open Model Manager..."
            : modelName;
    }

    /// <summary>
    /// Updates the elapsed time display.
    /// </summary>
    public void UpdateElapsedTime(string elapsedTimeText)
    {
        ElapsedTimeText.Text = elapsedTimeText;
    }

    #endregion

    #region Private Methods

    private void SetupEventHandlers()
    {
        HomeButton.Click += OnHomeClick;
        CancelButton.Click += OnCancelClick;
        SearchButton.Click += OnSearchClick;
        ModelManagerButton.Click += OnModelManagerClick;
        ThemeToggleButton.Click += OnThemeToggleClick;
        SettingsButton.Click += OnSettingsClick;
        AssistantButton.Click += OnAssistantClick;
        ExtensionsButton.Click += OnExtensionsClick;
        PolicyButton.Click += OnPolicyClick;
        ConnectivityButton.Click += OnConnectivityClick;
        HelpButton.Click += OnHelpClick;

        // Subscribe to theme changes
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
        UpdateThemeIcon();
    }

    private void UpdateStatusDisplay()
    {
        var isExecuting = _executionState.IsExecuting();

        // Toggle state visibility
        IdleState.Visibility = isExecuting ? Visibility.Collapsed : Visibility.Visible;
        ExecutingState.Visibility = isExecuting ? Visibility.Visible : Visibility.Collapsed;
        ExecutingProgress.IsActive = isExecuting;

        // Show/hide elapsed time and cancel
        ElapsedTimeText.Visibility = isExecuting ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Visibility = _executionState.CanCancel() ? Visibility.Visible : Visibility.Collapsed;

        // Update text and colors
        if (isExecuting)
        {
            ExecutingText.Text = _executionState.ToCapsuleText();
        }
        else
        {
            StatusText.Text = _executionState.ToCapsuleText();
            StatusIndicator.Fill = GetStatusBrush(_executionState);
        }
    }

    private void UpdateConnectivityIcon()
    {
        // E701 = Globe (online), E8CD = Airplane (offline)
        ConnectivityIcon.Glyph = _isOffline ? "\uE8CD" : "\uE701";
    }

    private Brush GetStatusBrush(ExecutionState state)
    {
        var resourceKey = state switch
        {
            ExecutionState.Complete => "SystemFillColorSuccessBrush",
            ExecutionState.Issue => "SystemFillColorCriticalBrush",
            ExecutionState.Cancelled => "SystemFillColorNeutralBrush",
            _ => "SystemFillColorSuccessBrush"
        };

        if (Application.Current.Resources.TryGetValue(resourceKey, out var brush) && brush is Brush b)
            return b;

        return new SolidColorBrush(Microsoft.UI.Colors.Green);
    }

    #endregion

    #region Event Handlers

    private void OnHomeClick(object sender, RoutedEventArgs e)
    {
        HomeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchClick(object sender, RoutedEventArgs e)
    {
        CommandPaletteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnModelManagerClick(object sender, RoutedEventArgs e)
    {
        ModelManagerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnAssistantClick(object sender, RoutedEventArgs e)
    {
        AssistantRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExtensionsClick(object sender, RoutedEventArgs e)
    {
        ExtensionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPolicyClick(object sender, RoutedEventArgs e)
    {
        PolicyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnConnectivityClick(object sender, RoutedEventArgs e)
    {
        ConnectivityRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnHelpClick(object sender, RoutedEventArgs e)
    {
        HelpRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        ThemeService.Instance.ToggleTheme();
    }

    private void OnThemeChanged(object? sender, ElementTheme theme)
    {
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        ThemeIcon.Glyph = ThemeService.Instance.GetThemeIcon();

        // Update tooltip to show current state
        var themeText = ThemeService.Instance.CurrentThemeString;
        ToolTipService.SetToolTip(ThemeToggleButton, $"Toggle theme (current: {themeText})");
    }

    #endregion
}
