namespace InControl.Core.Compute;

/// <summary>
/// The chat's compute target. Local Ollama until the operator connects a rented machine.
/// Connecting opens an SSH local forward and points Ollama's HTTP client at 127.0.0.1 on this PC.
/// </summary>
public sealed class ComputeSession : IOllamaEndpoint, IAsyncDisposable
{
    private readonly string _localBaseUrl;
    private readonly ISshSessionFactory _sessions;
    private readonly ITcpProbe _probe;
    private readonly string _configRoot;
    private readonly Func<int> _reservePort;
    private readonly TimeSpan _connectTimeout;
    private readonly IOllamaReadyProbe? _ready;
    private readonly object _gate = new();

    private ISshSession? _session;
    private string _baseUrl;
    private bool _leaves;
    private string _notice;

    public ComputeSession(
        string localBaseUrl,
        ISshSessionFactory sessions,
        ITcpProbe probe,
        string configRoot,
        Func<int>? reservePort = null,
        TimeSpan? connectTimeout = null,
        IOllamaReadyProbe? ready = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(probe);
        if (string.IsNullOrWhiteSpace(configRoot))
        {
            throw new ArgumentException("Config root is required.", nameof(configRoot));
        }

        _localBaseUrl = string.IsNullOrWhiteSpace(localBaseUrl)
            ? "http://127.0.0.1:11434"
            : localBaseUrl;
        _sessions = sessions;
        _probe = probe;
        _configRoot = configRoot;
        _reservePort = reservePort ?? (() => TunnelPort.DedicatedFor(_localBaseUrl));
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(20);
        _ready = ready;
        _baseUrl = _localBaseUrl;
        _notice = ComputeNotice.OnThisPc;
    }

    public string BaseUrl
    {
        get
        {
            lock (_gate)
            {
                return _baseUrl;
            }
        }
    }

    public bool PromptsLeaveThisPc
    {
        get
        {
            lock (_gate)
            {
                return _leaves;
            }
        }
    }

    public string Notice
    {
        get
        {
            lock (_gate)
            {
                return _notice;
            }
        }
    }

    public event EventHandler? Changed;

    public async Task<ComputeConnectResult> ConnectAsync(SshEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var inspection = SshEndpointInspector.Inspect(endpoint);
        if (!inspection.CanDial)
        {
            return ComputeConnectResult.Fail(inspection.Error ?? ComputeNotice.TunnelClosed);
        }

        if (!File.Exists(endpoint.IdentityFile))
        {
            return ComputeConnectResult.Fail(ComputeNotice.MissingIdentity);
        }

        var localPort = _reservePort();
        if (!TunnelPort.IsForwardPort(localPort, _localBaseUrl))
        {
            return ComputeConnectResult.Fail(
                ComputeNotice.LocalForwardRefused(localPort, TunnelPort.ParseLocal(_localBaseUrl)));
        }

        if (_ready is null)
        {
            return ComputeConnectResult.Fail("Ollama through the tunnel was not checked. The chat stayed on this PC.");
        }

        var directory = Path.Combine(_configRoot, endpoint.DirectoryKey);
        Directory.CreateDirectory(directory);

        ISshSession session;
        try
        {
            session = await _sessions.OpenAsync(endpoint, localPort, directory, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
        {
            return ComputeConnectResult.Fail(Redact(ex.Message, endpoint.IdentityFile));
        }

        var deadline = DateTime.UtcNow + _connectTimeout;
        var sawTcp = false;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session.HasExited)
            {
                var message = WithHostKeyHint(Redact(session.StandardError, endpoint.IdentityFile));
                await session.DisposeAsync().ConfigureAwait(false);
                return ComputeConnectResult.Fail(message);
            }

            if (await _probe.CanConnectAsync("127.0.0.1", localPort, cancellationToken).ConfigureAwait(false))
            {
                sawTcp = true;
                var version = OllamaVersion.Clean(await _ready.VersionAsync(
                    $"http://127.0.0.1:{localPort}",
                    cancellationToken).ConfigureAwait(false));
                if (version is not null)
                {
                    await SwapAsync(session, localPort, endpoint).ConfigureAwait(false);
                    var message = ComputeNotice.TunnelUp(endpoint.DisplayName, endpoint.Host)
                        + " "
                        + ComputeNotice.OllamaAnswered(version);
                    if (inspection.VastProxyWarning)
                    {
                        message = inspection.Warning + " " + message;
                    }

                    return ComputeConnectResult.Ok(message);
                }
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        await session.DisposeAsync().ConfigureAwait(false);
        return ComputeConnectResult.Fail(sawTcp ? ComputeNotice.TunnelNotOllama : ComputeNotice.TunnelTimedOut);
    }

    public async Task UseThisPcAsync()
    {
        ISshSession? previous;
        lock (_gate)
        {
            previous = _session;
            _session = null;
            _baseUrl = _localBaseUrl;
            _leaves = false;
            _notice = ComputeNotice.OnThisPc;
        }

        if (previous is not null)
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ComputeConnectResult ForgetHostKey(SshEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var inspection = SshEndpointInspector.Inspect(endpoint, requireIdentity: false);
        var dialBlockedButFileMayExist = inspection.Error is ComputeNotice.RunPodProxyBlocked
            or ComputeNotice.OllamaPortIsNotSsh
            or ComputeNotice.MissingIdentity;
        if (!inspection.CanDial && !dialBlockedButFileMayExist)
        {
            return ComputeConnectResult.Fail(inspection.Error ?? "That login cannot own a host-key file.");
        }

        if (endpoint.DirectoryKey.Length == 0
            || !endpoint.DirectoryKey.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_'))
        {
            return ComputeConnectResult.Fail("That login cannot own a host-key file.");
        }

        var root = Path.GetFullPath(_configRoot);
        var file = Path.GetFullPath(Path.Combine(root, endpoint.DirectoryKey, "known_hosts"));
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return ComputeConnectResult.Fail("That login cannot own a host-key file.");
        }

        if (File.Exists(file))
        {
            File.Delete(file);
        }

        return ComputeConnectResult.Ok("Saved host key forgotten. The next connect treats this machine as new.");
    }

    public async ValueTask DisposeAsync()
    {
        await UseThisPcAsync().ConfigureAwait(false);
    }

    internal static string Redact(string? text, string identityFile)
    {
        var value = string.IsNullOrWhiteSpace(text) ? ComputeNotice.TunnelClosed : text.Trim();
        if (!string.IsNullOrEmpty(identityFile))
        {
            value = value.Replace(identityFile, ComputeNotice.IdentityRedaction, StringComparison.OrdinalIgnoreCase);
            var slashed = identityFile.Replace('\\', '/');
            value = value.Replace(slashed, ComputeNotice.IdentityRedaction, StringComparison.OrdinalIgnoreCase);
        }

        const int cap = 500;
        if (value.Length > cap)
        {
            value = value[..cap];
        }

        return value;
    }

    private static string WithHostKeyHint(string message)
    {
        if (message.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase))
        {
            return message + ComputeNotice.HostKeyChanged;
        }

        return message;
    }

    private async Task SwapAsync(ISshSession session, int localPort, SshEndpoint endpoint)
    {
        ISshSession? previous;
        lock (_gate)
        {
            previous = _session;
            _session = session;
            _baseUrl = $"http://127.0.0.1:{localPort}";
            _leaves = true;
            _notice = ComputeNotice.OnRemote(endpoint.DisplayName, endpoint.Host);
        }

        if (previous is not null)
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
