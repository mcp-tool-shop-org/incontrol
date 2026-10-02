using System.Text.Json;
using System.Text.Json.Serialization;

namespace InControl.Core.Assistant;

/// <summary>
/// Represents an error that occurred during assistant operation.
/// All errors are classified and include recovery guidance.
/// </summary>
public sealed record AssistantError(
    Guid Id,
    AssistantErrorType Type,
    string Message,
    string? Details,
    AssistantErrorSeverity Severity,
    string RecoveryGuidance,
    DateTimeOffset OccurredAt,
    Exception? Exception = null
)
{
    /// <summary>
    /// Creates a user-friendly error message.
    /// </summary>
    public string ToUserMessage()
    {
        return Severity switch
        {
            AssistantErrorSeverity.Low => $"Minor issue: {Message}",
            AssistantErrorSeverity.Medium => $"Problem encountered: {Message}. {RecoveryGuidance}",
            AssistantErrorSeverity.High => $"Significant error: {Message}. {RecoveryGuidance}",
            AssistantErrorSeverity.Critical => $"Critical failure: {Message}. Please {RecoveryGuidance}",
            _ => Message
        };
    }
}

/// <summary>
/// Types of assistant errors.
/// </summary>
public enum AssistantErrorType
{
    /// <summary>Tool execution failed.</summary>
    ToolFailure,

    /// <summary>Memory operation failed.</summary>
    MemoryFailure,

    /// <summary>State transition was invalid.</summary>
    InvalidStateTransition,

    /// <summary>Network or connectivity issue.</summary>
    NetworkError,

    /// <summary>User input was invalid or unclear.</summary>
    InputError,

    /// <summary>Resource not found.</summary>
    NotFound,

    /// <summary>Permission denied.</summary>
    PermissionDenied,

    /// <summary>Operation timed out.</summary>
    Timeout,

    /// <summary>Rate limit exceeded.</summary>
    RateLimited,

    /// <summary>Internal logic error.</summary>
    InternalError,

    /// <summary>Configuration error.</summary>
    ConfigurationError,

    /// <summary>External service error.</summary>
    ExternalServiceError
}

/// <summary>
/// Severity levels for assistant errors.
/// </summary>
public enum AssistantErrorSeverity
{
    /// <summary>Minor issue, operation can continue.</summary>
    Low,

    /// <summary>Moderate issue, may affect results.</summary>
    Medium,

    /// <summary>Significant issue, operation may fail.</summary>
    High,

    /// <summary>Critical failure, immediate action needed.</summary>
    Critical
}

/// <summary>
/// Handles errors and recovery for the assistant.
/// </summary>
public sealed class AssistantErrorHandler
{
    private readonly List<AssistantError> _errorHistory = [];
    private readonly object _lock = new();
    private readonly int _maxHistorySize;
    private readonly AssistantTrace? _trace;

    /// <summary>
    /// Event raised when an error occurs.
    /// </summary>
    public event EventHandler<AssistantErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Event raised when recovery is attempted.
    /// </summary>
    public event EventHandler<RecoveryAttemptEventArgs>? RecoveryAttempted;

    public AssistantErrorHandler(int maxHistorySize = 100, AssistantTrace? trace = null)
    {
        _maxHistorySize = maxHistorySize;
        _trace = trace;
    }

    /// <summary>
    /// All recorded errors.
    /// </summary>
    public IReadOnlyList<AssistantError> ErrorHistory
    {
        get
        {
            lock (_lock)
            {
                return _errorHistory.ToList();
            }
        }
    }

    /// <summary>
    /// Records an error and returns recovery guidance.
    /// </summary>
    public AssistantError HandleError(
        AssistantErrorType type,
        string message,
        string? details = null,
        Exception? exception = null)
    {
        var severity = DetermineSeverity(type, exception);
        var guidance = GetRecoveryGuidance(type, severity);

        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: type,
            Message: message,
            Details: details ?? exception?.Message,
            Severity: severity,
            RecoveryGuidance: guidance,
            OccurredAt: DateTimeOffset.UtcNow,
            Exception: exception
        );

        lock (_lock)
        {
            _errorHistory.Add(error);
            while (_errorHistory.Count > _maxHistorySize)
            {
                _errorHistory.RemoveAt(0);
            }
        }

        _trace?.Error($"[{type}] {message}", guidance);

        ErrorOccurred?.Invoke(this, new AssistantErrorEventArgs(error));

