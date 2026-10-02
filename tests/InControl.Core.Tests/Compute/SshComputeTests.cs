using FluentAssertions;
using InControl.Core.Compute;
using Xunit;

namespace InControl.Core.Tests.Compute;

public class SshComputeTests
{
    [Fact]
    public void Parse_RunPodDirectCommand_ReadsUserHostPortAndKey()
    {
        const string command = "ssh -p 17432 root@213.173.109.39 -i C:\\keys\\id_ed25519";

        var parsed = SshConnectString.TryParse(command, out var fields, out var error);

        parsed.Should().BeTrue(error);
        fields!.User.Should().Be("root");
        fields.Host.Should().Be("213.173.109.39");
        fields.SshPort.Should().Be(17432);
        fields.IdentityFile.Should().Be("C:\\keys\\id_ed25519");
    }

    [Fact]
    public void Parse_UserHostColonPort_AndQuotedKey()
    {
        var parsed = SshConnectString.TryParse(
            "ssh -i \"C:\\keys\\my key\" root@10.0.0.8:2222",
            out var fields,
            out var error);

        parsed.Should().BeTrue(error);
        fields!.SshPort.Should().Be(2222);
        fields.IdentityFile.Should().Be("C:\\keys\\my key");
    }

    [Fact]
    public void Parse_RunPodProxy_IsALoginTheInspectorRefuses()
    {
        SshConnectString.TryParse(
            "ssh 7h9k2m4n6p-64411eb2@ssh.runpod.io -i C:\\keys\\id_ed25519",
            out var fields,
            out _).Should().BeTrue();

        var inspection = SshEndpointInspector.Inspect(Endpoint(fields!));

        inspection.CanDial.Should().BeFalse();
        inspection.Error.Should().Be(ComputeNotice.RunPodProxyBlocked);
    }

    [Fact]
    public void Inspect_VastProxy_WarnsAndStillAllows()
    {
        var inspection = SshEndpointInspector.Inspect(new SshEndpoint
        {
            DisplayName = "vast",
            User = "root",
            Host = "ssh2281.vast.ai",
            SshPort = 2281,
            IdentityFile = "C:\\keys\\id_ed25519"
        });

        inspection.CanDial.Should().BeTrue();
        inspection.VastProxyWarning.Should().BeTrue();
        inspection.Warning.Should().Contain("direct public-IP SSH");
    }

    [Fact]
    public void Inspect_SshPort11434_IsRefused()
    {
        var inspection = SshEndpointInspector.Inspect(new SshEndpoint
        {
            DisplayName = "wrong port",
            User = "root",
            Host = "203.0.113.10",
            SshPort = 11434,
            IdentityFile = "C:\\keys\\id_ed25519"
        });

        inspection.CanDial.Should().BeFalse();
        inspection.Error.Should().Be(ComputeNotice.OllamaPortIsNotSsh);
    }

    [Fact]
    public void Config_ForwardsLoopbackToLoopback_AndLaunchHidesTheKey()
    {
        var endpoint = new SshEndpoint
        {
            DisplayName = "pod",
            User = "root",
            Host = "203.0.113.10",
            SshPort = 17432,
            IdentityFile = "C:\\keys\\id_ed25519",
            RemoteOllamaPort = 11434
        };

        var config = SshSessionConfig.Render(endpoint, 18080, "C:\\AppData\\InControl\\ssh\\known_hosts");
        var arguments = SshLaunch.Arguments("C:\\AppData\\InControl\\ssh\\session.config");

        config.Should().Contain("LocalForward 127.0.0.1:18080 127.0.0.1:11434");
        config.Should().Contain("StrictHostKeyChecking accept-new");
        config.Should().Contain("ForwardAgent no");
        config.Should().Contain("BatchMode yes");
        config.Should().Contain("IdentityFile \"C:/keys/id_ed25519\"");
        config.Should().NotContain("0.0.0.0");
        config.Should().NotContain("localhost");
        arguments.Should().Equal("-F", "C:\\AppData\\InControl\\ssh\\session.config", "-N", SshSessionConfig.HostAlias);
        string.Join(" ", arguments).Should().NotContain("id_ed25519");
        string.Join(" ", arguments).Should().NotContain("203.0.113.10");
    }

