namespace InControl.Core.UX;

/// <summary>
/// Represents the connection state to the inference backend.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Connection status is unknown or not yet checked.
    /// </summary>
    Unknown,

    /// <summary>
    /// Attempting to connect to the backend.
    /// </summary>
    Connecting,

    /// <summary>
    /// Successfully connected to the backend.
    /// </summary>
    Connected,

    /// <summary>
    /// Connection to the backend is unavailable.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Connection attempt timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Backend is connected but reporting issues.
    /// </summary>
    Degraded
}

/// <summary>
/// Extension methods for ConnectionState.
/// </summary>
public static class ConnectionStateExtensions
{
    /// <summary>
    /// Gets the display text for the connection state.
    /// </summary>
    public static string ToDisplayText(this ConnectionState state) => state switch
    {
        ConnectionState.Unknown => "Checking connection...",
        ConnectionState.Connecting => "Connecting...",
        ConnectionState.Connected => "Connected",
        ConnectionState.Disconnected => "Disconnected",
        ConnectionState.Timeout => "Connection timeout",
        ConnectionState.Degraded => "Connected (degraded)",
        _ => "Unknown"
    };

    /// <summary>
    /// Whether the connection is usable for execution.
    /// </summary>
    public static bool IsUsable(this ConnectionState state) => state switch
    {
        ConnectionState.Connected or ConnectionState.Degraded => true,
        _ => false
    };

    /// <summary>
    /// Whether the connection is actively being established.
    /// </summary>
    public static bool IsConnecting(this ConnectionState state) =>
        state == ConnectionState.Connecting || state == ConnectionState.Unknown;
}
