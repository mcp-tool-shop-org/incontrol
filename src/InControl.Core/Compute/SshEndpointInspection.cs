namespace InControl.Core.Compute;

/// <summary>
/// What the dial rules decided. A warning still allows the connection.
/// </summary>
public sealed record SshEndpointInspection
{
    public bool CanDial { get; init; }

    public bool VastProxyWarning { get; init; }

    public string? Error { get; init; }

    public string? Warning { get; init; }

    public static SshEndpointInspection Allowed(string? warning = null) => new()
    {
        CanDial = true,
        VastProxyWarning = warning is not null,
        Warning = warning
    };

    public static SshEndpointInspection Blocked(string error) => new()
    {
        CanDial = false,
        Error = error
    };
}
