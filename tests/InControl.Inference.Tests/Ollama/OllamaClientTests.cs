using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using InControl.Core.Compute;
using InControl.Core.Configuration;
using InControl.Core.Models;
using Xunit;

namespace InControl.Inference.Ollama;

public class OllamaClientTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    private sealed class TestEndpoint : IOllamaEndpoint
    {
        public TestEndpoint(string url) => BaseUrl = url;

        public string BaseUrl { get; set; }
        public bool PromptsLeaveThisPc => false;
        public string Notice => string.Empty;
        public event EventHandler? Changed;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private static OllamaInferenceClient NewClient(IOllamaEndpoint endpoint, int timeoutSeconds = 300) =>
        new(
            Options.Create(new OllamaOptions()),
            endpoint,
            NullLogger<OllamaInferenceClient>.Instance,
            Options.Create(new InferenceOptions { TimeoutSeconds = timeoutSeconds }));

    private static ChatRequest Request() => new()
    {
        Model = "m",
        Messages = [Message.User("hi")]
    };

    private static async Task<List<string>> ReadAll(IAsyncEnumerable<string> stream)
    {
        var tokens = new List<string>();
        await foreach (var token in stream)
            tokens.Add(token);
        return tokens;
    }

    [Fact]
    public async Task Stream_KeepsRunning_WhenTheEndpointRaisesChangedWithTheSameUrl()
    {
        var gate = new TaskCompletionSource();
        using var server = new FakeOllamaServer(async (_, write) =>
        {
            await write(FakeOllamaServer.ChatLine("a"));
            await gate.Task;
            await write(FakeOllamaServer.ChatLine("b"));
            await write(FakeOllamaServer.ChatLine("", done: true));
        });
        var endpoint = new TestEndpoint(server.Url);
        var client = NewClient(endpoint);

        var tokens = new List<string>();
        await foreach (var token in client.StreamChatAsync(Request()).WithCancellation(CancellationToken.None))
        {
            tokens.Add(token);
            if (tokens.Count == 1)
            {
                // Using this PC again while a reply is running must not abort it.
                endpoint.Raise();
                gate.SetResult();
            }
        }

        tokens.Should().Equal("a", "b");
    }

    [Fact]
    public async Task Stream_KeepsRunning_WhenTheEndpointMovesToAnotherUrlMidReply()
    {
        var gate = new TaskCompletionSource();
        using var server = new FakeOllamaServer(async (_, write) =>
        {
            await write(FakeOllamaServer.ChatLine("a"));
            await gate.Task;
            await write(FakeOllamaServer.ChatLine("b"));
            await write(FakeOllamaServer.ChatLine("", done: true));
        });
        var endpoint = new TestEndpoint(server.Url);
        var client = NewClient(endpoint);

        var tokens = new List<string>();
        await foreach (var token in client.StreamChatAsync(Request()).WithCancellation(CancellationToken.None))
        {
            tokens.Add(token);
            if (tokens.Count == 1)
            {
                endpoint.BaseUrl = "http://127.0.0.1:1";
                endpoint.Raise();
                gate.SetResult();
            }
        }

        tokens.Should().Equal("a", "b");
    }

    [Fact]
    public async Task Stream_IsNotCutByTheTimeout_WhileTokensKeepArriving()
    {
        using var server = new FakeOllamaServer(async (_, write) =>
        {
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(500);
                await write(FakeOllamaServer.ChatLine(i.ToString()));
            }
            await write(FakeOllamaServer.ChatLine("", done: true));
        });
        // The reply takes about 2.5 seconds, longer than the one second limit.
        var client = NewClient(new TestEndpoint(server.Url), timeoutSeconds: 1);

        var tokens = await ReadAll(client.StreamChatAsync(Request())).WaitAsync(Wait);

        tokens.Should().Equal("0", "1", "2", "3", "4");
    }

    [Fact]
    public async Task Stream_Throws_WhenNothingArrivesWithinTheTimeout()
    {
        var never = new TaskCompletionSource();
        using var server = new FakeOllamaServer(async (_, write) =>
        {
            await write(FakeOllamaServer.ChatLine("a"));
            await never.Task;
        });
        var client = NewClient(new TestEndpoint(server.Url), timeoutSeconds: 1);

        var act = async () => await ReadAll(client.StreamChatAsync(Request())).WaitAsync(Wait);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Pull_KeepsRunning_WhenTheEndpointChangesMidPull()
    {
        var gate = new TaskCompletionSource();
        using var server = new FakeOllamaServer(async (path, write) =>
        {
            if (path.Contains("pull"))
            {
                await write(FakeOllamaServer.PullLine("pulling"));
                await gate.Task;
                await write(FakeOllamaServer.PullLine("success"));
            }
            else
            {
                await write("{\"models\":[]}");
            }
        });
        var endpoint = new TestEndpoint(server.Url);
        var manager = new OllamaModelManager(
            endpoint,
            NullLogger<OllamaModelManager>.Instance,
            Options.Create(new InferenceOptions()));
        manager.DownloadProgress += (_, e) =>
        {
            if (e.Status == "pulling")
            {
                endpoint.Raise();
                gate.TrySetResult();
            }
        };

        var pulled = await manager.PullModelAsync("m").WaitAsync(Wait);

        pulled.Id.Should().Be("m");
    }

    [Fact]
    public void Client_HasNoHttpClientTimeout_SoALongReplyIsNotCutAt100Seconds()
    {
        var endpoint = new TestEndpoint("http://127.0.0.1:11434");
        var cache = new OllamaClientCache(endpoint);

        using var lease = cache.Acquire();

        lease.Entry.Http.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void Cache_DisposesAnOldClient_OnlyAfterItsLastCallEnds()
    {
        var endpoint = new TestEndpoint("http://127.0.0.1:11434");
        var cache = new OllamaClientCache(endpoint);

        var lease = cache.Acquire();
        var first = lease.Entry;
        endpoint.BaseUrl = "http://127.0.0.1:22222";
        endpoint.Raise();

        first.IsDisposed.Should().BeFalse("a call is still using it");
        using (var next = cache.Acquire())
        {
            next.Entry.Should().NotBeSameAs(first);
            next.Entry.Url.Should().Be("http://127.0.0.1:22222");
        }

        lease.Dispose();
        first.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Cache_KeepsTheClient_WhenChangedFiresWithTheSameUrl()
    {
        var endpoint = new TestEndpoint("http://127.0.0.1:11434");
        var cache = new OllamaClientCache(endpoint);

        OllamaClientCache.Entry first;
        using (var lease = cache.Acquire())
            first = lease.Entry;

        endpoint.Raise();

        first.IsDisposed.Should().BeFalse();
        using var again = cache.Acquire();
        again.Entry.Should().BeSameAs(first);
    }

    [Fact]
    public void Messages_CarryImagesToOllama_AndLeaveTextOnlyMessagesWithout()
    {
        var request = new ChatRequest
        {
            Model = "llava",
            SystemPrompt = "Be brief.",
            Messages =
            [
                Message.User("What is this?", ["QUJD"]),
                Message.Assistant("A chart."),
                Message.User("Thanks")
            ]
        };

        var messages = OllamaInferenceClient.ToOllamaMessages(request);

        messages.Should().HaveCount(4);
        messages[0].Content.Should().Be("Be brief.");
        messages[1].Images.Should().Equal("QUJD");
        messages[2].Images.Should().BeNull();
        messages[3].Images.Should().BeNull();
    }

    [Fact]
    public async Task Stream_WithNoTokens_ThrowsInsteadOfAnEmptyReply_AndNamesImagesWhenSent()
    {
        using var server = new FakeOllamaServer(async (_, write) =>
            await write(FakeOllamaServer.ChatLine("", done: true)));
        var client = NewClient(new TestEndpoint(server.Url));
        var withImage = new ChatRequest { Model = "llama3.1:8b", Messages = [Message.User("what is this", ["QUJD"])] };

        var textOnly = async () => await ReadAll(client.StreamChatAsync(Request()));
        var image = async () => await ReadAll(client.StreamChatAsync(withImage));

        (await textOnly.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be("m sent back no reply.");
        (await image.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("may not read images");
    }

    [Fact]
    public async Task TextOnlyModel_LeavesOldImagesOut_AndRefusesANewOneBeforeSending()
    {
        var chatCalls = 0;
        using var server = new FakeOllamaServer(async (path, write) =>
        {
            if (path.StartsWith("/api/show", StringComparison.Ordinal))
            {
                await write("{\"capabilities\":[\"completion\",\"tools\"]}");
                return;
            }

            Interlocked.Increment(ref chatCalls);
            await write(FakeOllamaServer.ChatLine("ok"));
            await write(FakeOllamaServer.ChatLine("", done: true));
        });
        var client = NewClient(new TestEndpoint(server.Url));

        var earlierImage = new ChatRequest
        {
            Model = "llama3.1:8b",
            Messages = [Message.User("what is this", ["QUJD"]), Message.Assistant("..."), Message.User("thanks")]
        };
        var newImage = new ChatRequest
        {
            Model = "llama3.1:8b",
            Messages = [Message.User("and this?", ["QUJD"])]
        };

        (await ReadAll(client.StreamChatAsync(earlierImage)).WaitAsync(Wait)).Should().Equal("ok");

        var refused = async () => await ReadAll(client.StreamChatAsync(newImage));
        (await refused.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("can't read images");
        chatCalls.Should().Be(1);
    }

    [Fact]
    public async Task Stream_PassesThinkingToTheCallback_AndYieldsOnlyTheAnswer()
    {
        using var server = new FakeOllamaServer(async (_, write) =>
        {
            await write("{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"thinking\":\"Let me \"},\"done\":false}");
            await write("{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"thinking\":\"check.\"},\"done\":false}");
            await write(FakeOllamaServer.ChatLine("Answer"));
            await write(FakeOllamaServer.ChatLine("", done: true));
        });
        var client = NewClient(new TestEndpoint(server.Url));
        var thinking = new System.Text.StringBuilder();
        var request = Request() with { OnThinking = chunk => thinking.Append(chunk) };

        var tokens = await ReadAll(client.StreamChatAsync(request)).WaitAsync(Wait);

        tokens.Should().Equal("Answer");
        thinking.ToString().Should().Be("Let me check.");
    }
}
