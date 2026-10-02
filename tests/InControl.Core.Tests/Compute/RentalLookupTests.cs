using System.Net;
using FluentAssertions;
using InControl.Core.Compute;
using Xunit;

namespace InControl.Core.Tests.Compute;

public class RentalLookupTests
{
    [Fact]
    public void DedicatedPort_IsNotTheLocalOllamaPort()
    {
        TunnelPort.DedicatedFor("http://127.0.0.1:11434").Should().Be(11436);
        TunnelPort.DedicatedFor("http://127.0.0.1:11436").Should().Be(11437);
        TunnelPort.DedicatedFor("http://127.0.0.1:11434").Should().NotBe(11434);
    }

    [Fact]
    public void OllamaVersion_ReadsAShortToken_AndIgnoresAnythingElse()
    {
        OllamaVersion.Read("{\"version\":\"0.35.0\"}").Should().Be("0.35.0");
        OllamaVersion.Read("{\"version\":\"not a version\"}").Should().BeNull();
        OllamaVersion.Read("[]").Should().BeNull();
    }

    [Fact]
    public async Task ReadyProbe_RefusesAPublicUrl_WithoutSending()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("sent"));
        var probe = new HttpOllamaReadyProbe(handler);

        var version = await probe.VersionAsync("http://203.0.113.10:11434", CancellationToken.None);

        version.Should().BeNull();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ReadyProbe_ReadsTheLoopbackVersion()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{\"version\":\"0.35.0\"}"));
        var probe = new HttpOllamaReadyProbe(handler);

        var version = await probe.VersionAsync("http://127.0.0.1:11436", CancellationToken.None);

        version.Should().Be("0.35.0");
        handler.Last.Should().NotBeNull();
        handler.Last!.RequestUri!.Host.Should().Be("127.0.0.1");
        handler.Last.RequestUri.AbsolutePath.Should().Be("/api/version");
    }

    [Fact]
    public void PodList_OffersTheDirectSshEndpoint_AndRefusesAPublishedOllama()
    {
        const string json = """
            [
              {
                "id": "pod-a",
                "name": "medium",
                "desiredStatus": "RUNNING",
                "publicIp": "203.0.113.10",
                "portMappings": { "22": 17432 },
                "ports": ["22/tcp"]
              },
              {
                "id": "pod-open",
                "name": "published",
                "desiredStatus": "RUNNING",
                "publicIp": "203.0.113.20",
                "portMappings": { "22": 17433 },
                "ports": ["22/tcp", "11434/http"]
              },
              {
                "id": "gone",
                "name": "old",
                "desiredStatus": "TERMINATED",
                "publicIp": "203.0.113.30",
                "portMappings": { "22": 22 },
                "ports": ["22/tcp"]
              }
            ]
            """;

        var pods = RunPodPodList.Read(json)!;

        pods.Should().HaveCount(2);
        pods[0].CanOffer.Should().BeTrue();
        pods[0].SshCommand.Should().Be("ssh -p 17432 root@203.0.113.10");
        SshConnectString.TryParse(pods[0].SshCommand, out var fields, out var error).Should().BeTrue(error);
        fields!.Host.Should().Be("203.0.113.10");
        fields.SshPort.Should().Be(17432);
        fields.User.Should().Be("root");
        pods[1].CanOffer.Should().BeFalse();
        pods[1].OllamaPublished.Should().BeTrue();
        pods[1].Refusal.Should().Be(ComputeNotice.RunPodOllamaPublished);
        pods[1].SshCommand.Should().BeEmpty();
        RunPodPodList.Summarize(pods.Where(static pod => pod.OllamaPublished).ToList())
            .Should().Contain("11434");
    }

    [Fact]
    public void PodList_RefusesTheRunPodProxyHost()
    {
        const string json = """
            [
              {
                "id": "proxy",
                "name": "proxy",
                "desiredStatus": "RUNNING",
                "publicIp": "ssh.runpod.io",
                "portMappings": { "22": 22 },
                "ports": ["22/tcp"]
              }
            ]
            """;

        var pod = RunPodPodList.Read(json)!.Single();

        pod.CanOffer.Should().BeFalse();
        pod.Refusal.Should().Be(ComputeNotice.RunPodProxyBlocked);
    }

    [Fact]
    public async Task Client_MissingKey_DoesNotCallRunPod()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("sent"));
        var client = new RunPodPodClient(() => "  ", handler);

        var lookup = await client.ListAsync();

        lookup.Reached.Should().BeFalse();
        lookup.Message.Should().Be(ComputeNotice.RunPodKeyMissing);
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Client_SendsTheKeyOnlyAsABearer_AndScrubsItFromTheMessage()
    {
        const string key = "rp_test_key_0123456789";
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, $$"""
            [
              {
                "id": "pod-a",
                "name": "medium {{key}}",
                "desiredStatus": "RUNNING",
                "publicIp": "203.0.113.10",
                "portMappings": { "22": 17432 },
                "ports": ["22/tcp"]
              }
            ]
            """));
        var client = new RunPodPodClient(() => key, handler);

        var lookup = await client.ListAsync();

        lookup.Reached.Should().BeTrue();
        handler.Last.Should().NotBeNull();
        handler.Last!.RequestUri!.ToString().Should().Be(RunPodPodClient.PodsUrl);
        handler.Last.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Last.Headers.Authorization.Parameter.Should().Be(key);
        lookup.Message.Should().NotContain(key);
        lookup.Pods.Single().Name.Should().NotContain(key);
        lookup.Pods.Single().SshCommand.Should().Be("ssh -p 17432 root@203.0.113.10");
    }

    [Fact]
    public void PodList_RejectsBlankAndWrappedAndStoppedPods()
    {
        RunPodPodList.Read(null).Should().BeNull();
        RunPodPodList.Read("  ").Should().BeNull();
        RunPodPodList.Read("{").Should().BeNull();
        RunPodPodList.Read("{}").Should().BeNull();
        RunPodPodList.Read("[1, {\"name\":\"no-id\"}]").Should().BeEmpty();

        const string wrapped = """
            { "pods": [
              { "id": "a", "name": "one", "desiredStatus": "RUNNING", "publicIp": "203.0.113.10", "portMappings": { "22": 2201 }, "ports": ["22/tcp"] },
              { "id": "b", "name": "two", "desiredStatus": "RUNNING", "publicIp": "203.0.113.11", "portMappings": { "22": "2202" }, "ports": ["22/tcp"] },
              { "id": "c", "name": "asleep", "desiredStatus": "EXITED", "publicIp": "", "ports": ["22/tcp"] }
            ] }
            """;

        var pods = RunPodPodList.Read(wrapped)!;
        pods.Should().HaveCount(3);
        pods.Count(static pod => pod.CanOffer).Should().Be(2);
        pods[1].SshCommand.Should().Be("ssh -p 2202 root@203.0.113.11");
        pods[2].CanOffer.Should().BeFalse();
        RunPodPodList.Summarize(pods).Should().Contain("2 pods");
        RunPodPodList.Summarize(pods.Where(static pod => pod.Id == "c").ToList()).Should().Be(ComputeNotice.RunPodLookupEmpty);
        RunPodPodList.Summarize(pods.Where(static pod => pod.OllamaPublished).ToList()).Should().Be(ComputeNotice.RunPodLookupEmpty);
    }

    [Fact]
    public void PodList_TruncatesAWildName_AndIgnoresANonPortList()
    {
        var longName = new string('n', 90);
        var json = $$"""
            [
              {
                "id": "wide",
                "name": "{{longName}}\u000d",
                "desiredStatus": "RUNNING",
                "publicIp": " 203.0.113.12 ",
                "portMappings": { "22": "nope" },
                "ports": { "22": "11434/http" }
              }
            ]
            """;

        var pod = RunPodPodList.Read(json)!.Single();

        pod.Name.Should().HaveLength(80);
        pod.Name.Should().NotContain("\r");
        pod.OllamaPublished.Should().BeFalse();
        pod.CanOffer.Should().BeFalse();
        pod.Host.Should().Be("203.0.113.12");
    }

    [Fact]
    public void VersionAndTunnelPort_CoverTheRefusals()
    {
        OllamaVersion.Read(null).Should().BeNull();
        OllamaVersion.Read(" ").Should().BeNull();
        OllamaVersion.Read("{").Should().BeNull();
        OllamaVersion.Read("{\"version\":1}").Should().BeNull();
        OllamaVersion.Read("{\"version\":\"  \"}").Should().BeNull();
        OllamaVersion.Clean(new string('1', 41)).Should().BeNull();
        OllamaVersion.Clean("1.2.3+cpu").Should().Be("1.2.3+cpu");

        TunnelPort.ParseLocal(null).Should().Be(11434);
        TunnelPort.ParseLocal("not a url").Should().Be(11434);
        TunnelPort.IsForwardPort(0, "http://127.0.0.1:11434").Should().BeFalse();
        TunnelPort.IsForwardPort(70000, "http://127.0.0.1:11434").Should().BeFalse();
        TunnelPort.IsForwardPort(11436, "http://127.0.0.1:11434").Should().BeTrue();
        TunnelPort.IsLoopbackProbe(null).Should().BeFalse();
        TunnelPort.IsLoopbackProbe("https://127.0.0.1:11436").Should().BeFalse();
        TunnelPort.IsLoopbackProbe("http://user@127.0.0.1:11436").Should().BeFalse();
        TunnelPort.IsLoopbackProbe("http://127.0.0.1:11436").Should().BeTrue();
    }

    [Fact]
    public async Task Client_UnreadableBody_AndANetworkMiss_StayQuiet()
    {
        const string key = "rp_test_key_0123456789";
        var bad = new RunPodPodClient(() => key, new RecordingHandler(_ => Json(HttpStatusCode.OK, "{")));
        var missed = new RunPodPodClient(() => key, new RecordingHandler(_ => throw new HttpRequestException("down")));

        var unreadable = await bad.ListAsync();
        var down = await missed.ListAsync();

        unreadable.Reached.Should().BeFalse();
        unreadable.Message.Should().Be("RunPod's pod list could not be read.");
        unreadable.Message.Should().NotContain(key);
        down.Reached.Should().BeFalse();
        down.Message.Should().Be("RunPod could not be reached.");
        down.Pods.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadyProbe_NonOkAndThrown_AreNotAVersion()
    {
        var denied = new HttpOllamaReadyProbe(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var thrown = new HttpOllamaReadyProbe(new RecordingHandler(_ => throw new HttpRequestException("down")));

        (await denied.VersionAsync("http://127.0.0.1:11436", CancellationToken.None)).Should().BeNull();
        (await thrown.VersionAsync("http://127.0.0.1:11436", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Client_HttpError_DoesNotEchoTheBody()
    {
        const string key = "rp_test_key_0123456789";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(key + " secret body")
        });
        var client = new RunPodPodClient(() => key, handler);

        var lookup = await client.ListAsync();

        lookup.Reached.Should().BeFalse();
        lookup.Message.Should().Be("RunPod returned HTTP 401.");
        lookup.Message.Should().NotContain(key);
        lookup.Pods.Should().BeEmpty();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public int Calls { get; private set; }

        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Last = request;
            return Task.FromResult(_respond(request));
        }
    }
}
