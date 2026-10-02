namespace InControl.Core.Compute;

/// <summary>
/// The local end of the SSH forward. It must not be the Ollama already running on this PC,
/// or a dead tunnel can be answered by this machine.
/// </summary>
public static class TunnelPort
{
    /// <summary>
    /// Fixed loopback port for the rental forward. It is not 11434, which is Ollama on this PC.
    /// </summary>
    public const int Dedicated = 11436;

    public static int ParseLocal(string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl)
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && uri.Port is > 0 and <= 65535)
        {
            return uri.Port;
        }

        return 11434;
    }

    public static int DedicatedFor(string? localBaseUrl)
    {
        var local = ParseLocal(localBaseUrl);
        return Dedicated == local ? Dedicated + 1 : Dedicated;
    }

    public static bool IsForwardPort(int port, string? localBaseUrl) =>
        port is >= 1 and <= 65535 && port != ParseLocal(localBaseUrl);

    /// <summary>
    /// The version probe may only call numeric loopback HTTP. A public URL is not a tunnel check.
    /// </summary>
    public static bool IsLoopbackProbe(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == "http"
            && uri.Host == "127.0.0.1"
            && uri.Port is >= 1 and <= 65535
            && string.IsNullOrEmpty(uri.UserInfo);
    }
}
