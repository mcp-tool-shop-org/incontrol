using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InControl.App.Services;

namespace InControl.App.Controls;

/// <summary>
/// Banner shown after unexpected app termination.
/// Provides calm recovery messaging and access to support tools.
/// </summary>
public sealed partial class RecoveryBanner : UserControl
{
    public RecoveryBanner()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    /// <summary>
    /// Event raised when user requests support bundle creation.
    /// </summary>
    public event EventHandler? SupportBundleRequested;

    /// <summary>
    /// Event raised when banner is dismissed.
    /// </summary>
    public event EventHandler? Dismissed;

    private void SetupEventHandlers()
    {
        SupportBundleButton.Click += (s, e) => SupportBundleRequested?.Invoke(this, EventArgs.Empty);
        DismissButton.Click += OnDismissClick;
    }

    /// <summary>
    /// Show the recovery banner with the appropriate message.
    /// </summary>
    public void Show()
    {
        var recovery = CrashRecoveryService.Instance;

        TitleText.Text = recovery.GetRecoveryMessage();
        DetailsText.Text = recovery.GetRecoveryDetails();

        BannerBorder.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hide the recovery banner.
    /// </summary>
    public void Hide()
    {
        BannerBorder.Visibility = Visibility.Collapsed;
    }

    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        // Acknowledge recovery in service
        CrashRecoveryService.Instance.AcknowledgeRecovery();

        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Check if recovery mode is active and show banner if needed.
    /// </summary>
    public void CheckAndShow()
    {
        if (CrashRecoveryService.Instance.IsRecoveryMode)
        {
            Show();
        }
    }
}