    [Fact]
    public async Task Connect_PointsOllamaAtTheLocalForward_AndSaysTheChatLeaves()
    {
        var identity = NewKeyFile();
        var factory = new FakeFactory();
        var session = NewSession(factory, probeConnects: true);
        var endpoint = Endpoint(identity, "203.0.113.10", "pod-a");

        var result = await session.ConnectAsync(endpoint);

        result.Connected.Should().BeTrue();
        result.Message.Should().Contain("Prompts leave this PC");
        result.Message.Should().Contain(ComputeNotice.OllamaStillLocal);
        session.PromptsLeaveThisPc.Should().BeTrue();
        session.BaseUrl.Should().Be("http://127.0.0.1:18080");
        session.Notice.Should().Be("This chat is on pod-a (203.0.113.10). Prompts leave this PC.");
        factory.Opened.Should().NotBeNull();
    }

    [Fact]
    public async Task Connect_RunPodProxy_DoesNotOpenSsh()
    {
        var factory = new FakeFactory();
        var session = NewSession(factory, probeConnects: true);

        var result = await session.ConnectAsync(new SshEndpoint
        {
            DisplayName = "proxy",
            User = "root",
            Host = "ssh.runpod.io",
            SshPort = 22,
            IdentityFile = NewKeyFile()
        });

        result.Connected.Should().BeFalse();
        result.Message.Should().Be(ComputeNotice.RunPodProxyBlocked);
        factory.OpenCount.Should().Be(0);
        session.PromptsLeaveThisPc.Should().BeFalse();
        session.Notice.Should().Be(ComputeNotice.OnThisPc);
    }

    [Fact]
    public async Task Connect_WhenSshExits_RedactsTheKeyPath_AndStaysLocal()
    {
        var identity = NewKeyFile();
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                HasExited = true,
                StandardError = $"denied {identity.Replace('\\', '/')} HOST KEY VERIFICATION FAILED"
            }
        };
        var session = NewSession(factory, probeConnects: false);

        var result = await session.ConnectAsync(Endpoint(identity, "203.0.113.10", "pod-a"));

        result.Connected.Should().BeFalse();
        result.Message.Should().NotContain(identity);
        result.Message.Should().Contain(ComputeNotice.IdentityRedaction);
        result.Message.Should().Contain("Forget the saved host key");
        session.PromptsLeaveThisPc.Should().BeFalse();
        session.BaseUrl.Should().Be("http://127.0.0.1:11434");
    }

    [Fact]
    public async Task UseThisPc_DropsTheRemoteNotice()
    {
        var factory = new FakeFactory();
        var session = NewSession(factory, probeConnects: true);
        await session.ConnectAsync(Endpoint(NewKeyFile(), "203.0.113.10", "pod-a"));

        await session.UseThisPcAsync();

        session.PromptsLeaveThisPc.Should().BeFalse();
        session.Notice.Should().Be(ComputeNotice.OnThisPc);
        session.BaseUrl.Should().Be("http://127.0.0.1:11434");
        factory.Session.Disposed.Should().BeTrue();
    }

    private static ComputeSession NewSession(FakeFactory factory, bool probeConnects)
    {
        return new ComputeSession(
            "http://127.0.0.1:11434",
            factory,
            new FakeProbe(probeConnects),
            Path.Combine(Path.GetTempPath(), "incontrol-ssh-tests", Guid.NewGuid().ToString("N")),
            reservePort: () => 18080,
            connectTimeout: TimeSpan.FromMilliseconds(200));
    }

    private static SshEndpoint Endpoint(SshConnectFields fields) => new()
    {
        DisplayName = fields.Host,
        User = fields.User,
        Host = fields.Host,
        SshPort = fields.SshPort,
        IdentityFile = fields.IdentityFile ?? "C:\\keys\\id_ed25519"
    };

    private static SshEndpoint Endpoint(string identity, string host, string name) => new()
    {
        DisplayName = name,
        User = "root",
        Host = host,
        SshPort = 17432,
        IdentityFile = identity
    };

    private static string NewKeyFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "incontrol-ssh-tests", Guid.NewGuid().ToString("N") + ".key");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not-a-real-key");
        return path;
    }

    private sealed class FakeProbe : ITcpProbe
    {
        private readonly bool _connects;

        public FakeProbe(bool connects) => _connects = connects;

        public Task<bool> CanConnectAsync(string host, int port, CancellationToken cancellationToken) =>
            Task.FromResult(_connects);
    }

    private sealed class FakeFactory : ISshSessionFactory
    {
        public FakeSession Session { get; set; } = new();

        public int OpenCount { get; private set; }

        public SshEndpoint? Opened { get; private set; }

        public Task<ISshSession> OpenAsync(
            SshEndpoint endpoint,
            int localPort,
            string configDirectory,
            CancellationToken cancellationToken)
        {
            OpenCount++;
            Opened = endpoint;
            return Task.FromResult<ISshSession>(Session);
        }
    }

    private sealed class FakeSession : ISshSession
    {
        public bool HasExited { get; set; }

        public string StandardError { get; set; } = "";

        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
