using FluentAssertions;
using InControl.Core.Models;
using InControl.Inference.Fakes;
using Xunit;

namespace InControl.Inference.Tests.Fakes;

public class FakeInferenceClientTests
{
    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_ByDefault()
    {
        var client = new FakeInferenceClient();

        var available = await client.IsAvailableAsync();

        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenConfigured()
    {
        var client = new FakeInferenceClient().SetAvailable(false);

        var available = await client.IsAvailableAsync();

        available.Should().BeFalse();
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsAddedModels()
    {
        var client = new FakeInferenceClient()
            .AddModel("llama3.2", 4_000_000_000)
            .AddModel("mistral");

        var models = await client.ListModelsAsync();

        models.Should().HaveCount(2);
        models.Should().Contain(m => m.Name == "llama3.2");
        models.Should().Contain(m => m.Name == "mistral");
    }

    [Fact]
    public async Task GetModelAsync_ReturnsModel_WhenExists()
    {
        var client = new FakeInferenceClient().AddModel("llama3.2");

        var model = await client.GetModelAsync("llama3.2");

        model.Should().NotBeNull();
        model!.Name.Should().Be("llama3.2");
    }

    [Fact]
    public async Task GetModelAsync_ReturnsNull_WhenNotExists()
    {
        var client = new FakeInferenceClient();

        var model = await client.GetModelAsync("nonexistent");

        model.Should().BeNull();
    }

    [Fact]
    public async Task StreamChatAsync_ReturnsQueuedResponse()
    {
        var client = new FakeInferenceClient()
            .QueueResponse("Hello, I am a helpful assistant!");

        var request = ChatRequest.Simple("llama3.2", "Hi");
        var tokens = new List<string>();
        await foreach (var token in client.StreamChatAsync(request))
        {
            tokens.Add(token);
        }

        var response = string.Join("", tokens);
        response.Should().Be("Hello, I am a helpful assistant!");
    }

    [Fact]
    public async Task StreamChatAsync_TracksRequestCount()
    {
        var client = new FakeInferenceClient();
        var request = ChatRequest.Simple("llama3.2", "Hi");

        await foreach (var _ in client.StreamChatAsync(request)) { }
        await foreach (var _ in client.StreamChatAsync(request)) { }

        client.ChatRequestCount.Should().Be(2);
    }

    [Fact]
    public async Task StreamChatAsync_CapturesLastRequest()
    {
        var client = new FakeInferenceClient();
        var request = ChatRequest.Simple("llama3.2", "What is 2+2?");

        await foreach (var _ in client.StreamChatAsync(request)) { }

        client.LastRequest.Should().NotBeNull();
        client.LastRequest!.Model.Should().Be("llama3.2");
    }

    [Fact]
    public async Task StreamChatAsync_ThrowsQueuedError()
    {
        var client = new FakeInferenceClient()
            .QueueError(new InvalidOperationException("Simulated failure"));

        var request = ChatRequest.Simple("llama3.2", "Hi");

        var action = async () =>
        {
            await foreach (var _ in client.StreamChatAsync(request)) { }
        };

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated failure");
    }

    [Fact]
    public async Task StreamChatAsync_ThrowsWhenUnavailable()
    {
        var client = new FakeInferenceClient().SetAvailable(false);
        var request = ChatRequest.Simple("llama3.2", "Hi");

        var action = async () =>
        {
            await foreach (var _ in client.StreamChatAsync(request)) { }
        };

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StreamChatAsync_SupportsCancellation()
    {
        var client = new FakeInferenceClient()
            .SetTokenDelay(TimeSpan.FromMilliseconds(100))
            .QueueResponse("This is a very long response that will take time to stream");

        var cts = new CancellationTokenSource();
        var request = ChatRequest.Simple("llama3.2", "Hi");
        var tokenCount = 0;

        var action = async () =>
        {
            await foreach (var _ in client.StreamChatAsync(request, cts.Token))
            {
                tokenCount++;
                if (tokenCount >= 2)
                {
                    cts.Cancel();
                }
            }
        };

        await action.Should().ThrowAsync<OperationCanceledException>();
        tokenCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ChatAsync_ReturnsCompleteResponse()
    {
        var client = new FakeInferenceClient()
            .QueueResponse("Complete response");

        var request = ChatRequest.Simple("llama3.2", "Hi");
        var response = await client.ChatAsync(request);

        response.Content.Should().Be("Complete response");
        response.Model.Should().Be("llama3.2");
        response.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenAvailable()
    {
        var client = new FakeInferenceClient()
            .AddModel("llama3.2");

        var health = await client.CheckHealthAsync();

        health.IsHealthy.Should().BeTrue();
        health.LoadedModels.Should().Be(1);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenUnavailable()
    {
        var client = new FakeInferenceClient().SetAvailable(false);

        var health = await client.CheckHealthAsync();

        health.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task SetLatency_SimulatesDelay()
    {
        var client = new FakeInferenceClient()
            .SetLatency(TimeSpan.FromMilliseconds(50));

        var start = DateTime.UtcNow;
        await client.IsAvailableAsync();
        var elapsed = DateTime.UtcNow - start;

        elapsed.TotalMilliseconds.Should().BeGreaterThan(40);
    }

    [Fact]
    public void BackendName_ReturnsFake()
    {
        var client = new FakeInferenceClient();

        client.BackendName.Should().Be("Fake");
    }
}
