using FluentAssertions;
using InControl.Core.Compute;
using Xunit;

namespace InControl.Core.Tests.Compute;

public class SshEndpointInspectorTests
{
    private static SshEndpoint Valid(Func<SshEndpoint, SshEndpoint>? change = null)
    {
        var endpoint = new SshEndpoint
        {
            DisplayName = "my pod",
            User = "root",
            Host = "203.0.113.10",
            SshPort = 17432,
            IdentityFile = "C:\\keys\\id_ed25519"
        };
        return change is null ? endpoint : change(endpoint);
    }

    [Fact]
    public void ValidLogin_CanDial_WithoutAWarning()
    {
        var result = SshEndpointInspector.Inspect(Valid());

        result.CanDial.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Warning.Should().BeNull();
        result.VastProxyWarning.Should().BeFalse();
    }

    [Fact]
    public void NullEndpoint_Throws()
    {
        var act = () => SshEndpointInspector.Inspect(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    [InlineData("tab\there")]
    public void DisplayName_MustBeOneNonBlankLine(string name)
    {
        var result = SshEndpointInspector.Inspect(Valid(e => e with { DisplayName = name }));

        result.CanDial.Should().BeFalse();
        result.Error.Should().Contain("one-line name");
    }

    [Fact]
    public void DisplayName_IsCappedAtEightyCharacters()
    {
        SshEndpointInspector.Inspect(Valid(e => e with { DisplayName = new string('a', 80) })).CanDial.Should().BeTrue();
        SshEndpointInspector.Inspect(Valid(e => e with { DisplayName = new string('a', 81) })).CanDial.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ro ot")]
    [InlineData("root;rm")]
    [InlineData("root\nBadOption yes")]
    [InlineData("r@oot")]
    [InlineData("rôot")]
    [InlineData("this-user-name-is-longer-than-thirty-two")]
    public void User_MustBeAPlainName(string user)
    {
        var result = SshEndpointInspector.Inspect(Valid(e => e with { User = user }));

        result.CanDial.Should().BeFalse();
        result.Error.Should().Contain("plain name");
    }

    [Theory]
    [InlineData("root")]
    [InlineData("ubuntu")]
    [InlineData("my.user_name-1")]
    [InlineData("7h9k2m4n6p-64411eb2")]
    public void User_AcceptsLettersDigitsDotUnderscoreDash(string user)
    {
        SshEndpointInspector.Inspect(Valid(e => e with { User = user })).CanDial.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("host name")]
    [InlineData("host..example")]
    [InlineData(".example")]
    [InlineData("example.")]
    [InlineData("-bad.example")]
    [InlineData("bad-.example")]
    [InlineData("exa_mple.com")]
    [InlineData("exa mple")]
    [InlineData("host\nname")]
    [InlineData("[::1]")]
    [InlineData("host:22")]
    [InlineData("-oProxyCommand=calc")]
    public void Host_MustBeAHostnameOrIPv4(string host)
    {
        var result = SshEndpointInspector.Inspect(Valid(e => e with { Host = host }));

        result.CanDial.Should().BeFalse();
        result.Error.Should().Contain("hostname or an IPv4 address");
    }

    [Fact]
    public void Host_LongerThanTheDnsLimit_IsRefused()
    {
        var label = new string('a', 63);
        var tooLong = string.Join('.', label, label, label, label, "x");
        tooLong.Length.Should().BeGreaterThan(253);

        SshEndpointInspector.Inspect(Valid(e => e with { Host = tooLong })).CanDial.Should().BeFalse();
        SshEndpointInspector.Inspect(Valid(e => e with { Host = new string('a', 64) })).CanDial.Should().BeFalse();
        SshEndpointInspector.Inspect(Valid(e => e with { Host = new string('a', 63) })).CanDial.Should().BeTrue();
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("pod-1.region.example.com")]
    [InlineData("127.0.0.1")]
    [InlineData("203.0.113.10")]
    [InlineData("999.1.1.1")]
    [InlineData("localhost")]
    public void Host_AcceptsNamesAndDottedNumbers(string host)
    {
        // 999.1.1.1 is not an IPv4 address, but it is a legal run of DNS labels, so it only
        // fails later at the network. Pinned so a change to that rule is deliberate.
        SshEndpointInspector.Inspect(Valid(e => e with { Host = host })).CanDial.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void SshPort_MustBeInRange(int port)
    {
        var result = SshEndpointInspector.Inspect(Valid(e => e with { SshPort = port }));

        result.CanDial.Should().BeFalse();
        result.Error.Should().Contain("SSH port");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void RemoteOllamaPort_MustBeInRange(int port)
    {
        var result = SshEndpointInspector.Inspect(Valid(e => e with { RemoteOllamaPort = port }));

        result.CanDial.Should().BeFalse();
        result.Error.Should().Contain("Ollama port");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(22)]
    [InlineData(65535)]
    public void PortBoundaries_AreAccepted(int port)
    {
        SshEndpointInspector.Inspect(Valid(e => e with { SshPort = port, RemoteOllamaPort = port })).CanDial.Should().BeTrue();
    }

    [Theory]
    [InlineData("ssh.runpod.io")]
    [InlineData("SSH.RUNPOD.IO")]
    public void RunPodProxy_IsRefused(string host)
    {
        SshEndpointInspector.IsRunPodProxy(host).Should().BeTrue();

        var result = SshEndpointInspector.Inspect(Valid(e => e with { Host = host }));

        result.CanDial.Should().BeFalse();
        result.Error.Should().Be(ComputeNotice.RunPodProxyBlocked);
    }

    [Theory]
    [InlineData("ssh.runpod.io.evil.example")]
    [InlineData("xssh.runpod.io")]
    public void OtherHostsContainingTheProxyName_AreNotTheProxy(string host)
    {
        SshEndpointInspector.IsRunPodProxy(host).Should().BeFalse();
    }

    [Theory]
    [InlineData("ssh2.vast.ai", true)]
    [InlineData("SSH10.VAST.AI", true)]
    [InlineData("ssh.vast.ai", false)]
    [InlineData("sshx.vast.ai", false)]
    [InlineData("ssh1a.vast.ai", false)]
    [InlineData("host.vast.ai", false)]
    [InlineData("ssh2.vast.ai.example.com", false)]
    [InlineData("ssh2.notvast.ai", false)]
    [InlineData("example.com", false)]
    public void VastProxy_IsRecognisedByItsSshDigitsLabel(string host, bool expected)
    {
        SshEndpointInspector.IsVastProxy(host).Should().Be(expected);
    }

    [Fact]
    public void VastProxy_WarnsButStillDials()
    {
        var result = SshEndpointInspector.Inspect(Valid(e => e with { Host = "ssh7.vast.ai" }));

        result.CanDial.Should().BeTrue();
        result.VastProxyWarning.Should().BeTrue();
        result.Warning.Should().Be(ComputeNotice.VastProxyWarning);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingIdentity_IsRefusedWhenRequired(string identity)
    {
        var result = SshEndpointInspector.Inspect(Valid(e => e with { IdentityFile = identity }));

        result.CanDial.Should().BeFalse();
        result.Error.Should().Be(ComputeNotice.MissingIdentity);
    }

    [Fact]
    public void MissingIdentity_IsAllowedWhenNotRequired()
    {
        SshEndpointInspector.Inspect(Valid(e => e with { IdentityFile = "" }), requireIdentity: false)
            .CanDial.Should().BeTrue();
    }

    [Theory]
    [InlineData("C:\\keys\\\"id")]
    [InlineData("C:\\keys\\id\nIdentityFile other")]
    [InlineData("C:\\keys\\id\rx")]
    [InlineData("C:\\keys\\id\0x")]
    public void IdentityPathWithAQuoteOrLineBreak_IsRefused_RequiredOrNot(string path)
    {
        var endpoint = Valid(e => e with { IdentityFile = path });

        SshEndpointInspector.Inspect(endpoint).CanDial.Should().BeFalse();
        var optional = SshEndpointInspector.Inspect(endpoint, requireIdentity: false);
        optional.CanDial.Should().BeFalse();
        optional.Error.Should().Contain("key path");
    }

    [Theory]
    [InlineData("C:\\keys\\id_ed25519", true)]
    [InlineData("/home/me/.ssh/id", true)]
    [InlineData("C:\\my keys\\id", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a\"b", false)]
    [InlineData("a\nb", false)]
    [InlineData("a\rb", false)]
    [InlineData("a\0b", false)]
    public void IsSafePath_RejectsBlanksQuotesAndLineBreaks(string path, bool expected)
    {
        SshEndpointInspector.IsSafePath(path).Should().Be(expected);
    }

    [Fact]
    public void DirectoryKey_ReplacesEverythingThatIsNotFileNameSafe()
    {
        var endpoint = Valid(e => e with { User = "a.b-c_d", Host = "host name/..\\x", SshPort = 22 });

        endpoint.DirectoryKey.Should().Be("a.b-c_d_host_name_.._x_22");
        endpoint.DirectoryKey.IndexOfAny(Path.GetInvalidFileNameChars()).Should().BeNegative();
    }

    [Fact]
    public void SessionConfig_RefusesAnEndpointTheInspectorBlocks()
    {
        var act = () => SshSessionConfig.Render(
            Valid(e => e with { Host = "ssh.runpod.io" }), 18080, "C:\\k\\known_hosts");

        act.Should().Throw<InvalidOperationException>().WithMessage(ComputeNotice.RunPodProxyBlocked);
    }

    [Fact]
    public void SessionConfig_RefusesNullEndpoint()
    {
        var act = () => SshSessionConfig.Render(null!, 18080, "C:\\k\\known_hosts");

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void SessionConfig_RefusesALocalPortOutOfRange(int port)
    {
        var act = () => SshSessionConfig.Render(Valid(), port, "C:\\k\\known_hosts");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("C:\\k\\\"known_hosts")]
    [InlineData("C:\\k\\known_hosts\nProxyCommand calc")]
    public void SessionConfig_RefusesAnUnsafeKnownHostsPath(string path)
    {
        var act = () => SshSessionConfig.Render(Valid(), 18080, path);

        act.Should().Throw<ArgumentException>().WithParameterName("knownHostsFile");
    }

    [Fact]
    public void SessionConfig_WritesTheLoginAndNothingElseFromTheEndpoint()
    {
        var config = SshSessionConfig.Render(Valid(), 18080, "C:\\k\\known_hosts");

        config.Should().Contain("Host incontrol-compute");
        config.Should().Contain("HostName 203.0.113.10");
        config.Should().Contain("User root");
        config.Should().Contain("Port 17432");
        config.Should().Contain("IdentitiesOnly yes");
        config.Should().Contain("UserKnownHostsFile \"C:/k/known_hosts\"");
        config.Should().NotContain("ProxyCommand");
        config.Should().NotContain("\\");
    }
}
