using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace InControl.ViewModels;

/// <summary>
/// Base class for all ViewModels providing common infrastructure.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected readonly ILogger Logger;

    /// <summary>
    /// Indicates whether the ViewModel is currently loading data.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Indicates whether the ViewModel is currently busy with an operation.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Current error message, if any.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Indicates whether there is an error.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    protected ViewModelBase(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Called when the ViewModel is navigated to.
    /// </summary>
    /// <param name="parameter">Navigation parameter.</param>
    public virtual Task OnNavigatedToAsync(object? parameter)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the ViewModel is navigated away from.
    /// </summary>
    public virtual Task OnNavigatedFromAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears the current error.
    /// </summary>
    protected void ClearError()
    {
        ErrorMessage = null;
    }

    /// <summary>
    /// Sets an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected void SetError(string message)
    {
        ErrorMessage = message;
        Logger.LogError("ViewModel error: {Message}", message);
    }

    /// <summary>
    /// Sets an error from an exception.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="userMessage">Optional user-friendly message.</param>
    protected void SetError(Exception ex, string? userMessage = null)
    {
        ErrorMessage = userMessage ?? ex.Message;
        Logger.LogError(ex, "ViewModel exception: {Message}", ex.Message);
    }

    /// <summary>
    /// Executes an async operation with loading state management.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="showLoading">Whether to set IsLoading during execution.</param>
    protected async Task ExecuteAsync(Func<Task> operation, bool showLoading = true)
    {
        if (showLoading) IsLoading = true;
        ClearError();

        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // Cancelled - don't treat as error
            Logger.LogDebug("Operation cancelled");
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            if (showLoading) IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an async operation with loading state management and returns a result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="defaultValue">Value to return on failure.</param>
    /// <param name="showLoading">Whether to set IsLoading during execution.</param>
    /// <returns>The operation result or default value.</returns>
    protected async Task<T?> ExecuteAsync<T>(
        Func<Task<T>> operation,
        T? defaultValue = default,
        bool showLoading = true)
    {
        if (showLoading) IsLoading = true;
        ClearError();

        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Operation cancelled");
            return defaultValue;
        }
        catch (Exception ex)
        {
            SetError(ex);
            return defaultValue;
        }
        finally
        {
            if (showLoading) IsLoading = false;
        }
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }
}
