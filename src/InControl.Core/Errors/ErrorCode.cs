namespace InControl.Core.Errors;

/// <summary>
/// Categorized error codes for InControl operations.
/// </summary>
public enum ErrorCode
{
    // General (0-99)
    Unknown = 0,
    InvalidArgument = 1,
    InvalidState = 2,
    NotSupported = 3,
    Cancelled = 4,
    Timeout = 5,

    // Connection (100-199)
    ConnectionFailed = 100,
    ConnectionTimeout = 101,
    ConnectionRefused = 102,
    HostNotFound = 103,
    NetworkUnavailable = 104,

    // Inference (200-299)
    InferenceFailed = 200,
    ModelNotFound = 201,
    ModelLoadFailed = 202,
    ContextExceeded = 203,
    GenerationFailed = 204,
    StreamInterrupted = 205,
    BackendUnavailable = 206,

    // Storage (300-399)
    StorageFailed = 300,
    FileNotFound = 301,
    PermissionDenied = 302,
    PathNotAllowed = 303,
    SerializationFailed = 304,
    DeserializationFailed = 305,
    CorruptedData = 306,
    FileOperationFailed = 307,

    // Recovery (350-399)
    RecoveryFailed = 350,
    BackupFailed = 351,
    RestoreFailed = 352,
    InvalidOperation = 353,

    // Configuration (400-499)
    ConfigurationInvalid = 400,
    ConfigurationMissing = 401,

    // Validation (500-599)
    ValidationFailed = 500,
    RequiredFieldMissing = 501,
    ValueOutOfRange = 502,

    // Assistant Tools (600-699)
    ToolExecutionFailed = 600,
    ToolNotFound = 601,
    ToolPermissionDenied = 602,
    ToolParameterMissing = 603
}
