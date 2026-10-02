using Microsoft.UI.Xaml.Controls;

namespace InControl.App.Services;

/// <summary>
/// Service for managing page navigation with backstack support.
/// Provides consistent navigation behavior across the application.
/// </summary>
public sealed class NavigationService
{
    private static NavigationService? _instance;
    private static readonly object _lock = new();

    private readonly Stack<Type> _navigationStack = new();
    private Type? _currentPageType;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static NavigationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new NavigationService();
                }
            }
            return _instance;
        }
    }

    private NavigationService() { }

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event EventHandler<NavigationEventArgs>? Navigated;

    /// <summary>
    /// Event raised when navigating back.
    /// </summary>
    public event EventHandler<NavigationEventArgs>? NavigatedBack;

    /// <summary>
    /// Event raised when navigating to home.
    /// </summary>
    public event EventHandler? NavigatedHome;

    /// <summary>
    /// Gets whether back navigation is possible.
    /// </summary>
    public bool CanGoBack => _navigationStack.Count > 0;

    /// <summary>
    /// Gets the current page type.
    /// </summary>
    public Type? CurrentPageType => _currentPageType;

    /// <summary>
    /// Navigate to a page type, optionally adding current page to backstack.
    /// </summary>
    /// <typeparam name="T">The page type to navigate to.</typeparam>
    /// <param name="addToBackstack">Whether to add current page to backstack.</param>
    public void Navigate<T>(bool addToBackstack = true) where T : UserControl
    {
        Navigate(typeof(T), addToBackstack);
    }

    /// <summary>
    /// Navigate to a page type, optionally adding current page to backstack.
    /// </summary>
    /// <param name="pageType">The page type to navigate to.</param>
    /// <param name="addToBackstack">Whether to add current page to backstack.</param>
    public void Navigate(Type pageType, bool addToBackstack = true)
    {
        // Don't navigate to the same page
        if (_currentPageType == pageType) return;

        // Add current page to backstack if requested and we have a current page
        if (addToBackstack && _currentPageType != null)
        {
            _navigationStack.Push(_currentPageType);
        }

        var previousPageType = _currentPageType;
        _currentPageType = pageType;

        Navigated?.Invoke(this, new NavigationEventArgs(pageType, previousPageType, addToBackstack));
    }

    /// <summary>
    /// Navigate back to the previous page.
    /// </summary>
    /// <returns>True if back navigation occurred, false if stack was empty.</returns>
    public bool GoBack()
    {
        if (_navigationStack.Count == 0)
        {
            // No backstack - go home
            GoHome();
            return false;
        }

        var previousPageType = _currentPageType;
        _currentPageType = _navigationStack.Pop();

        NavigatedBack?.Invoke(this, new NavigationEventArgs(_currentPageType, previousPageType, false));
        return true;
    }

    /// <summary>
    /// Navigate to home, clearing the backstack.
    /// </summary>
    public void GoHome()
    {
        _navigationStack.Clear();
        _currentPageType = null;
        NavigatedHome?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clear the navigation backstack without navigating.
    /// </summary>
    public void ClearBackstack()
    {
        _navigationStack.Clear();
    }

    /// <summary>
    /// Get the backstack depth.
    /// </summary>
    public int BackstackDepth => _navigationStack.Count;
}

/// <summary>
/// Event arguments for navigation events.
/// </summary>
public sealed class NavigationEventArgs : EventArgs
{
    public NavigationEventArgs(Type? pageType, Type? previousPageType, bool addedToBackstack)
    {
        PageType = pageType;
        PreviousPageType = previousPageType;
        AddedToBackstack = addedToBackstack;
    }

    /// <summary>
    /// The page type being navigated to.
    /// </summary>
    public Type? PageType { get; }

    /// <summary>
    /// The page type being navigated from.
    /// </summary>
    public Type? PreviousPageType { get; }

    /// <summary>
    /// Whether the previous page was added to backstack.
    /// </summary>
    public bool AddedToBackstack { get; }
}
