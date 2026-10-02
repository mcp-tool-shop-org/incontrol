using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace InControl.App.Controls;

/// <summary>
/// Full-area loading overlay for page/section loading states.
/// </summary>
public sealed partial class LoadingOverlay : UserControl
{
    public LoadingOverlay()
    {
        this.InitializeComponent();
    }

    #region Dependency Properties

    /// <summary>
    /// Primary loading message.
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
            typeof(LoadingOverlay),
            new PropertyMetadata("Loading...", OnMessageChanged));

    /// <summary>
    /// Secondary/detail message (optional).
    /// </summary>
    public string SecondaryText
    {
        get => (string)GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public static readonly DependencyProperty SecondaryTextProperty =
        DependencyProperty.Register(
            nameof(SecondaryText),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata(string.Empty, OnSecondaryTextChanged));

    /// <summary>
    /// Whether the overlay is visible.
    /// </summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(false, OnIsLoadingChanged));

    #endregion

    #region Public Methods

    /// <summary>
    /// Show the loading overlay with a message.
    /// </summary>
    public void Show(string message = "Loading...", string? secondaryText = null)
    {
        Message = message;
        SecondaryText = secondaryText ?? string.Empty;
        IsLoading = true;
    }

    /// <summary>
    /// Hide the loading overlay.
    /// </summary>
    public void Hide()
    {
        IsLoading = false;
    }

    #endregion

    #region Private Methods

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
        {
            overlay.LoadingMessage.Text = (string)e.NewValue;
        }
    }

    private static void OnSecondaryTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
        {
            var text = (string)e.NewValue;
            overlay.SecondaryMessage.Text = text;
            overlay.SecondaryMessage.Visibility = string.IsNullOrEmpty(text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
        {
            var isLoading = (bool)e.NewValue;
            if (isLoading)
            {
                overlay.AnimateIn();
            }
            else
            {
                overlay.AnimateOut();
            }
        }
    }

    private void AnimateIn()
    {
        OverlayGrid.Visibility = Visibility.Visible;
        LoadingRing.IsActive = true;
        OverlayGrid.Opacity = 0;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeIn);
        Storyboard.SetTarget(fadeIn, OverlayGrid);
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
        Storyboard.SetTarget(fadeOut, OverlayGrid);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyboard.Completed += (s, e) =>
        {
            OverlayGrid.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = false;
        };
        storyboard.Begin();
    }

    #endregion
}
