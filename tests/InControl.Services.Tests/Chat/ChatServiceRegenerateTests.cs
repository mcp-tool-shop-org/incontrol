using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Core.Models;
using InControl.Inference.Fakes;
using InControl.Services.Chat;
using InControl.Services.Storage;
using Xunit;

namespace InControl.Services.Tests.Chat;

public class ChatServiceRegenerateTests : IDisposable
{
    private readonly string _root;

    public ChatServiceRegenerateTests()
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
    public async Task Regenerate_WhenInferenceThrows_KeepsTheStoredAnswer()
    {
        var files = new FileStore(new Mock<ILogger<FileStore>>().Object, _root);
        var storage = new JsonConversationStorage(files, new Mock<ILogger<JsonConversationStorage>>().Object);
        var fake = new FakeInferenceClient().SetTokenDelay(TimeSpan.Zero);
        fake.QueueResponse("Kept answer.");
        var chat = new ChatService(fake, storage, new Mock<ILogger<ChatService>>().Object);

        var session = await chat.CreateConversationAsync(model: "fake");
        await foreach (var _ in chat.SendMessageAsync(session.Id, "Hello"))
        {
        }

        fake.QueueError(new InvalidOperationException("boom"));
        var act = async () =>
        {
            await foreach (var _ in chat.RegenerateLastResponseAsync(session.Id))
            {
            }
        };
        await act.Should().ThrowAsync<InvalidOperationException>();

        var live = await chat.GetConversationAsync(session.Id);
        live!.Messages.Should().Contain(m => m.Role == MessageRole.Assistant && m.Content.Contains("Kept answer"));

        await chat.UpdateConversationAsync(session.Id, title: "Renamed");

        var reloaded = new ChatService(new FakeInferenceClient(), storage, new Mock<ILogger<ChatService>>().Object);
        var again = await reloaded.GetConversationAsync(session.Id);
        again!.Title.Should().Be("Renamed");
        again.Messages.Should().Contain(m => m.Role == MessageRole.Assistant && m.Content.Contains("Kept answer"));
    }
}
