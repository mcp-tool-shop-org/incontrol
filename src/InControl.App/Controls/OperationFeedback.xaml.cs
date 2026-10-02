using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace InControl.App.Controls;

/// <summary>
/// Inline feedback for operation results.
/// Provides consistent success/error/warning/info messages.
/// </summary>
public sealed partial class OperationFeedback : UserControl
{
    private DispatcherTimer? _autoDismissTimer;

    public OperationFeedback()
    {
        this.InitializeComponent();
        DismissButton.Click += (s, e) => Hide();
    }

    #region Dependency Properties

    /// <summary>
    /// Duration in milliseconds before auto-dismissing (default: 3000).
    /// Set to 0 to disable auto-dismiss.
    /// </summary>
    public int AutoDismissDelay
    {
        get => (int)GetValue(AutoDismissDelayProperty);
        set => SetValue(AutoDismissDelayProperty, value);
    }

    public static readonly DependencyProperty AutoDismissDelayProperty =
        DependencyProperty.Register(
            nameof(AutoDismissDelay),
            typeof(int),
            typeof(OperationFeedback),
            new PropertyMetadata(3000));

    /// <summary>
    /// Whether to show the dismiss button.
    /// </summary>
    public bool ShowDismissButton
    {
        get => (bool)GetValue(ShowDismissButtonProperty);
        set => SetValue(ShowDismissButtonProperty, value);
    }

    public static readonly DependencyProperty ShowDismissButtonProperty =
        DependencyProperty.Register(
            nameof(ShowDismissButton),
            typeof(bool),
            typeof(OperationFeedback),
            new PropertyMetadata(false, OnShowDismissButtonChanged));

    #endregion

    #region Public Methods

    /// <summary>
    /// Show success feedback.
    /// </summary>
    public void ShowSuccess(string message)
    {
        Show(message, FeedbackType.Success);
    }

    /// <summary>
    /// Show error feedback.
    /// </summary>
    public void ShowError(string message)
    {
        Show(message, FeedbackType.Error);
    }

    /// <summary>
    /// Show warning feedback.
    /// </summary>
    public void ShowWarning(string message)
    {
        Show(message, FeedbackType.Warning);
    }

    /// <summary>
    /// Show info feedback.
    /// </summary>
    public void ShowInfo(string message)
    {
        Show(message, FeedbackType.Info);
    }

    /// <summary>
    /// Show "Copied!" feedback for copy operations.
    /// </summary>
    public void ShowCopied()
    {
        ShowSuccess("Copied to clipboard");
    }

    /// <summary>
    /// Show "Saved" feedback for save operations.
    /// </summary>
    public void ShowSaved()
    {
        ShowSuccess("Saved");
    }

    /// <summary>
    /// Hide the feedback.
    /// </summary>
    public void Hide()
    {
        StopAutoDismissTimer();
        AnimateOut();
    }

    #endregion

    #region Private Methods

    private void Show(string message, FeedbackType type)
    {
        StopAutoDismissTimer();

        MessageText.Text = message;
        ApplyTypeStyle(type);

        FeedbackBorder.Visibility = Visibility.Visible;
        AnimateIn();

        if (AutoDismissDelay > 0)
        {
            StartAutoDismissTimer();
        }
    }

    private void ApplyTypeStyle(FeedbackType type)
    {
        var (glyph, foreground, background) = type switch
        {
            FeedbackType.Success => ("\uE73E", "SystemFillColorSuccessBrush", "SystemFillColorSuccessBackgroundBrush"),
            FeedbackType.Error => ("\uE783", "SystemFillColorCriticalBrush", "SystemFillColorCriticalBackgroundBrush"),
            FeedbackType.Warning => ("\uE7BA", "SystemFillColorCautionBrush", "SystemFillColorCautionBackgroundBrush"),
            FeedbackType.Info => ("\uE946", "TextFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush"),
            _ => ("\uE946", "TextFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush")
        };

        StatusIcon.Glyph = glyph;
        StatusIcon.Foreground = GetBrush(foreground);
        MessageText.Foreground = GetBrush(foreground);
        FeedbackBorder.Background = GetBrush(background);
    }

    private void AnimateIn()
    {
        FeedbackBorder.Opacity = 0;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeIn);
        Storyboard.SetTarget(fadeIn, FeedbackBorder);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        storyboard.Begin();
    }

    private void AnimateOut()
    {
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeOut);
        Storyboard.SetTarget(fadeOut, FeedbackBorder);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyboard.Completed += (s, e) =>
        {
            FeedbackBorder.Visibility = Visibility.Collapsed;
        };
        storyboard.Begin();
    }

    private void StartAutoDismissTimer()
    {
        _autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoDismissDelay)
        };
        _autoDismissTimer.Tick += (s, e) =>
        {
            StopAutoDismissTimer();
            Hide();
        };
        _autoDismissTimer.Start();
    }

    private void StopAutoDismissTimer()
    {
        _autoDismissTimer?.Stop();
        _autoDismissTimer = null;
    }

    private static void OnShowDismissButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OperationFeedback feedback)
        {
            feedback.DismissButton.Visibility = (bool)e.NewValue
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
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

    private enum FeedbackType
    {
        Success,
        Error,
        Warning,
        Info
    }
}
