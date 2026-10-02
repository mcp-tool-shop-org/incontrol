using FluentAssertions;
using InControl.Core.Errors;
using InControl.Core.Models;
using InControl.Core.State;
using Xunit;

namespace InControl.Core.Tests.State;

public class SerializationTests
{
    [Fact]
    public void Message_RoundTrip_PreservesAllProperties()
    {
        var original = new Message
        {
            Id = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            Role = MessageRole.User,
            Content = "Hello, world!",
            CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            Model = "llama3.2",
            TokenCount = 42
        };

        var json = StateSerializer.Serialize(original);
        var result = StateSerializer.Deserialize<Message>(json);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Conversation_RoundTrip_PreservesMessagesOrder()
    {
        var messages = new[]
        {
            Message.User("First"),
            Message.Assistant("Second"),
            Message.User("Third")
        };

        var original = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "Test Conversation",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
            Model = "llama3.2",
            SystemPrompt = "You are helpful.",
            Messages = messages
        };

        var json = StateSerializer.Serialize(original);
        var result = StateSerializer.Deserialize<Conversation>(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Messages.Should().HaveCount(3);
        result.Value.Messages[0].Content.Should().Be("First");
        result.Value.Messages[1].Content.Should().Be("Second");
        result.Value.Messages[2].Content.Should().Be("Third");
    }

    [Fact]
    public void ModelSelectionState_RoundTrip_PreservesAllProperties()
    {
        var original = ModelSelectionState.WithModel(
            "llama3.2",
            "Ollama",
            ["llama3.2", "mistral", "codellama"]);

        var json = StateSerializer.Serialize(original);
        var result = StateSerializer.Deserialize<ModelSelectionState>(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SelectedModel.Should().Be("llama3.2");
        result.Value.Backend.Should().Be("Ollama");
        result.Value.AvailableModels.Should().BeEquivalentTo(["llama3.2", "mistral", "codellama"]);
    }

    [Fact]
    public void AppState_RoundTrip_PreservesCompleteState()
    {
        var conversation1 = Conversation.Create("First", "llama3.2")
            .WithMessage(Message.User("Hello"))
            .WithMessage(Message.Assistant("Hi there!"));

        var conversation2 = Conversation.Create("Second", "mistral");

        var original = AppState.Initial()
            .WithConversation(conversation1)
            .WithConversation(conversation2)
            .WithActiveConversation(conversation1.Id)
            .WithModelSelection(ModelSelectionState.WithModel("llama3.2"));

        var json = StateSerializer.Serialize(original);
        var result = StateSerializer.Deserialize<AppState>(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Conversations.Should().HaveCount(2);
        result.Value.ActiveConversationId.Should().Be(conversation1.Id);
        result.Value.ActiveConversation.Should().NotBeNull();
        result.Value.ActiveConversation!.Messages.Should().HaveCount(2);
    }

    [Fact]
    public void Serialize_UsesStableOrdering()
    {
        var state1 = new Message
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Role = MessageRole.User,
            Content = "Test",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var json1 = StateSerializer.Serialize(state1);
        var json2 = StateSerializer.Serialize(state1);

        json1.Should().Be(json2, "serialization should be deterministic");
    }

    [Fact]
    public void Serialize_Compact_ProducesMinimalJson()
    {
        var state = Message.User("Hello");

        var normal = StateSerializer.Serialize(state);
        var compact = StateSerializer.Serialize(state, compact: true);

        compact.Should().NotContain("\n");
        compact.Should().NotContain("  ");
        compact.Length.Should().BeLessThan(normal.Length);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsFailure()
    {
        var result = StateSerializer.Deserialize<Message>("not valid json");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.DeserializationFailed);
    }

    [Fact]
    public void Deserialize_EmptyJson_ReturnsFailure()
    {
        var result = StateSerializer.Deserialize<Message>("null");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SerializeAsync_WritesToStream()
    {
        var state = Message.User("Async test");
        using var stream = new MemoryStream();

        await StateSerializer.SerializeAsync(stream, state);

        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeserializeAsync_ReadsFromStream()
    {
        var original = Message.User("Stream test");
        using var stream = new MemoryStream();
        await StateSerializer.SerializeAsync(stream, original);
        stream.Position = 0;

        var result = await StateSerializer.DeserializeAsync<Message>(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("Stream test");
    }

    [Fact]
    public void SerializeToBytes_ProducesValidUtf8()
    {
        var state = Message.User("UTF-8 test: 日本語");

        var bytes = StateSerializer.SerializeToBytes(state);
        var result = StateSerializer.Deserialize<Message>(bytes);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("UTF-8 test: 日本語");
    }

    [Fact]
    public void Enums_SerializeAsCamelCaseStrings()
    {
        var message = Message.User("Test");

        var json = StateSerializer.Serialize(message);

        json.Should().Contain("\"role\": \"user\"");
    }

    [Fact]
    public void NullValues_AreOmittedFromJson()
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Role = MessageRole.User,
            Content = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
            Model = null,
            TokenCount = null
        };

        var json = StateSerializer.Serialize(message);

        json.Should().NotContain("model");
        json.Should().NotContain("tokenCount");
    }
}

public class AppStateTests
{
    [Fact]
    public void Initial_CreatesEmptyState()
    {
        var state = AppState.Initial();

        state.Conversations.Should().BeEmpty();
        state.ActiveConversationId.Should().BeNull();
        state.ModelSelection.Should().NotBeNull();
        state.Version.Should().Be(1);
    }

    [Fact]
    public void WithConversation_AddsToList()
    {
        var state = AppState.Initial();
        var conversation = Conversation.Create("Test");

        var newState = state.WithConversation(conversation);

        newState.Conversations.Should().HaveCount(1);
        newState.Conversations[0].Should().Be(conversation);
        state.Conversations.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void WithUpdatedConversation_ReplacesExisting()
    {
        var conversation = Conversation.Create("Original");
        var state = AppState.Initial().WithConversation(conversation);
        var updated = conversation.WithTitle("Updated");

        var newState = state.WithUpdatedConversation(updated);

        newState.Conversations.Should().HaveCount(1);
        newState.Conversations[0].Title.Should().Be("Updated");
    }

    [Fact]
    public void WithoutConversation_RemovesFromList()
    {
        var conversation = Conversation.Create("Test");
        var state = AppState.Initial()
            .WithConversation(conversation)
            .WithActiveConversation(conversation.Id);

        var newState = state.WithoutConversation(conversation.Id);

        newState.Conversations.Should().BeEmpty();
        newState.ActiveConversationId.Should().BeNull("should clear active if deleted");
    }

    [Fact]
    public void ActiveConversation_ReturnsCorrectConversation()
    {
        var conversation1 = Conversation.Create("First");
        var conversation2 = Conversation.Create("Second");
        var state = AppState.Initial()
            .WithConversation(conversation1)
            .WithConversation(conversation2)
            .WithActiveConversation(conversation2.Id);

        state.ActiveConversation.Should().NotBeNull();
        state.ActiveConversation!.Title.Should().Be("Second");
    }

    [Fact]
    public void StateTransitions_UpdateLastModified()
    {
        var before = DateTimeOffset.UtcNow;
        var state = AppState.Initial();
        var after = state.WithConversation(Conversation.Create("Test"));

        after.LastModified.Should().BeOnOrAfter(before);
    }
}

public class ModelSelectionStateTests
{
    [Fact]
    public void Default_HasEmptySelection()
    {
        var state = ModelSelectionState.Default();

        state.SelectedModel.Should().BeEmpty();
        state.AvailableModels.Should().BeEmpty();
        state.Backend.Should().Be("Ollama");
    }

    [Fact]
    public void WithModel_SetsSelection()
    {
        var state = ModelSelectionState.WithModel("llama3.2");

        state.SelectedModel.Should().Be("llama3.2");
        state.AvailableModels.Should().Contain("llama3.2");
    }

    [Fact]
    public void SelectModel_ChangesSelection()
    {
        var state = ModelSelectionState.WithModel(
            "llama3.2",
            availableModels: ["llama3.2", "mistral"]);

        var newState = state.SelectModel("mistral");

        newState.SelectedModel.Should().Be("mistral");
        newState.AvailableModels.Should().BeEquivalentTo(state.AvailableModels);
    }

    [Fact]
    public void WithAvailableModels_UpdatesList()
    {
        var state = ModelSelectionState.Default();

        var newState = state.WithAvailableModels(["model1", "model2"]);

        newState.AvailableModels.Should().HaveCount(2);
        newState.LastRefreshed.Should().NotBeNull();
    }
}
