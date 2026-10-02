using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using InControl.ViewModels.Errors;

namespace InControl.App.Controls;

/// <summary>
/// Displays an issue (error/warning) with recovery actions.
/// Per UX contract: No blame language - state facts, provide context, offer actions.
/// </summary>
public sealed partial class IssueCard : UserControl
{
    public IssueCard()
    {
        this.InitializeComponent();
        DismissButton.Click += OnDismissClick;
    }

    /// <summary>
    /// The issue view model to display.
    /// </summary>
    public IssueViewModel? Issue
    {
        get => (IssueViewModel?)GetValue(IssueProperty);
        set => SetValue(IssueProperty, value);
    }

    public static readonly DependencyProperty IssueProperty =
        DependencyProperty.Register(
            nameof(Issue),
            typeof(IssueViewModel),
            typeof(IssueCard),
            new PropertyMetadata(null, OnIssueChanged));

    /// <summary>
    /// Event raised when dismiss is clicked.
    /// </summary>
    public event EventHandler? DismissClicked;

    private static void OnIssueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IssueCard card)
        {
            card.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        var issue = Issue;
        if (issue == null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = issue.IsDismissed ? Visibility.Collapsed : Visibility.Visible;

        TitleText.Text = issue.Title;
        DetailText.Text = issue.Detail;
        SeverityIcon.Glyph = issue.SeverityIcon;
        SeverityIcon.Foreground = GetSeverityBrush(issue.Severity);
        CardBorder.BorderBrush = GetSeverityBrush(issue.Severity);

        // Show suggestions if available
        if (issue.HasSuggestions)
        {
            SuggestionsPanel.Visibility = Visibility.Visible;
            SuggestionsList.ItemsSource = issue.Suggestions;
        }
        else
        {
            SuggestionsPanel.Visibility = Visibility.Collapsed;
        }

        // Build recovery actions
        ActionsPanel.Children.Clear();
        if (issue.HasRecoveryActions)
        {
            ActionsPanel.Visibility = Visibility.Visible;
            foreach (var action in issue.RecoveryActions)
            {
                var button = new Button
                {
                    Content = action.Label,
                    Style = action.IsPrimary
                        ? (Style)Application.Current.Resources["AccentButtonStyle"]
                        : null
                };
                button.Click += (s, e) => action.Execute();
                ActionsPanel.Children.Add(button);
            }
        }
        else
        {
            ActionsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private Brush GetSeverityBrush(IssueSeverity severity)
    {
        var resourceKey = severity switch
        {
            IssueSeverity.Info => "SystemFillColorNeutralBrush",
            IssueSeverity.Warning => "SystemFillColorCautionBrush",
            IssueSeverity.Critical => "SystemFillColorCriticalBrush",
            _ => "SystemFillColorNeutralBrush"
        };

        if (Application.Current.Resources.TryGetValue(resourceKey, out var brush) && brush is Brush b)
            return b;

        return new SolidColorBrush(Microsoft.UI.Colors.Orange);
    }

    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        Issue?.Dismiss();
        DismissClicked?.Invoke(this, EventArgs.Empty);
        Visibility = Visibility.Collapsed;
    }
}
