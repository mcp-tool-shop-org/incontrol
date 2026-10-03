using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InControl.App.Pages;

/// <summary>
/// Policy and Governance page - configure security policies and access controls.
/// </summary>
public sealed partial class PolicyPage : UserControl
{
    public PolicyPage()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    /// <summary>
    /// Event raised when user wants to go back.
    /// </summary>
    public event EventHandler? BackRequested;

    /// <summary>
    /// Event raised when offline mode changes.
    /// </summary>
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
        OfflineModeToggle.Toggled += OnOfflineModeToggled;
    }

    private void OnOfflineModeToggled(object sender, RoutedEventArgs e)
    {
        OfflineModeChanged?.Invoke(this, OfflineModeToggle.IsOn);
    }

    /// <summary>
    /// Sets the current policy status display.
    /// </summary>
    public void SetPolicyStatus(bool isActive, string statusText)
    {
        PolicyStatusText.Text = statusText;
        PolicyLockIcon.Glyph = isActive ? "\uE72E" : "\uE785";
    }
}
