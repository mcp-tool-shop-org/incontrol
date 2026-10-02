using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace InControl.App.Pages;

/// <summary>
/// Connectivity page - manage network connections and offline mode.
/// </summary>
public sealed partial class ConnectivityPage : UserControl
{
    private bool _isOffline;

    public ConnectivityPage()
    {
        this.InitializeComponent();
        SetupEventHandlers();
        UpdateNetworkInfo();
    }

    /// <summary>
    /// Event raised when user wants to go back.
    /// </summary>
    public event EventHandler? BackRequested;

    /// <summary>
    /// Event raised when offline mode changes.
    /// </summary>
    public event EventHandler<bool>? OfflineModeChanged;

    /// <summary>
    /// Gets or sets offline mode state.
    /// </summary>
    public bool IsOffline
    {
        get => _isOffline;
        set
        {
            _isOffline = value;
            OfflineModeToggle.IsOn = value;
            UpdateOfflineModeUI();
        }
    }

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        OfflineModeToggle.Toggled += OnOfflineModeToggled;
        RefreshButton.Click += OnRefreshClick;
    }

    private void OnOfflineModeToggled(object sender, RoutedEventArgs e)
    {
        _isOffline = OfflineModeToggle.IsOn;
        UpdateOfflineModeUI();
        OfflineModeChanged?.Invoke(this, _isOffline);
    }

    private void UpdateOfflineModeUI()
    {
        if (_isOffline)
        {
            // Offline state
            OfflineModeIcon.Glyph = "\uE8CD"; // Airplane
            OfflineModeDescription.Text = "A rented GPU, RunPod lookup, and model download are off. Chat on this PC still works.";
            ConnectionIndicator.Fill = new SolidColorBrush(Colors.Orange);
            ConnectionStatusText.Text = "Offline";
            DetailStatusText.Text = "Offline (by choice)";

            ModelDownloadStatus.Text = "Off";
            ModelDownloadStatus.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        }
        else
        {
            OfflineModeIcon.Glyph = "\uE701"; // Globe
            OfflineModeDescription.Text = "A rented GPU, RunPod lookup, and model download are available. Chat on this PC works either way.";
            ConnectionIndicator.Fill = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            ConnectionStatusText.Text = "Online";
            DetailStatusText.Text = "Connected";

            ModelDownloadStatus.Text = "Available";
            ModelDownloadStatus.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
        }
    }

    private void UpdateNetworkInfo()
    {
        // Detect network type
        try
        {
            var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            if (profile != null)
            {
                NetworkTypeText.Text = profile.IsWlanConnectionProfile ? "Wi-Fi" : "Ethernet";
            }
            else
            {
                NetworkTypeText.Text = "No connection";
            }
        }
        catch
        {
            NetworkTypeText.Text = "Unknown";
        }

        LastCheckText.Text = "Just now";
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        UpdateNetworkInfo();
    }
}
