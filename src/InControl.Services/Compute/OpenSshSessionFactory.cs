using System.Diagnostics;
using System.Text;
using InControl.Core.Compute;

namespace InControl.Services.Compute;

/// <summary>
/// Starts Windows OpenSSH with a config file and <c>-N</c>. The private key path is in the
/// config, not in the argument list. No remote command is sent.
/// </summary>
public sealed class OpenSshSessionFactory : ISshSessionFactory
{
    public async Task<ISshSession> OpenAsync(
        SshEndpoint endpoint,
        int localPort,
        string configDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        Directory.CreateDirectory(configDirectory);
        var configPath = Path.Combine(configDirectory, "session.config");
        var knownHosts = Path.Combine(configDirectory, "known_hosts");
        var rendered = SshSessionConfig.Render(endpoint, localPort, knownHosts);
        await File.WriteAllTextAsync(configPath, rendered, cancellationToken).ConfigureAwait(false);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ResolveSsh(),
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        foreach (var argument in SshLaunch.Arguments(configPath))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start ssh.");
        }

        return new OpenSshSession(process);
    }

    private static string ResolveSsh()
    {
        var windows = Path.Combine(Environment.SystemDirectory, "OpenSSH", "ssh.exe");
        return File.Exists(windows) ? windows : "ssh";
    }

    private sealed class OpenSshSession : ISshSession
    {
        private readonly Process _process;
        private readonly StringBuilder _stderr = new();
        private readonly Task _pump;

        public OpenSshSession(Process process)
        {
            _process = process;
            _pump = Task.Run(PumpStderr);
        }

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public string StandardError
        {
            get
            {
                lock (_stderr)
                {
                    return _stderr.ToString();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The pipe closed with the process.
            }

            _process.Dispose();
        }

        private async Task PumpStderr()
        {
            while (true)
            {
                string? line;
                try
                {
                    line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    break;
                }

                if (line is null)
                {
                    break;
                }

                lock (_stderr)
                {
                    if (_stderr.Length < 4000)
                    {
                        _stderr.AppendLine(line);
                    }
                }
            }
        }
    }
}
