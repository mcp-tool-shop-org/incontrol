using FluentAssertions;
using InControl.Core.Compute;
using Xunit;

namespace InControl.Core.Tests.Compute;

/// <summary>
/// Edges of the rental session: error text that may contain the key path, host-key files
/// that must stay inside the config root, and constructor guards.
/// </summary>
public class ComputeSessionEdgeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "compute-edge-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ComputeSession NewSession(
        FakeFactory? factory = null,
        string localBaseUrl = "http://127.0.0.1:11434",
        string? root = null) =>
        new(
            localBaseUrl,
            factory ?? new FakeFactory(),
            new FakeProbe(),
            root ?? _root,
            reservePort: () => 18080,
            connectTimeout: TimeSpan.FromMilliseconds(50),
            ready: new FakeReady());

    private static SshEndpoint Endpoint(string host = "203.0.113.10", string user = "root", int port = 17432) => new()
    {
        DisplayName = "pod",
        User = user,
        Host = host,
        SshPort = port,
        IdentityFile = "C:\\keys\\id_ed25519"
    };

    #region Redact

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Redact_BlankText_BecomesTheTunnelClosedNotice(string? text)
    {
        ComputeSession.Redact(text, "C:\\keys\\id").Should().Be(ComputeNotice.TunnelClosed);
    }

    [Fact]
    public void Redact_RemovesTheKeyPath_InBothSlashStyles_AndAnyCase()
    {
        var text = "Load key \"C:\\Keys\\ID_ed25519\": bad; also C:/keys/id_ed25519 and c:\\KEYS\\id_ed25519";

        var redacted = ComputeSession.Redact(text, "C:\\keys\\id_ed25519");

        redacted.Should().NotContainEquivalentOf("id_ed25519");
        redacted.Should().Contain(ComputeNotice.IdentityRedaction);
    }

    [Fact]
    public void Redact_NoIdentity_LeavesTheTextAlone_ButTrimsIt()
    {
        ComputeSession.Redact("  permission denied  ", "").Should().Be("permission denied");
    }

    [Fact]
    public void Redact_CapsTheMessageAtFiveHundredCharacters()
    {
        var redacted = ComputeSession.Redact(new string('x', 2000), "C:\\k");

        redacted.Should().HaveLength(500);
    }

    #endregion

    #region Host-key file

    [Fact]
    public void ForgetHostKey_RequiresAnEndpoint_AndAConfigRoot()
    {
        var session = NewSession();

        var noEndpoint = () => session.ForgetHostKey(null!);
        var noRoot = () => NewSession(root: " ");

        noEndpoint.Should().Throw<ArgumentNullException>();
        noRoot.Should().Throw<ArgumentException>().WithParameterName("configRoot");
    }

    [Theory]
    [InlineData("ssh.runpod.io", "root", 22)]
    [InlineData("203.0.113.10", "root", 11434)]
    public void ForgetHostKey_StillWorksForLoginsThatCannotBeDialled_BecauseAFileMayExist(string host, string user, int port)
    {
        var session = NewSession();
        var endpoint = Endpoint(host, user, port);
        var hosts = Path.Combine(_root, endpoint.DirectoryKey, "known_hosts");
        Directory.CreateDirectory(Path.GetDirectoryName(hosts)!);
        File.WriteAllText(hosts, "entry");

        var result = session.ForgetHostKey(endpoint);

        result.Connected.Should().BeTrue();
        File.Exists(hosts).Should().BeFalse();
    }

    [Theory]
    [InlineData("bad host", "root")]
    [InlineData("203.0.113.10", "ro/ot")]
    [InlineData("203.0.113.10", "..\\..")]
    [InlineData("-bad.example", "root")]
    public void ForgetHostKey_RefusesLoginsThatCouldNotNameAFolder_AndDeletesNothing(string host, string user)
    {
        var survivor = Path.Combine(_root, "known_hosts");
        Directory.CreateDirectory(_root);
        File.WriteAllText(survivor, "keep");
        var session = NewSession();

        var result = session.ForgetHostKey(Endpoint(host, user));

        result.Connected.Should().BeFalse();
        File.Exists(survivor).Should().BeTrue();
    }

    [Fact]
    public void ForgetHostKey_OnlyDeletesTheNamedLoginsFile()
    {
        var session = NewSession();
        var mine = Endpoint("203.0.113.10");
        var other = Endpoint("203.0.113.11");
        foreach (var endpoint in new[] { mine, other })
        {
            var file = Path.Combine(_root, endpoint.DirectoryKey, "known_hosts");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "entry");
        }

        session.ForgetHostKey(mine);

        File.Exists(Path.Combine(_root, mine.DirectoryKey, "known_hosts")).Should().BeFalse();
        File.Exists(Path.Combine(_root, other.DirectoryKey, "known_hosts")).Should().BeTrue();
    }

    #endregion

    #region Connect messages

    [Theory]
    [InlineData("@@@ WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED! @@@")]
    [InlineData("Host key verification failed.")]
    public async Task Connect_WhenSshSaysTheHostKeyIsWrong_AddsTheForgetHint(string stderr)
    {
        var factory = new FakeFactory { Session = new FakeSession { HasExited = true, StandardError = stderr } };
        var session = NewSession(factory);
        Directory.CreateDirectory(_root);
        var key = Path.Combine(_root, "id_ed25519");
        File.WriteAllText(key, "not-a-real-key");

        var result = await session.ConnectAsync(Endpoint() with { IdentityFile = key });

        result.Connected.Should().BeFalse();
        result.Message.Should().EndWith(ComputeNotice.HostKeyChanged);
    }

    [Fact]
    public async Task Connect_OtherSshFailures_DoNotGetTheHostKeyHint()
    {
        var factory = new FakeFactory { Session = new FakeSession { HasExited = true, StandardError = "Permission denied (publickey)." } };
        var session = NewSession(factory);
        Directory.CreateDirectory(_root);
        var key = Path.Combine(_root, "id_ed25519");
        File.WriteAllText(key, "not-a-real-key");

        var result = await session.ConnectAsync(Endpoint() with { IdentityFile = key });

        result.Connected.Should().BeFalse();
        result.Message.Should().Contain("Permission denied");
        result.Message.Should().NotContain(ComputeNotice.HostKeyChanged);
    }

    [Fact]
    public void ABlankLocalBaseUrl_FallsBackToTheDefaultOllamaAddress()
    {
        var session = NewSession(localBaseUrl: " ");

        session.BaseUrl.Should().Be("http://127.0.0.1:11434");
        session.PromptsLeaveThisPc.Should().BeFalse();
        session.Notice.Should().Be(ComputeNotice.OnThisPc);
    }

    [Fact]
    public void Constructor_RequiresTheFactoryAndProbe()
    {
        var noFactory = () => new ComputeSession("http://127.0.0.1:11434", null!, new FakeProbe(), _root);
        var noProbe = () => new ComputeSession("http://127.0.0.1:11434", new FakeFactory(), null!, _root);

        noFactory.Should().Throw<ArgumentNullException>();
        noProbe.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ASessionExitedEventFromSomethingElse_IsIgnored()
    {
        var factory = new FakeFactory();
        var session = NewSession(factory);

        // A session that was never adopted raising Exited must not change the state.
        factory.Session.RaiseExited();

        session.Notice.Should().Be(ComputeNotice.OnThisPc);
        await session.DisposeAsync();
    }

    #endregion

    private sealed class FakeReady : IOllamaReadyProbe
    {
        public Task<string?> VersionAsync(string baseUrl, CancellationToken cancellationToken) => Task.FromResult<string?>("0.35.0");
    }

    private sealed class FakeProbe : ITcpProbe
    {
        public Task<bool> CanConnectAsync(string host, int port, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeFactory : ISshSessionFactory
    {
        public FakeSession Session { get; set; } = new();

        public Task<ISshSession> OpenAsync(
            SshEndpoint endpoint, int localPort, string configDirectory, CancellationToken cancellationToken) =>
            Task.FromResult<ISshSession>(Session);
    }

    private sealed class FakeSession : ISshSession
    {
        public bool HasExited { get; set; }

        public string StandardError { get; set; } = "";

        public event EventHandler? Exited;

        public void RaiseExited() => Exited?.Invoke(this, EventArgs.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
