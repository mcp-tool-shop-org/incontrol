using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InControl.App.Services;

/// <summary>
/// The icon by the clock that holds InControl while it is minimized to the tray.
/// Clicking it brings the window back; its menu can also close the app.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Window _window;
    private TaskbarIcon? _icon;

    public TrayIcon(Window window)
    {
        _window = window;
    }

    /// <summary>
    /// True while the window is hidden in the tray.
    /// </summary>
    public bool IsHidden { get; private set; }

    /// <summary>
    /// Hides the window and shows the tray icon.
    /// </summary>
    public void HideToTray()
    {
        EnsureIcon();
        IsHidden = true;
        _window.Hide(enableEfficiencyMode: false);
    }

    /// <summary>
    /// Brings the window back from the tray.
    /// </summary>
    public void Restore()
    {
        IsHidden = false;
        _window.Show(disableEfficiencyMode: true);
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        _window.Activate();
    }

    private void EnsureIcon()
    {
        if (_icon is not null)
            return;

        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Open InControl", Command = new RelayCommand(Restore) };
        var exit = new MenuFlyoutItem { Text = "Exit", Command = new RelayCommand(Exit) };
        menu.Items.Add(open);
        menu.Items.Add(exit);

        _icon = new TaskbarIcon
        {
            ToolTipText = "InControl",
            Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico")),
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(Restore),
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu
        };
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void Exit()
    {
        Dispose();
        _window.Close();
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
