namespace InControl.Core.Exceptions;

/// <summary>
/// Base exception for all InControl-specific errors.
/// </summary>
public class InControlException : Exception
{
    /// <summary>
    /// Error code for categorizing the exception.
    /// </summary>
    public string? ErrorCode { get; }

    public InControlException()
    {
    }

    public InControlException(string message) : base(message)
    {
    }

    public InControlException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public InControlException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public InControlException(string message, string errorCode, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
