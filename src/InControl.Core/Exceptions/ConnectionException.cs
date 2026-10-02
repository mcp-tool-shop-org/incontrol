namespace InControl.Core.Exceptions;

/// <summary>
/// Exception thrown when connection to an inference backend fails.
/// </summary>
public class ConnectionException : InControlException
{
    /// <summary>
    /// The endpoint that could not be reached.
    /// </summary>
    public string? Endpoint { get; }

    /// <summary>
    /// The backend type (e.g., "Ollama", "LlamaCpp").
    /// </summary>
    public string? Backend { get; }

    public ConnectionException(string message) : base(message, "CONNECTION_ERROR")
    {
    }

    public ConnectionException(string message, string? endpoint, string? backend)
        : base(message, "CONNECTION_ERROR")
    {
        Endpoint = endpoint;
        Backend = backend;
    }

    public ConnectionException(string message, Exception innerException)
        : base(message, "CONNECTION_ERROR", innerException)
    {
    }

    public ConnectionException(string message, string? endpoint, string? backend, Exception innerException)
        : base(message, "CONNECTION_ERROR", innerException)
    {
        Endpoint = endpoint;
        Backend = backend;
    }
}
