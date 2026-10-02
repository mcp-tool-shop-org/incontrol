using Microsoft.UI.Xaml;

namespace InControl.App.Services;

/// <summary>
/// Service for managing application theme (Light/Dark/System).
/// Provides app-wide theme switching with persistence.
/// </summary>
public sealed class ThemeService
{
    private static ThemeService? _instance;
    private static readonly object _lock = new();

    private ElementTheme _currentTheme = ElementTheme.Default;
    private FrameworkElement? _rootElement;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static ThemeService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ThemeService();
                }
            }
            return _instance;
        }
    }

    private ThemeService() { }

    /// <summary>
    /// Event raised when theme changes.
    /// </summary>
    public event EventHandler<ElementTheme>? ThemeChanged;

    /// <summary>
    /// Gets the current theme.
    /// </summary>
    public ElementTheme CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets whether the current theme is dark (resolved).
    /// </summary>
    public bool IsDarkTheme
    {
        get
        {
            if (_currentTheme == ElementTheme.Dark) return true;
            if (_currentTheme == ElementTheme.Light) return false;
            // System theme - check actual system setting
            return Application.Current.RequestedTheme == ApplicationTheme.Dark;
        }
    }

    /// <summary>
    /// Gets the current theme as a string for display/persistence.
    /// </summary>
    public string CurrentThemeString => _currentTheme switch
    {
        ElementTheme.Light => "Light",
        ElementTheme.Dark => "Dark",
        _ => "System"
    };

    /// <summary>
    /// Initialize with the root element (typically MainWindow content).
    /// </summary>
    public void Initialize(FrameworkElement rootElement, string? savedTheme = null)
    {
        _rootElement = rootElement;

        if (!string.IsNullOrEmpty(savedTheme))
        {
            SetTheme(savedTheme);
        }
    }

    /// <summary>
    /// Sets the theme by string name (Light, Dark, System).
    /// </summary>
    public void SetTheme(string themeName)
    {
        var theme = themeName?.ToLowerInvariant() switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        SetTheme(theme);
    }

    /// <summary>
    /// Sets the theme directly.
    /// </summary>
    public void SetTheme(ElementTheme theme)
    {
        if (_currentTheme == theme) return;

        _currentTheme = theme;

        if (_rootElement != null)
        {
            _rootElement.RequestedTheme = theme;
        }

        ThemeChanged?.Invoke(this, theme);
    }

    /// <summary>
    /// Toggles between Light and Dark themes.
    /// If currently on System, switches to Light.
    /// </summary>
    public void ToggleTheme()
    {
        var nextTheme = IsDarkTheme ? ElementTheme.Light : ElementTheme.Dark;
        SetTheme(nextTheme);
    }

    /// <summary>
    /// Cycles through themes: System -> Light -> Dark -> System
    /// </summary>
    public void CycleTheme()
    {
        var nextTheme = _currentTheme switch
        {
            ElementTheme.Default => ElementTheme.Light,
            ElementTheme.Light => ElementTheme.Dark,
            ElementTheme.Dark => ElementTheme.Default,
            _ => ElementTheme.Default
        };

        SetTheme(nextTheme);
    }

    /// <summary>
    /// Gets the theme index for ComboBox binding.
    /// </summary>
    public int GetThemeIndex()
    {
        return _currentTheme switch
        {
            ElementTheme.Light => 0,
            ElementTheme.Dark => 1,
            _ => 2 // System/Default
        };
    }

    /// <summary>
    /// Sets theme from ComboBox index.
    /// </summary>
    public void SetThemeFromIndex(int index)
    {
        var theme = index switch
        {
            0 => ElementTheme.Light,
            1 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        SetTheme(theme);
    }

    /// <summary>
    /// Gets the icon glyph for the current theme.
    /// </summary>
    public string GetThemeIcon()
    {
        return _currentTheme switch
        {
            ElementTheme.Light => "\uE706", // Sun
            ElementTheme.Dark => "\uE708", // Moon
            _ => "\uE770" // Settings (System)
        };
    }
}