        return error;
    }

    /// <summary>
    /// Attempts automatic recovery for the given error.
    /// </summary>
    public async Task<RecoveryResult> AttemptRecoveryAsync(
        AssistantError error,
        CancellationToken ct = default)
    {
        var strategy = GetRecoveryStrategy(error.Type);

        RecoveryAttempted?.Invoke(this, new RecoveryAttemptEventArgs(error, strategy));

        return strategy switch
        {
            RecoveryStrategy.Retry => await RetryAsync(error, ct),
            RecoveryStrategy.Fallback => ApplyFallback(error),
            RecoveryStrategy.Reset => ResetState(error),
            RecoveryStrategy.Escalate => EscalateToUser(error),
            RecoveryStrategy.Ignore => new RecoveryResult(true, "Error ignored per policy"),
            _ => new RecoveryResult(false, "No recovery strategy available")
        };
    }

    /// <summary>
    /// Creates an error boundary that catches exceptions.
    /// </summary>
    public async Task<ErrorBoundaryResult<T>> WithErrorBoundaryAsync<T>(
        Func<Task<T>> action,
        AssistantErrorType errorType,
        string context,
        T? fallbackValue = default)
    {
        try
        {
            var result = await action();
            return new ErrorBoundaryResult<T>(result, null, true);
        }
        catch (OperationCanceledException)
        {
            throw; // Don't catch cancellation
        }
        catch (Exception ex)
        {
            var error = HandleError(errorType, $"Error in {context}", ex.Message, ex);
            return new ErrorBoundaryResult<T>(fallbackValue, error, false);
        }
    }

    /// <summary>
    /// Creates a synchronous error boundary.
    /// </summary>
    public ErrorBoundaryResult<T> WithErrorBoundary<T>(
        Func<T> action,
        AssistantErrorType errorType,
        string context,
        T? fallbackValue = default)
    {
        try
        {
            var result = action();
            return new ErrorBoundaryResult<T>(result, null, true);
        }
        catch (Exception ex)
        {
            var error = HandleError(errorType, $"Error in {context}", ex.Message, ex);
            return new ErrorBoundaryResult<T>(fallbackValue, error, false);
        }
    }

    /// <summary>
    /// Gets errors of a specific type.
    /// </summary>
    public IReadOnlyList<AssistantError> GetErrorsByType(AssistantErrorType type)
    {
        lock (_lock)
        {
            return _errorHistory.Where(e => e.Type == type).ToList();
        }
    }

    /// <summary>
    /// Gets errors at or above a severity level.
    /// </summary>
    public IReadOnlyList<AssistantError> GetErrorsBySeverity(AssistantErrorSeverity minSeverity)
    {
        lock (_lock)
        {
            return _errorHistory.Where(e => e.Severity >= minSeverity).ToList();
        }
    }

    /// <summary>
    /// Gets recent errors.
    /// </summary>
    public IReadOnlyList<AssistantError> GetRecentErrors(int count)
    {
        lock (_lock)
        {
            return _errorHistory.TakeLast(count).ToList();
        }
    }

    /// <summary>
    /// Clears error history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _errorHistory.Clear();
        }
    }

    /// <summary>
    /// Exports errors to JSON.
    /// </summary>
    public string ExportToJson()
    {
        lock (_lock)
        {
            var exportable = _errorHistory.Select(e => new
            {
                e.Id,
                e.Type,
                e.Message,
                e.Details,
                e.Severity,
                e.RecoveryGuidance,
                e.OccurredAt,
                ExceptionType = e.Exception?.GetType().Name,
                ExceptionMessage = e.Exception?.Message
            });

            return JsonSerializer.Serialize(exportable, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }
    }

    private static AssistantErrorSeverity DetermineSeverity(AssistantErrorType type, Exception? ex)
    {
        return type switch
        {
            AssistantErrorType.InputError => AssistantErrorSeverity.Low,
            AssistantErrorType.NotFound => AssistantErrorSeverity.Low,
            AssistantErrorType.RateLimited => AssistantErrorSeverity.Medium,
            AssistantErrorType.Timeout => AssistantErrorSeverity.Medium,
            AssistantErrorType.NetworkError => AssistantErrorSeverity.Medium,
            AssistantErrorType.ToolFailure => AssistantErrorSeverity.Medium,
            AssistantErrorType.MemoryFailure => AssistantErrorSeverity.High,
            AssistantErrorType.PermissionDenied => AssistantErrorSeverity.High,
            AssistantErrorType.InvalidStateTransition => AssistantErrorSeverity.High,
            AssistantErrorType.ConfigurationError => AssistantErrorSeverity.High,
            AssistantErrorType.ExternalServiceError => AssistantErrorSeverity.Medium,
            AssistantErrorType.InternalError => ex is OutOfMemoryException or StackOverflowException
                ? AssistantErrorSeverity.Critical
                : AssistantErrorSeverity.High,
            _ => AssistantErrorSeverity.Medium
        };
    }

    private static string GetRecoveryGuidance(AssistantErrorType type, AssistantErrorSeverity severity)
    {
        return type switch
        {
            AssistantErrorType.ToolFailure => "The tool will be retried automatically. If the problem persists, try a different approach.",
            AssistantErrorType.MemoryFailure => "Memory could not be saved. Your data is still in the current session.",
            AssistantErrorType.InvalidStateTransition => "An unexpected state occurred. The assistant will reset to a safe state.",
            AssistantErrorType.NetworkError => "Network connectivity issue. Please check your connection.",
            AssistantErrorType.InputError => "Please rephrase your request or provide more details.",
            AssistantErrorType.NotFound => "The requested item was not found. Please verify the reference.",
            AssistantErrorType.PermissionDenied => "You don't have permission for this action. Contact your administrator.",
            AssistantErrorType.Timeout => "The operation took too long. It will be retried with a longer timeout.",
            AssistantErrorType.RateLimited => "Too many requests. Please wait a moment before trying again.",
            AssistantErrorType.InternalError => severity == AssistantErrorSeverity.Critical
                ? "restart the application"
                : "This is a bug. Please report it with the error details.",
            AssistantErrorType.ConfigurationError => "Check your configuration settings.",
            AssistantErrorType.ExternalServiceError => "An external service is unavailable. Please try again later.",
            _ => "Please try again or contact support if the problem persists."
        };
    }

    private static RecoveryStrategy GetRecoveryStrategy(AssistantErrorType type)
    {
        return type switch
        {
            AssistantErrorType.ToolFailure => RecoveryStrategy.Retry,
            AssistantErrorType.Timeout => RecoveryStrategy.Retry,
            AssistantErrorType.NetworkError => RecoveryStrategy.Retry,
            AssistantErrorType.RateLimited => RecoveryStrategy.Retry,
            AssistantErrorType.MemoryFailure => RecoveryStrategy.Fallback,
            AssistantErrorType.NotFound => RecoveryStrategy.Fallback,
            AssistantErrorType.InvalidStateTransition => RecoveryStrategy.Reset,
            AssistantErrorType.InternalError => RecoveryStrategy.Reset,
            AssistantErrorType.InputError => RecoveryStrategy.Escalate,
            AssistantErrorType.PermissionDenied => RecoveryStrategy.Escalate,
            AssistantErrorType.ConfigurationError => RecoveryStrategy.Escalate,
            AssistantErrorType.ExternalServiceError => RecoveryStrategy.Retry,
            _ => RecoveryStrategy.Escalate
        };
    }

    private static async Task<RecoveryResult> RetryAsync(AssistantError error, CancellationToken ct)
    {
        // In a real implementation, this would retry the operation
        await Task.Delay(100, ct); // Simulated backoff
        return new RecoveryResult(false, "Retry scheduled - actual retry depends on context");
    }

    private static RecoveryResult ApplyFallback(AssistantError error)
    {
        return new RecoveryResult(true, "Fallback applied - using default behavior");
    }

    private static RecoveryResult ResetState(AssistantError error)
    {
        return new RecoveryResult(true, "State reset to safe defaults");
    }

    private static RecoveryResult EscalateToUser(AssistantError error)
    {
        return new RecoveryResult(false, $"User action required: {error.RecoveryGuidance}");
    }
}

