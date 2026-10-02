namespace InControl.Core.Errors;

/// <summary>
/// Severity levels for errors.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational - operation completed but with notes.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - operation completed but may have issues.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - operation failed but can be retried.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - operation failed and requires intervention.
    /// </summary>
    Critical
}
