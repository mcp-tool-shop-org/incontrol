using System.ComponentModel;

namespace InControl.ViewModels.Theme;

/// <summary>
/// ViewModel for theme and appearance settings.
/// Manages visual preferences across the application.
/// </summary>
public sealed class ThemeViewModel : INotifyPropertyChanged
{
    private AppTheme _theme = AppTheme.System;
    private AccentColor _accentColor = AccentColor.System;
    private bool _useCompactMode;
    private bool _showAnimations = true;
    private double _uiScale = 1.0;

    /// <summary>
    /// The current app theme.
    /// </summary>
    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme != value)
            {
                _theme = value;
                OnPropertyChanged(nameof(Theme));
                OnPropertyChanged(nameof(ThemeDisplayName));
                OnPropertyChanged(nameof(IsDarkMode));
            }
        }
    }

    /// <summary>
    /// Display name for the current theme.
    /// </summary>
    public string ThemeDisplayName => _theme switch
    {
        AppTheme.Light => "Light",
        AppTheme.Dark => "Dark",
        AppTheme.System => "Follow system",
        _ => "Unknown"
    };

    /// <summary>
    /// Whether the effective theme is dark mode.
    /// When set to System, this should be updated based on OS setting.
    /// </summary>
    public bool IsDarkMode => _theme == AppTheme.Dark;

    /// <summary>
    /// The accent color preference.
    /// </summary>
    public AccentColor AccentColor
    {
        get => _accentColor;
        set
        {
            if (_accentColor != value)
            {
                _accentColor = value;
                OnPropertyChanged(nameof(AccentColor));
                OnPropertyChanged(nameof(AccentColorDisplayName));
            }
        }
    }

    /// <summary>
    /// Display name for the accent color.
    /// </summary>
    public string AccentColorDisplayName => _accentColor switch
    {
        AccentColor.System => "System accent",
        AccentColor.Blue => "Blue",
        AccentColor.Purple => "Purple",
        AccentColor.Teal => "Teal",
        AccentColor.Green => "Green",
        AccentColor.Orange => "Orange",
        _ => "Unknown"
    };

    /// <summary>
    /// Whether to use compact UI mode.
    /// Reduces padding and spacing for information density.
    /// </summary>
    public bool UseCompactMode
    {
        get => _useCompactMode;
        set
        {
            if (_useCompactMode != value)
            {
                _useCompactMode = value;
                OnPropertyChanged(nameof(UseCompactMode));
                OnPropertyChanged(nameof(DensityDisplayName));
            }
        }
    }

    /// <summary>
    /// Display name for the current density setting.
    /// </summary>
    public string DensityDisplayName => _useCompactMode ? "Compact" : "Comfortable";

    /// <summary>
    /// Whether to show UI animations.
    /// </summary>
    public bool ShowAnimations
    {
        get => _showAnimations;
        set
        {
            if (_showAnimations != value)
            {
                _showAnimations = value;
                OnPropertyChanged(nameof(ShowAnimations));
            }
        }
    }

    /// <summary>
    /// UI scale factor (1.0 = 100%).
    /// </summary>
    public double UIScale
    {
        get => _uiScale;
        set
        {
            var clamped = Math.Clamp(value, 0.75, 1.5);
            if (Math.Abs(_uiScale - clamped) > 0.001)
            {
                _uiScale = clamped;
                OnPropertyChanged(nameof(UIScale));
                OnPropertyChanged(nameof(UIScalePercent));
            }
        }
    }

    /// <summary>
    /// UI scale as a percentage string.
    /// </summary>
    public string UIScalePercent => $"{_uiScale * 100:F0}%";

    /// <summary>
    /// Resets all theme settings to defaults.
    /// </summary>
    public void ResetToDefaults()
    {
        Theme = AppTheme.System;
        AccentColor = AccentColor.System;
        UseCompactMode = false;
        ShowAnimations = true;
        UIScale = 1.0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Application theme modes.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Follow system theme.
    /// </summary>
    System,

    /// <summary>
    /// Light theme.
    /// </summary>
    Light,

    /// <summary>
    /// Dark theme.
    /// </summary>
    Dark
}

/// <summary>
/// Accent color options.
/// </summary>
public enum AccentColor
{
    /// <summary>
    /// Use system accent color.
    /// </summary>
    System,

    /// <summary>
    /// Blue accent.
    /// </summary>
    Blue,

    /// <summary>
    /// Purple accent.
    /// </summary>
    Purple,

    /// <summary>
    /// Teal accent.
    /// </summary>
    Teal,

    /// <summary>
    /// Green accent.
    /// </summary>
    Green,

    /// <summary>
    /// Orange accent.
    /// </summary>
    Orange
}
