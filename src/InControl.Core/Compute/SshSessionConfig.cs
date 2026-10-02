using System.Text;

namespace InControl.Core.Compute;

/// <summary>
/// OpenSSH client config for one forward. The chat never becomes a remote command.
/// Both sides of the forward are numeric loopback, because Windows OpenSSH resolves
/// the name "localhost" to IPv6 and then misses an IPv4-only listener.
/// </summary>
public static class SshSessionConfig
{
    public const string HostAlias = "incontrol-compute";

    public static string Render(SshEndpoint endpoint, int localPort, string knownHostsFile)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var inspection = SshEndpointInspector.Inspect(endpoint);
        if (!inspection.CanDial)
        {
            throw new InvalidOperationException(inspection.Error);
        }

        if (localPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort));
        }

        if (!SshEndpointInspector.IsSafePath(knownHostsFile))
        {
            throw new ArgumentException("Known hosts path is not safe to write into an SSH config.", nameof(knownHostsFile));
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Host {HostAlias}");
        builder.AppendLine($"  HostName {endpoint.Host}");
        builder.AppendLine($"  User {endpoint.User}");
        builder.AppendLine($"  Port {endpoint.SshPort}");
        builder.AppendLine($"  IdentityFile \"{ToSshPath(endpoint.IdentityFile)}\"");
        builder.AppendLine("  IdentitiesOnly yes");
        builder.AppendLine("  ForwardAgent no");
        builder.AppendLine("  ClearAllForwardings yes");
        builder.AppendLine("  ExitOnForwardFailure yes");
        builder.AppendLine("  BatchMode yes");
        builder.AppendLine("  StrictHostKeyChecking accept-new");
        builder.AppendLine($"  UserKnownHostsFile \"{ToSshPath(knownHostsFile)}\"");
        builder.AppendLine("  ServerAliveInterval 30");
        builder.AppendLine("  ServerAliveCountMax 3");
        builder.AppendLine("  RequestTTY no");
        builder.AppendLine("  LogLevel ERROR");
        builder.AppendLine($"  LocalForward 127.0.0.1:{localPort} 127.0.0.1:{endpoint.RemoteOllamaPort}");
        return builder.ToString();
    }

    public static string ToSshPath(string path) => path.Replace('\\', '/');
}
