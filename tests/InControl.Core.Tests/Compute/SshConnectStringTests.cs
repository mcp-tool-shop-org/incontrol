using FluentAssertions;
using InControl.Core.Compute;
using Xunit;

namespace InControl.Core.Tests.Compute;

/// <summary>
/// The connect string is pasted from a rental's console, so it is untrusted text.
/// The parser only reads fields. <see cref="SshEndpointInspector"/> is the gate that decides
/// whether those fields may reach an OpenSSH config, and these tests exercise both.
/// </summary>
public class SshConnectStringTests
{
    private static SshConnectFields Parse(string text)
    {
        SshConnectString.TryParse(text, out var fields, out var error).Should().BeTrue(error);
        error.Should().BeNull();
        return fields!;
    }

    private static string Reject(string? text)
    {
        SshConnectString.TryParse(text, out var fields, out var error).Should().BeFalse();
        fields.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
        return error!;
    }

    private static SshEndpoint EndpointFor(SshConnectFields f) => new()
    {
        DisplayName = "pasted",
        User = f.User,
        Host = f.Host,
        SshPort = f.SshPort,
        IdentityFile = f.IdentityFile ?? "C:\\keys\\id_ed25519"
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Empty_AsksForTheCommand(string? text)
    {
        Reject(text).Should().Contain("Paste the ssh command");
    }

    [Theory]
    [InlineData("ssh root@10.0.0.1\nrm -rf /")]
    [InlineData("ssh root@10.0.0.1\r\n-i key")]
    [InlineData("ssh root@10.0.0.1\r")]
    public void MultipleLines_AreRefused(string text)
    {
        Reject(text).Should().Contain("one line");
    }

    [Fact]
    public void UnclosedQuote_IsRefused()
    {
        Reject("ssh -i \"C:\\keys\\my key root@10.0.0.1").Should().Contain("quote");
    }

    [Fact]
    public void DefaultPort_IsTwentyTwo_AndIdentityIsOptional()
    {
        var fields = Parse("ssh root@example.com");

        fields.User.Should().Be("root");
        fields.Host.Should().Be("example.com");
        fields.SshPort.Should().Be(22);
        fields.IdentityFile.Should().BeNull();
    }

    [Theory]
    [InlineData("ssh root@10.0.0.1")]
    [InlineData("SSH root@10.0.0.1")]
    [InlineData("Ssh root@10.0.0.1")]
    [InlineData("root@10.0.0.1")]
    [InlineData("   ssh   root@10.0.0.1   ")]
    [InlineData("ssh \"root@10.0.0.1\"")]
    public void LeadingSshWord_IsOptionalAndCaseInsensitive(string text)
    {
        var fields = Parse(text);

        fields.User.Should().Be("root");
        fields.Host.Should().Be("10.0.0.1");
    }

    [Theory]
    [InlineData("ssh -p 2222 root@h.example", 2222)]
    [InlineData("ssh -p2222 root@h.example", 2222)]
    [InlineData("ssh root@h.example -p 1", 1)]
    [InlineData("ssh root@h.example -p 65535", 65535)]
    [InlineData("ssh root@h.example:2200", 2200)]
    [InlineData("ssh root@h.example:65535", 65535)]
    public void PortForms_AreRead(string text, int expected)
    {
        Parse(text).SshPort.Should().Be(expected);
    }

    [Fact]
    public void PortOnTheHost_WinsOverEarlierDashP_AndLaterDashPWinsOverHost()
    {
        Parse("ssh -p 2222 root@h.example:3333").SshPort.Should().Be(3333);
        Parse("ssh root@h.example:3333 -p 2222").SshPort.Should().Be(2222);
    }

    [Theory]
    [InlineData("ssh -p root@h.example")]
    [InlineData("ssh root@h.example -p")]
    [InlineData("ssh -p 0 root@h.example")]
    [InlineData("ssh -p 65536 root@h.example")]
    [InlineData("ssh -p -1 root@h.example")]
    [InlineData("ssh -p abc root@h.example")]
    [InlineData("ssh -p99999 root@h.example")]
    [InlineData("ssh -p0 root@h.example")]
    [InlineData("ssh -p99999999999999999999 root@h.example")]
    public void BadDashP_IsRefused(string text)
    {
        Reject(text).Should().Contain("-p value is not a port");
    }

    [Theory]
    [InlineData("root@h.example:0")]
    [InlineData("root@h.example:65536")]
    [InlineData("root@h.example:99999999999999999999")]
    public void BadHostPort_IsRefused(string text)
    {
        Reject("ssh " + text).Should().Contain("host port is not a port");
    }

    [Theory]
    [InlineData("ssh root@h.example:abc")]
    [InlineData("ssh root@h.example:")]
    [InlineData("ssh root@:22")]
    public void HostWithAColonThatIsNotAPort_ParsesButTheInspectorRefusesIt(string text)
    {
        var fields = Parse(text);

        var inspection = SshEndpointInspector.Inspect(EndpointFor(fields));

        inspection.CanDial.Should().BeFalse();
        inspection.Error.Should().Contain("hostname or an IPv4 address");
    }

    [Theory]
    [InlineData("ssh -i key.pem root@h.example", "key.pem")]
    [InlineData("ssh -ikey.pem root@h.example", "key.pem")]
    [InlineData("ssh -i \"C:\\my keys\\k\" root@h.example", "C:\\my keys\\k")]
    [InlineData("ssh root@h.example -i /home/me/.ssh/id", "/home/me/.ssh/id")]
    public void IdentityForms_AreRead(string text, string expected)
    {
        Parse(text).IdentityFile.Should().Be(expected);
    }

    [Fact]
    public void LastIdentity_Wins()
    {
        Parse("ssh -i first -i second root@h.example").IdentityFile.Should().Be("second");
    }

    [Fact]
    public void DashIWithoutAPath_IsRefused()
    {
        Reject("ssh root@h.example -i").Should().Contain("-i flag needs a key file path");
    }

    [Fact]
    public void DashIFollowedByDash_IsNotTreatedAsAGluedKeyPath()
    {
        // "-i-foo" must not become identity "-foo"; it falls through to the unknown-flag refusal.
        Reject("ssh -i-foo root@h.example").Should().Contain("-i-foo");
    }

    [Theory]
    [InlineData("-N")]
    [InlineData("-n")]
    [InlineData("-T")]
    [InlineData("-t")]
    [InlineData("-A")]
    [InlineData("-a")]
    [InlineData("-v")]
    [InlineData("-vv")]
    [InlineData("-vvv")]
    [InlineData("-4")]
    [InlineData("-6")]
    [InlineData("-f")]
    [InlineData("-g")]
    public void HarmlessFlagsWithoutAValue_AreIgnored(string flag)
    {
        var fields = Parse($"ssh {flag} root@h.example");

        fields.Host.Should().Be("h.example");
    }

    [Theory]
    [InlineData("-o", "StrictHostKeyChecking=no")]
    [InlineData("-J", "jump@proxy.example")]
    [InlineData("-L", "8080:localhost:8080")]
    [InlineData("-R", "9000:localhost:9000")]
    [InlineData("-W", "host:22")]
    [InlineData("-c", "aes256-ctr")]
    [InlineData("-m", "hmac-sha2-256")]
    [InlineData("-F", "C:\\evil\\config")]
    public void FlagsThatTakeAValue_AreSkippedAndNeverReachTheFields(string flag, string value)
    {
        var fields = Parse($"ssh {flag} {value} root@h.example");

        fields.Host.Should().Be("h.example");
        fields.User.Should().Be("root");
        fields.IdentityFile.Should().BeNull();
        fields.SshPort.Should().Be(22);
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("-J")]
    [InlineData("-L")]
    [InlineData("-R")]
    [InlineData("-W")]
    [InlineData("-c")]
    [InlineData("-m")]
    [InlineData("-F")]
    [InlineData("-l")]
    public void ValueFlagAtTheEnd_IsRefused(string flag)
    {
        Reject($"ssh root@h.example {flag}").Should().Contain($"The {flag} flag is missing its value");
    }

    [Fact]
    public void DashL_SetsTheUser_BeforeOrAfterTheHostToken()
    {
        Parse("ssh -l admin root@h.example").User.Should().Be("admin");
        Parse("ssh root@h.example -l admin").User.Should().Be("admin");
    }

    [Theory]
    [InlineData("-X")]
    [InlineData("-D")]
    [InlineData("-w")]
    [InlineData("-oProxyCommand=calc.exe")]
    [InlineData("--help")]
    [InlineData("-")]
    [InlineData("-pabc")]
    public void UnknownFlags_AreRefusedByName(string flag)
    {
        Reject($"ssh {flag} root@h.example").Should().Contain($"does not use the {flag} flag");
    }

    [Theory]
    [InlineData("ssh h.example")]
    [InlineData("ssh root@")]
    [InlineData("ssh @h.example")]
    [InlineData("ssh @")]
    public void TokenWithoutAUserAndHost_IsRefused(string text)
    {
        Reject(text).Should().Contain("Expected user@host");
    }

    [Theory]
    [InlineData("ssh root@[::1]")]
    [InlineData("ssh root@[2001:db8::1]:22")]
    [InlineData("ssh root@host[1]")]
    public void IPv6Hosts_AreRefused(string text)
    {
        Reject(text).Should().Contain("IPv6");
    }

    [Fact]
    public void TwoHosts_AreRefused()
    {
        Reject("ssh root@a.example root@b.example").Should().Contain("more than one host");
    }

    [Theory]
    [InlineData("ssh")]
    [InlineData("ssh -N")]
    [InlineData("ssh -p 22")]
    [InlineData("ssh -i key")]
    public void NoHost_IsRefused(string text)
    {
        Reject(text).Should().Contain("needs a user@host");
    }

    [Fact]
    public void AtSignInTheUser_SplitsOnTheLastAt_AndTheInspectorRefusesThatUser()
    {
        var fields = Parse("ssh a@b@h.example");

        fields.User.Should().Be("a@b");
        fields.Host.Should().Be("h.example");
        SshEndpointInspector.Inspect(EndpointFor(fields)).CanDial.Should().BeFalse();
    }

    [Theory]
    [InlineData("ssh root@-oProxyCommand=calc.exe")]
    [InlineData("ssh root@-F")]
    [InlineData("ssh root@-host.example")]
    public void HostThatLooksLikeAFlag_ParsesButNeverPassesTheInspector(string text)
    {
        // The host token is data. It reaches OpenSSH only through a generated config file,
        // and only if the inspector accepts it.
        var fields = Parse(text);
        fields.Host.Should().StartWith("-");

        var inspection = SshEndpointInspector.Inspect(EndpointFor(fields));

        inspection.CanDial.Should().BeFalse();
        inspection.Error.Should().Contain("hostname or an IPv4 address");
    }

    [Theory]
    [InlineData("ssh root@host.example;calc")]
    [InlineData("ssh root@host.example&&calc")]
    [InlineData("ssh root@host.example|calc")]
    [InlineData("ssh root@$(calc)")]
    [InlineData("ssh root@`calc`")]
    [InlineData("ssh root@host.example>out")]
    [InlineData("ssh root@host.example%20x")]
    [InlineData("ssh root@host_example")]
    [InlineData("ssh root@host..example")]
    public void ShellMetacharactersInTheHost_NeverPassTheInspector(string text)
    {
        var fields = Parse(text);

        SshEndpointInspector.Inspect(EndpointFor(fields)).CanDial.Should().BeFalse();
    }

    [Theory]
    [InlineData("ssh ro;ot@host.example")]
    [InlineData("ssh \"ro ot@host.example\"")]
    [InlineData("ssh $(calc)@host.example")]
    [InlineData("ssh root:pw@host.example")]
    public void OddUsers_NeverPassTheInspector(string text)
    {
        var fields = Parse(text);

        var inspection = SshEndpointInspector.Inspect(EndpointFor(fields));

        inspection.CanDial.Should().BeFalse();
        inspection.Error.Should().Contain("plain name");
    }

    [Fact]
    public void QuotedTokens_KeepInnerSpaces_AndEmptyQuotesBecomeAnEmptyToken()
    {
        Parse("ssh \"-i\" \"C:\\a b\\k\" root@h.example").IdentityFile.Should().Be("C:\\a b\\k");
        Reject("ssh \"\" root@h.example").Should().Contain("Expected user@host");
    }

    [Fact]
    public void TabsAndRepeatedSpaces_SeparateTokens()
    {
        var fields = Parse("ssh\t-p\t2222   root@h.example\t-i\tkey");

        fields.SshPort.Should().Be(2222);
        fields.IdentityFile.Should().Be("key");
    }

    [Fact]
    public void RealRunPodAndVastStrings_Parse()
    {
        var runpod = Parse("ssh root@213.173.109.39 -p 17432 -i ~/.ssh/id_ed25519");
        runpod.Host.Should().Be("213.173.109.39");
        runpod.SshPort.Should().Be(17432);
        runpod.IdentityFile.Should().Be("~/.ssh/id_ed25519");

        var vast = Parse("ssh -p 41234 root@ssh5.vast.ai -L 8080:localhost:8080");
        vast.Host.Should().Be("ssh5.vast.ai");
        vast.SshPort.Should().Be(41234);
        SshEndpointInspector.IsVastProxy(vast.Host).Should().BeTrue();
    }
}
