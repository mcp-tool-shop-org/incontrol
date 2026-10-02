using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Core.Assistant;
using InControl.Core.Models;
using InControl.Inference.Fakes;
using InControl.Services.Chat;
using InControl.Services.Storage;
using Xunit;

namespace InControl.Services.Tests.Chat;

public class ChatServiceRecallTests : IDisposable
{
    private readonly string _root;

    public ChatServiceRecallTests()
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
    public async Task Send_AddsProjectNoteAndInstructions_AndSkipsSiblingNotes()
    {
        var files = new FileStore(new Mock<ILogger<FileStore>>().Object, _root);
        var projects = new JsonProjectLibrary(files, new Mock<ILogger<JsonProjectLibrary>>().Object);
        var memory = new JsonSessionMemory(files, new Mock<ILogger<JsonSessionMemory>>().Object);
        var storage = new JsonConversationStorage(files, new Mock<ILogger<JsonConversationStorage>>().Object);
        var fake = new FakeInferenceClient().SetTokenDelay(TimeSpan.Zero).SetTokensPerResponse(8);
        fake.QueueResponse("Noted.");
        var chat = new ChatService(
            fake,
            storage,
            new Mock<ILogger<ChatService>>().Object,
            projects,
            memory);

        var project = await projects.CreateAsync("Harbour");
        await projects.UpdateInstructionsAsync(project.Id, "Speak plainly.");
        await memory.RememberAsync(Note("dock", "The dock closes at dusk", project.Id, sessionId: null));

        var sibling = await chat.CreateConversationAsync(model: "fake", projectId: project.Id);
        await memory.RememberAsync(Note("secret", "Sibling secret", project.Id, sibling.Id));
        var other = await projects.CreateAsync("Inland");
        await memory.RememberAsync(Note("inland", "Other project fact", other.Id, sessionId: null));

        var session = await chat.CreateConversationAsync(
            model: "fake",
            systemPrompt: "You are InControl.",
            projectId: project.Id);

        await foreach (var _ in chat.SendMessageAsync(session.Id, "When does the dock close?"))
        {
        }

        fake.LastRequest.Should().NotBeNull();
        var prompt = fake.LastRequest!.SystemPrompt;
        prompt.Should().Contain("You are InControl.");
        prompt.Should().Contain("Speak plainly.");
        prompt.Should().Contain("The dock closes at dusk");
        prompt.Should().NotContain("Sibling secret");
        prompt.Should().NotContain("Other project fact");
    }

    [Fact]
    public async Task Send_WithoutMemory_KeepsTheSessionPrompt()
    {
        var files = new FileStore(new Mock<ILogger<FileStore>>().Object, _root);
        var storage = new JsonConversationStorage(files, new Mock<ILogger<JsonConversationStorage>>().Object);
        var fake = new FakeInferenceClient().SetTokenDelay(TimeSpan.Zero);
        fake.QueueResponse("Ok.");
        var chat = new ChatService(fake, storage, new Mock<ILogger<ChatService>>().Object);

        var session = await chat.CreateConversationAsync(model: "fake", systemPrompt: "Be brief.");
        await foreach (var _ in chat.SendMessageAsync(session.Id, "Hello"))
        {
        }

        fake.LastRequest!.SystemPrompt.Should().Be("Be brief.");
        session = (await chat.GetConversationAsync(session.Id))!;
        session.ProjectId.Should().Be(ChatProject.GeneralId);
    }

    private static AssistantMemoryItem Note(string key, string value, Guid projectId, Guid? sessionId)
    {
        return AssistantMemoryItem.Create(
            MemoryType.Fact,
            sessionId is null ? MemoryScope.User : MemoryScope.Session,
            MemorySource.ExplicitUser,
            key,
            value,
            projectId: projectId,
            sessionId: sessionId);
    }
}
