using System.Text.Json.Serialization;

namespace InControl.Core.Errors;

/// <summary>
/// Represents a structured error with actionable information.
/// Designed to be user-safe (no stack traces or internal paths exposed).
/// </summary>
public sealed record InControlError
{
    /// <summary>
    /// The error code for categorization.
    /// </summary>
    public required ErrorCode Code { get; init; }

    /// <summary>
    /// User-friendly error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional detailed description for logging/debugging.
    /// Should not contain sensitive information.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Suggested actions the user can take to resolve the error.
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; init; } = [];

    /// <summary>
    /// Error severity level.
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;

    /// <summary>
    /// When the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The source component where the error originated.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Creates a new error with minimal information.
    /// </summary>
    public static InControlError Create(ErrorCode code, string message) => new()
    {
        Code = code,
        Message = message
    };

    /// <summary>
    /// Creates an error from an exception, mapping to appropriate code.
    /// </summary>
    public static InControlError FromException(Exception exception, ErrorCode? code = null)
    {
        var errorCode = code ?? MapExceptionToCode(exception);
        var message = GetUserSafeMessage(exception, errorCode);

        return new InControlError
        {
            Code = errorCode,
            Message = message,
            Detail = exception.Message,
            Severity = ErrorSeverity.Error,
            Source = exception.Source
        };
    }

    /// <summary>
    /// Creates a cancellation error.
    /// </summary>
    public static InControlError Cancelled(string operation = "Operation") => new()
    {
        Code = ErrorCode.Cancelled,
        Message = $"{operation} was cancelled.",
        Severity = ErrorSeverity.Info
    };

    /// <summary>
    /// Creates a timeout error.
    /// </summary>
    public static InControlError Timeout(string operation = "Operation", TimeSpan? duration = null)
    {
        var message = duration.HasValue
            ? $"{operation} timed out after {duration.Value.TotalSeconds:F1} seconds."
            : $"{operation} timed out.";

        return new InControlError
        {
            Code = ErrorCode.Timeout,
            Message = message,
            Suggestions = ["Check your network connection.", "Try again later."]
        };
    }

    /// <summary>
    /// Creates a connection error.
    /// </summary>
    public static InControlError ConnectionFailed(string endpoint, string? backend = null) => new()
    {
        Code = ErrorCode.ConnectionFailed,
        Message = $"Could not connect to {backend ?? "backend"} at {endpoint}.",
        Suggestions =
        [
            $"Ensure {backend ?? "the service"} is running.",
            "Check the endpoint configuration.",
            "Verify firewall settings."
        ]
    };

    /// <summary>
    /// Creates a model not found error.
    /// </summary>
    public static InControlError ModelNotFound(string modelName) => new()
    {
        Code = ErrorCode.ModelNotFound,
        Message = $"Model '{modelName}' was not found.",
        Suggestions =
        [
            $"Run 'ollama pull {modelName}' to download the model.",
            "Check available models in Settings."
        ]
    };

    /// <summary>
    /// Creates a storage path not allowed error.
    /// </summary>
    public static InControlError PathNotAllowed(string path) => new()
    {
        Code = ErrorCode.PathNotAllowed,
        Message = "Access to the specified path is not allowed.",
        Detail = $"Attempted path: {path}",
        Severity = ErrorSeverity.Critical,
        Suggestions = ["Files can only be saved in the app data folder."]
    };

    private static ErrorCode MapExceptionToCode(Exception exception) => exception switch
    {
        OperationCanceledException => ErrorCode.Cancelled,
        TimeoutException => ErrorCode.Timeout,
        HttpRequestException => ErrorCode.ConnectionFailed,
        FileNotFoundException => ErrorCode.FileNotFound,
        UnauthorizedAccessException => ErrorCode.PermissionDenied,
        ArgumentException => ErrorCode.InvalidArgument,
        InvalidOperationException => ErrorCode.InvalidState,
        NotSupportedException => ErrorCode.NotSupported,
        System.Text.Json.JsonException => ErrorCode.DeserializationFailed,
        _ => ErrorCode.Unknown
    };

    private static string GetUserSafeMessage(Exception exception, ErrorCode code) => code switch
    {
        ErrorCode.ConnectionFailed => "Unable to connect to the server.",
        ErrorCode.Timeout => "The operation timed out.",
        ErrorCode.FileNotFound => "The requested file was not found.",
        ErrorCode.PermissionDenied => "Permission denied.",
        ErrorCode.Cancelled => "The operation was cancelled.",
        ErrorCode.DeserializationFailed => "Failed to read data.",
        _ => "An unexpected error occurred."
    };
}
