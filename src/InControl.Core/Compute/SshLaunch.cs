namespace InControl.Core.Compute;

/// <summary>
/// Arguments for <c>ssh -N</c>. The identity file stays in the config, not on this command line.
/// There is no remote command, so the prompt cannot show up in the server's process list.
/// </summary>
public static class SshLaunch
{
    public static IReadOnlyList<string> Arguments(string configPath)
    {
        if (!SshEndpointInspector.IsSafePath(configPath))
        {
            throw new ArgumentException("Config path is not safe to pass to ssh.", nameof(configPath));
        }

        return ["-F", configPath, "-N", SshSessionConfig.HostAlias];
    }
}
