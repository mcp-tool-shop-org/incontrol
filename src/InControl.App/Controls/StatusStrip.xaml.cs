using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace InControl.App.Controls;

/// <summary>
/// Status strip showing continuous system state at the bottom of the window.
/// Provides at-a-glance visibility into model, device, connectivity, and assistant status.
/// </summary>
public sealed partial class StatusStrip : UserControl
{
    public StatusStrip()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    #region Events

    /// <summary>
    /// Event raised when Model status is clicked.
    /// </summary>
    public event EventHandler? ModelClicked;

    /// <summary>
    /// Event raised when Device status is clicked.
    /// </summary>
    public event EventHandler? DeviceClicked;

    /// <summary>
    /// Event raised when Connectivity status is clicked.
    /// </summary>
    public event EventHandler? ConnectivityClicked;

    /// <summary>
    /// Event raised when Policy status is clicked.
    /// </summary>
    public event EventHandler? PolicyClicked;

    /// <summary>
    /// Event raised when Assistant status is clicked.
    /// </summary>
    public event EventHandler? AssistantClicked;

    /// <summary>
    /// Event raised when Memory status is clicked.
    /// </summary>
    public event EventHandler? MemoryClicked;

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the model status display.
    /// </summary>
    public void SetModelStatus(string? modelName, bool isLoaded)
    {
        if (isLoaded && !string.IsNullOrEmpty(modelName))
        {
            ModelText.Text = modelName;
            ModelIcon.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
        }
        else
        {
            ModelText.Text = "No model";
            ModelIcon.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }
    }

    /// <summary>
    /// Sets the device status display.
    /// </summary>
    public void SetDeviceStatus(string deviceType, bool isGpu)
    {
        DeviceText.Text = deviceType;
        DeviceIcon.Glyph = isGpu ? "\uE950" : "\uE7F4"; // GPU or CPU icon
    }

    /// <summary>
    /// Sets the connectivity status display.
    /// </summary>
    public void SetConnectivityStatus(bool isOffline)
    {
        if (isOffline)
        {
            ConnectivityText.Text = "Offline";
            ConnectivityIcon.Glyph = "\uE8CD"; // Airplane
            ConnectivityIcon.Foreground = new SolidColorBrush(Colors.Orange);
        }
        else
        {
            ConnectivityText.Text = "Online";
            ConnectivityIcon.Glyph = "\uE701"; // Globe
            ConnectivityIcon.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }
    }

    /// <summary>
    /// Sets the policy status display.
    /// </summary>
    public void SetPolicyStatus(bool isActive)
    {
        PolicyButton.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Sets the assistant status display.
    /// </summary>
    public void SetAssistantStatus(bool isEnabled, string statusText = "Assistant")
    {
        AssistantText.Text = statusText;
        AssistantIndicator.Fill = isEnabled
            ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (Brush)Application.Current.Resources["SystemFillColorNeutralBrush"];
    }

    /// <summary>
    /// Sets the memory usage display.
    /// </summary>
    public void SetMemoryUsage(double gigabytes, bool show = true)
    {
        MemoryButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        MemoryText.Text = $"{gigabytes:F1} GB";
    }

    #endregion

    #region Private Methods

    private void SetupEventHandlers()
    {
        ModelStatusButton.Click += (s, e) => ModelClicked?.Invoke(this, EventArgs.Empty);
        DeviceStatusButton.Click += (s, e) => DeviceClicked?.Invoke(this, EventArgs.Empty);
        ConnectivityButton.Click += (s, e) => ConnectivityClicked?.Invoke(this, EventArgs.Empty);
        PolicyButton.Click += (s, e) => PolicyClicked?.Invoke(this, EventArgs.Empty);
        AssistantButton.Click += (s, e) => AssistantClicked?.Invoke(this, EventArgs.Empty);
        MemoryButton.Click += (s, e) => MemoryClicked?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
