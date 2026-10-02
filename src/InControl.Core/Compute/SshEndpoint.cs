namespace InControl.Core.Compute;

/// <summary>
/// One SSH login to a machine that runs Ollama on its own loopback.
/// <see cref="SshPort"/> is sshd. It is not Ollama's port.
/// </summary>
public sealed record SshEndpoint
{
    public required string DisplayName { get; init; }

    public required string User { get; init; }

    public required string Host { get; init; }

    public required int SshPort { get; init; }

    /// <summary>
    /// Path to the private key on this PC. The file's contents are never stored here.
    /// </summary>
    public required string IdentityFile { get; init; }

    /// <summary>
    /// Port Ollama is listening on inside the remote machine. Default 11434.
    /// </summary>
    public int RemoteOllamaPort { get; init; } = 11434;

    /// <summary>
    /// Filesystem-safe folder name for this login's known_hosts file.
    /// </summary>
    public string DirectoryKey
    {
        get
        {
            var raw = $"{User}_{Host}_{SshPort}";
            var chars = raw.Select(ch =>
                char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_').ToArray();
            return new string(chars);
        }
    }
}
