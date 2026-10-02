namespace InControl.Core.Compute;

/// <summary>
/// A running SSH process that exists only to hold a local port forward.
/// </summary>
public interface ISshSession : IAsyncDisposable
{
    bool HasExited { get; }

    string StandardError { get; }

    /// <summary>
    /// Raised when the SSH process exits on its own. Disposing the session may also
    /// raise it. Callers ignore the event unless this instance is still the current one.
    /// </summary>
    event EventHandler? Exited;
}

/// <summary>
/// Starts the forward. Implementations write the session config and own the process.
/// </summary>
public interface ISshSessionFactory
{
    Task<ISshSession> OpenAsync(
        SshEndpoint endpoint,
        int localPort,
        string configDirectory,
        CancellationToken cancellationToken);
}

/// <summary>
/// Checks that the local forward port is accepting TCP.
/// </summary>
public interface ITcpProbe
{
    Task<bool> CanConnectAsync(string host, int port, CancellationToken cancellationToken);
}
