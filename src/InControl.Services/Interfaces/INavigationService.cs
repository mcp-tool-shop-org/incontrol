namespace InControl.Services.Interfaces;

/// <summary>
/// Service for navigating between views.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    event EventHandler<NavigatedEventArgs>? Navigated;

    /// <summary>
    /// Gets whether back navigation is possible.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Gets the current view key.
    /// </summary>
    string? CurrentView { get; }

    /// <summary>
    /// Navigates to a view.
    /// </summary>
    /// <param name="viewKey">The view key.</param>
    /// <param name="parameter">Optional navigation parameter.</param>
    /// <returns>True if navigation succeeded.</returns>
    bool NavigateTo(string viewKey, object? parameter = null);

    /// <summary>
    /// Navigates back.
    /// </summary>
    /// <returns>True if navigation succeeded.</returns>
    bool GoBack();

    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    void ClearHistory();
}

/// <summary>
/// Event args for navigation.
/// </summary>
public sealed class NavigatedEventArgs : EventArgs
{
    /// <summary>
    /// The view that was navigated to.
    /// </summary>
    public required string ViewKey { get; init; }

    /// <summary>
    /// The navigation parameter.
    /// </summary>
    public object? Parameter { get; init; }

    /// <summary>
    /// Whether this was a back navigation.
    /// </summary>
    public bool IsBack { get; init; }
}
