using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Core.Models;
using InControl.Inference.Fakes;
using InControl.Services.Chat;
using InControl.Services.Storage;
using Xunit;

namespace InControl.Services.Tests.Chat;

public class ChatServiceSendTests : IDisposable
{
    private readonly string _root;

    public ChatServiceSendTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"InControlTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Send_WhenNoModel_LeavesTheSessionEmpty()
    {
        var files = new FileStore(new Mock<ILogger<FileStore>>().Object, _root);
        var storage = new JsonConversationStorage(files, new Mock<ILogger<JsonConversationStorage>>().Object);
        var fake = new FakeInferenceClient().SetTokenDelay(TimeSpan.Zero);
        var chat = new ChatService(fake, storage, new Mock<ILogger<ChatService>>().Object);

        var session = await chat.CreateConversationAsync();
        var act = async () =>
        {
            await foreach (var _ in chat.SendMessageAsync(session.Id, "Hello"))
            {
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>();

        fake.ChatRequestCount.Should().Be(0);
        var live = await chat.GetConversationAsync(session.Id);
        live!.Messages.Should().BeEmpty();

        var reloaded = new ChatService(new FakeInferenceClient(), storage, new Mock<ILogger<ChatService>>().Object);
        var again = await reloaded.GetConversationAsync(session.Id);
        again!.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_WhenInferenceThrows_KeepsTheUserMessage()
    {
        var files = new FileStore(new Mock<ILogger<FileStore>>().Object, _root);
        var storage = new JsonConversationStorage(files, new Mock<ILogger<JsonConversationStorage>>().Object);
        var fake = new FakeInferenceClient().SetTokenDelay(TimeSpan.Zero);
        fake.QueueError(new InvalidOperationException("boom"));
        var chat = new ChatService(fake, storage, new Mock<ILogger<ChatService>>().Object);

        var session = await chat.CreateConversationAsync(model: "fake");
        var act = async () =>
        {
            await foreach (var _ in chat.SendMessageAsync(session.Id, "Hello"))
            {
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>();

        var live = await chat.GetConversationAsync(session.Id);
        live!.Messages.Should().ContainSingle(m => m.Role == MessageRole.User && m.Content == "Hello");
        live.Messages.Should().NotContain(m => m.Role == MessageRole.Assistant);

        var reloaded = new ChatService(new FakeInferenceClient(), storage, new Mock<ILogger<ChatService>>().Object);
        var again = await reloaded.GetConversationAsync(session.Id);
        again!.Messages.Should().ContainSingle(m => m.Role == MessageRole.User && m.Content == "Hello");
    }
}
