using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace InControl.App.Controls;

/// <summary>
/// Unified status indicator for loading/success/error/warning states.
/// Provides consistent visual feedback across the application.
/// </summary>
public sealed partial class StatusIndicator : UserControl
{
    private DispatcherTimer? _autoHideTimer;

    public StatusIndicator()
    {
        this.InitializeComponent();
    }

    #region Dependency Properties

    /// <summary>
    /// The current status state.
    /// </summary>
    public IndicatorStatus Status
    {
        get => (IndicatorStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status),
            typeof(IndicatorStatus),
            typeof(StatusIndicator),
            new PropertyMetadata(IndicatorStatus.Hidden, OnStatusChanged));

    /// <summary>
    /// The message to display alongside the indicator.
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(StatusIndicator),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    /// <summary>
    /// Whether to auto-hide after showing success/info states.
    /// </summary>
    public bool AutoHide
    {
        get => (bool)GetValue(AutoHideProperty);
        set => SetValue(AutoHideProperty, value);
    }

    public static readonly DependencyProperty AutoHideProperty =
        DependencyProperty.Register(
            nameof(AutoHide),
            typeof(bool),
            typeof(StatusIndicator),
            new PropertyMetadata(false));

    /// <summary>
    /// Duration in milliseconds before auto-hiding (default: 3000).
    /// </summary>
    public int AutoHideDelay
    {
        get => (int)GetValue(AutoHideDelayProperty);
        set => SetValue(AutoHideDelayProperty, value);
    }

    public static readonly DependencyProperty AutoHideDelayProperty =
        DependencyProperty.Register(
            nameof(AutoHideDelay),
            typeof(int),
            typeof(StatusIndicator),
            new PropertyMetadata(3000));

    /// <summary>
    /// Size variant for the indicator.
    /// </summary>
    public IndicatorSize Size
    {
        get => (IndicatorSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(
            nameof(Size),
            typeof(IndicatorSize),
            typeof(StatusIndicator),
            new PropertyMetadata(IndicatorSize.Medium, OnSizeChanged));

    #endregion

    #region Public Methods

    /// <summary>
    /// Show loading state with message.
    /// </summary>
    public void ShowLoading(string message = "Loading...")
    {
        Message = message;
        Status = IndicatorStatus.Loading;
    }

    /// <summary>
    /// Show success state with message. Auto-hides if AutoHide is true.
    /// </summary>
    public void ShowSuccess(string message = "Complete")
    {
        Message = message;
        Status = IndicatorStatus.Success;
        StartAutoHideTimer();
    }

    /// <summary>
    /// Show error state with message.
    /// </summary>
    public void ShowError(string message = "Error occurred")
    {
        Message = message;
        Status = IndicatorStatus.Error;
    }

    /// <summary>
    /// Show warning state with message.
    /// </summary>
    public void ShowWarning(string message = "Warning")
    {
        Message = message;
        Status = IndicatorStatus.Warning;
    }

    /// <summary>
    /// Show info state with message.
    /// </summary>
    public void ShowInfo(string message = "")
    {
        Message = message;
        Status = IndicatorStatus.Info;
        StartAutoHideTimer();
    }

    /// <summary>
    /// Hide the indicator.
    /// </summary>
    public void Hide()
    {
        Status = IndicatorStatus.Hidden;
    }

    #endregion

    #region Private Methods

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusIndicator indicator)
        {
            indicator.UpdateVisualState();
        }
    }

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusIndicator indicator)
        {
            indicator.MessageText.Text = (string)e.NewValue;
        }
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusIndicator indicator)
        {
            indicator.UpdateSize();
        }
    }

    private void UpdateVisualState()
    {
        StopAutoHideTimer();

        // Hide all first
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        StateIcon.Visibility = Visibility.Collapsed;
        SuccessCheckContainer.Visibility = Visibility.Collapsed;

        switch (Status)
        {
            case IndicatorStatus.Loading:
                RootGrid.Visibility = Visibility.Visible;
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                break;

            case IndicatorStatus.Success:
                RootGrid.Visibility = Visibility.Visible;
                ShowSuccessAnimation();
                break;

            case IndicatorStatus.Error:
                RootGrid.Visibility = Visibility.Visible;
                StateIcon.Glyph = "\uE783"; // ErrorBadge
                StateIcon.Foreground = GetBrush("SystemFillColorCriticalBrush");
                StateIcon.Visibility = Visibility.Visible;
                MessageText.Foreground = GetBrush("SystemFillColorCriticalBrush");
                break;

            case IndicatorStatus.Warning:
                RootGrid.Visibility = Visibility.Visible;
                StateIcon.Glyph = "\uE7BA"; // Warning
                StateIcon.Foreground = GetBrush("SystemFillColorCautionBrush");
                StateIcon.Visibility = Visibility.Visible;
                MessageText.Foreground = GetBrush("SystemFillColorCautionBrush");
                break;

            case IndicatorStatus.Info:
                RootGrid.Visibility = Visibility.Visible;
                StateIcon.Glyph = "\uE946"; // Info
                StateIcon.Foreground = GetBrush("TextFillColorSecondaryBrush");
                StateIcon.Visibility = Visibility.Visible;
                MessageText.Foreground = GetBrush("TextFillColorSecondaryBrush");
                break;

            case IndicatorStatus.Hidden:
            default:
                RootGrid.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void ShowSuccessAnimation()
    {
        SuccessCheckContainer.Visibility = Visibility.Visible;
        MessageText.Foreground = GetBrush("SystemFillColorSuccessBrush");

        // Animate the success checkmark appearing
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeIn);
        Storyboard.SetTarget(fadeIn, SuccessCheckContainer);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        storyboard.Begin();
    }

    private void UpdateSize()
    {
        var (ringSize, iconSize, textStyle) = Size switch
        {
            IndicatorSize.Small => (12.0, 12.0, "CaptionTextBlockStyle"),
            IndicatorSize.Large => (24.0, 24.0, "BodyStrongTextBlockStyle"),
            _ => (16.0, 16.0, "BodyTextBlockStyle")
        };

        LoadingRing.Width = ringSize;
        LoadingRing.Height = ringSize;
        StateIcon.FontSize = iconSize;

        if (Resources.TryGetValue(textStyle, out var style) && style is Style s)
        {
            MessageText.Style = s;
        }
    }

    private void StartAutoHideTimer()
    {
        if (!AutoHide) return;
        if (Status != IndicatorStatus.Success && Status != IndicatorStatus.Info) return;

        StopAutoHideTimer();

        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoHideDelay)
        };
        _autoHideTimer.Tick += (s, e) =>
        {
            StopAutoHideTimer();
            Hide();
        };
        _autoHideTimer.Start();
    }

    private void StopAutoHideTimer()
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
    }

    private Brush GetBrush(string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) && resource is Brush brush)
        {
            return brush;
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    #endregion
}

/// <summary>
/// Status states for the indicator.
/// </summary>
public enum IndicatorStatus
{
    Hidden,
    Loading,
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// Size variants for the indicator.
/// </summary>
public enum IndicatorSize
{
    Small,
    Medium,
    Large
}
