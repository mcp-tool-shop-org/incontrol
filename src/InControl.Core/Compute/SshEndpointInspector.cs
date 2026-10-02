namespace InControl.Core.Compute;

/// <summary>
/// Decides whether a login is safe to hand to OpenSSH. RunPod's proxy host is refused
/// because that path cannot forward a port. Vast's proxy host is allowed with a warning,
/// because the provider documents the forward on the direct address and is silent on the proxy.
/// </summary>
public static class SshEndpointInspector
{
    public static SshEndpointInspection Inspect(SshEndpoint endpoint, bool requireIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (string.IsNullOrWhiteSpace(endpoint.DisplayName)
            || endpoint.DisplayName.Length > 80
            || endpoint.DisplayName.Any(char.IsControl))
        {
            return SshEndpointInspection.Blocked("Give the machine a one-line name.");
        }

        if (!IsUser(endpoint.User))
        {
            return SshEndpointInspection.Blocked("The SSH user has to be a plain name, like root.");
        }

        if (!IsHost(endpoint.Host))
        {
            return SshEndpointInspection.Blocked("The SSH host has to be a hostname or an IPv4 address.");
        }

        if (endpoint.SshPort is < 1 or > 65535)
        {
            return SshEndpointInspection.Blocked("The SSH port has to be between 1 and 65535.");
        }

        if (endpoint.RemoteOllamaPort is < 1 or > 65535)
        {
            return SshEndpointInspection.Blocked("The Ollama port on that machine has to be between 1 and 65535.");
        }

        if (endpoint.SshPort == 11434)
        {
            return SshEndpointInspection.Blocked(ComputeNotice.OllamaPortIsNotSsh);
        }

        if (IsRunPodProxy(endpoint.Host))
        {
            return SshEndpointInspection.Blocked(ComputeNotice.RunPodProxyBlocked);
        }

        if (requireIdentity)
        {
            if (string.IsNullOrWhiteSpace(endpoint.IdentityFile))
            {
                return SshEndpointInspection.Blocked(ComputeNotice.MissingIdentity);
            }

            if (!IsSafePath(endpoint.IdentityFile))
            {
                return SshEndpointInspection.Blocked("The key path cannot contain a quote or a new line.");
            }
        }
        else if (!string.IsNullOrEmpty(endpoint.IdentityFile) && !IsSafePath(endpoint.IdentityFile))
        {
            return SshEndpointInspection.Blocked("The key path cannot contain a quote or a new line.");
        }

        var warning = IsVastProxy(endpoint.Host) ? ComputeNotice.VastProxyWarning : null;
        return SshEndpointInspection.Allowed(warning);
    }

    public static bool IsRunPodProxy(string host) =>
        string.Equals(host, "ssh.runpod.io", StringComparison.OrdinalIgnoreCase);

    public static bool IsVastProxy(string host)
    {
        if (!host.EndsWith(".vast.ai", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var label = host[..^".vast.ai".Length];
        return label.StartsWith("ssh", StringComparison.OrdinalIgnoreCase)
            && label.Length > 3
            && label[3..].All(char.IsDigit);
    }

    public static bool IsSafePath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.IndexOfAny(['"', '\r', '\n', '\0']) < 0;

    private static bool IsUser(string user) =>
        user.Length is >= 1 and <= 32
        && user.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-');

    private static bool IsHost(string host)
    {
        if (host.Length is < 1 or > 253 || host.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var labels = host.Split('.');
        if (labels.Length == 4 && labels.All(static label => byte.TryParse(label, out _)))
        {
            return true;
        }

        foreach (var label in labels)
        {
            if (label.Length is < 1 or > 63 || label[0] == '-' || label[^1] == '-')
            {
                return false;
            }

            if (!label.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch == '-'))
            {
                return false;
            }
        }

        return true;
    }
}