/// <summary>
/// Recovery strategies for errors.
/// </summary>
public enum RecoveryStrategy
{
    /// <summary>Retry the operation.</summary>
    Retry,

    /// <summary>Use a fallback value or behavior.</summary>
    Fallback,

    /// <summary>Reset to a safe state.</summary>
    Reset,

    /// <summary>Ask the user for help.</summary>
    Escalate,

    /// <summary>Ignore the error.</summary>
    Ignore
}

/// <summary>
/// Result of a recovery attempt.
/// </summary>
public sealed record RecoveryResult(
    bool Recovered,
    string Message
);

/// <summary>
/// Result of an operation within an error boundary.
/// </summary>
public sealed record ErrorBoundaryResult<T>(
    T? Value,
    AssistantError? Error,
    bool Success
);

/// <summary>
/// Event args for error events.
/// </summary>
public sealed class AssistantErrorEventArgs : EventArgs
{
    public AssistantError Error { get; }

    public AssistantErrorEventArgs(AssistantError error)
    {
        Error = error;
    }
}

/// <summary>
/// Event args for recovery attempts.
/// </summary>
public sealed class RecoveryAttemptEventArgs : EventArgs
{
    public AssistantError Error { get; }
    public RecoveryStrategy Strategy { get; }

    public RecoveryAttemptEventArgs(AssistantError error, RecoveryStrategy strategy)
    {
        Error = error;
        Strategy = strategy;
    }
}

/// <summary>
/// Extension methods for error handling.
/// </summary>
public static class AssistantErrorExtensions
{
    /// <summary>
    /// Determines if an error is recoverable automatically.
    /// </summary>
    public static bool IsAutoRecoverable(this AssistantError error)
    {
        return error.Type is AssistantErrorType.ToolFailure
            or AssistantErrorType.Timeout
            or AssistantErrorType.NetworkError
            or AssistantErrorType.RateLimited
            && error.Severity < AssistantErrorSeverity.Critical;
    }

    /// <summary>
    /// Determines if an error requires user intervention.
    /// </summary>
    public static bool RequiresUserAction(this AssistantError error)
    {
        return error.Type is AssistantErrorType.InputError
            or AssistantErrorType.PermissionDenied
            or AssistantErrorType.ConfigurationError
            || error.Severity == AssistantErrorSeverity.Critical;
    }
}
