using FluentAssertions;
using InControl.Core.Exceptions;
using InControl.Core.Models;
using Xunit;

namespace InControl.Core.Tests.Models;

public class ChatRequestTests
{
    [Fact]
    public void Simple_HasOneUserMessageAndNoSamplingOverrides()
    {
        var request = ChatRequest.Simple("llama3.2", "hello");

        request.Model.Should().Be("llama3.2");
        request.Messages.Should().ContainSingle();
        request.Messages[0].Role.Should().Be(MessageRole.User);
        request.Messages[0].Content.Should().Be("hello");
        request.SystemPrompt.Should().BeNull();
        request.Temperature.Should().BeNull();
        request.MaxTokens.Should().BeNull();
        request.TopP.Should().BeNull();
        request.StopSequences.Should().BeNull();
    }

    [Fact]
    public void FromConversation_UsesTheConversationsModelMessagesAndSystemPrompt()
    {
        var conversation = Conversation.Create(model: "mistral", systemPrompt: "Be brief.")
            .WithMessage(Message.User("hi"))
            .WithMessage(Message.Assistant("hello", "mistral"));

        var request = ChatRequest.FromConversation(conversation);

        request.Model.Should().Be("mistral");
        request.SystemPrompt.Should().Be("Be brief.");
        request.Messages.Select(m => m.Content).Should().Equal("hi", "hello");
    }

    [Fact]
    public void FromConversation_ModelOverrideWins()
    {
        var conversation = Conversation.Create(model: "mistral");

        ChatRequest.FromConversation(conversation, "codegemma").Model.Should().Be("codegemma");
    }

    [Fact]
    public void FromConversation_WithNoModelAnywhere_Throws()
    {
        var act = () => ChatRequest.FromConversation(Conversation.Create());

        act.Should().Throw<ArgumentException>().WithMessage("No model specified*");
    }
}

public class ChatResponseTests
{
    private static ChatResponse Response(int? prompt = null, int? completion = null, TimeSpan? duration = null) => new()
    {
        Content = "text",
        Model = "llama3.2",
        CompletedAt = DateTimeOffset.UtcNow,
        PromptTokens = prompt,
        CompletionTokens = completion,
        Duration = duration
    };

    [Fact]
    public void TotalTokens_IsTheSum_OnlyWhenBothCountsAreKnown()
    {
        Response(prompt: 10, completion: 5).TotalTokens.Should().Be(15);
        Response(prompt: 10).TotalTokens.Should().BeNull();
        Response(completion: 5).TotalTokens.Should().BeNull();
        Response().TotalTokens.Should().BeNull();
    }

    [Fact]
    public void TokensPerSecond_NeedsCompletionTokensAndAPositiveDuration()
    {
        Response(completion: 50, duration: TimeSpan.FromSeconds(2)).TokensPerSecond.Should().Be(25);
        Response(completion: 50, duration: TimeSpan.Zero).TokensPerSecond.Should().BeNull();
        Response(completion: 50).TokensPerSecond.Should().BeNull();
        Response(duration: TimeSpan.FromSeconds(2)).TokensPerSecond.Should().BeNull();
    }
}

public class ConversationEditingTests
{
    [Fact]
    public void WithoutMessage_RemovesOnlyThatMessage_AndTouchesModifiedAt()
    {
        var first = Message.User("one");
        var second = Message.User("two");
        var conversation = Conversation.Create().WithMessage(first).WithMessage(second);
        var before = conversation.ModifiedAt;

        var trimmed = conversation.WithoutMessage(first.Id);

        trimmed.Messages.Should().ContainSingle().Which.Id.Should().Be(second.Id);
        trimmed.ModifiedAt.Should().BeOnOrAfter(before);
        conversation.Messages.Should().HaveCount(2, "the original is immutable");
    }

    [Fact]
    public void WithoutMessage_UnknownId_ChangesNothingButTheTimestamp()
    {
        var conversation = Conversation.Create().WithMessage(Message.User("one"));

        conversation.WithoutMessage(Guid.NewGuid()).Messages.Should().HaveCount(1);
    }

    [Fact]
    public void WithProject_FilesTheSession_WithoutChangingTheRest()
    {
        var conversation = Conversation.Create("Title", model: "m");
        var project = Guid.NewGuid();

        var filed = conversation.WithProject(project);

        filed.ProjectId.Should().Be(project);
        filed.Title.Should().Be("Title");
        filed.Model.Should().Be("m");
        filed.Id.Should().Be(conversation.Id);
        conversation.ProjectId.Should().BeNull();
    }
}

public class ExceptionTypesTests
{
    [Fact]
    public void BaseException_CarriesAMessageAnOptionalCodeAndAnInnerException()
    {
        var inner = new IOException("inner");

        new InControlException().ErrorCode.Should().BeNull();
        new InControlException("plain").Message.Should().Be("plain");
        new InControlException("plain").ErrorCode.Should().BeNull();

        var coded = new InControlException("coded", "E_CODE");
        coded.ErrorCode.Should().Be("E_CODE");
        coded.Message.Should().Be("coded");

        var wrapped = new InControlException("wrapped", inner);
        wrapped.InnerException.Should().BeSameAs(inner);
        wrapped.ErrorCode.Should().BeNull();

        var both = new InControlException("both", "E_BOTH", inner);
        both.ErrorCode.Should().Be("E_BOTH");
        both.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void ConnectionException_RecordsTheEndpointAndBackend()
    {
        var inner = new IOException("refused");

        var simple = new ConnectionException("cannot connect");
        simple.ErrorCode.Should().Be("CONNECTION_ERROR");
        simple.Endpoint.Should().BeNull();
        simple.Backend.Should().BeNull();

        var detailed = new ConnectionException("cannot connect", "http://127.0.0.1:11434", "Ollama");
        detailed.Endpoint.Should().Be("http://127.0.0.1:11434");
        detailed.Backend.Should().Be("Ollama");

        var wrapped = new ConnectionException("cannot connect", inner);
        wrapped.InnerException.Should().BeSameAs(inner);
        wrapped.ErrorCode.Should().Be("CONNECTION_ERROR");

        var full = new ConnectionException("cannot connect", "http://x", "Ollama", inner);
        full.Endpoint.Should().Be("http://x");
        full.Backend.Should().Be("Ollama");
        full.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void InferenceException_RecordsTheModelAndBackend()
    {
        var inner = new InvalidOperationException("bad");

        var simple = new InferenceException("failed");
        simple.ErrorCode.Should().Be("INFERENCE_ERROR");
        simple.Model.Should().BeNull();

        var detailed = new InferenceException("failed", "llama3.2", "Ollama");
        detailed.Model.Should().Be("llama3.2");
        detailed.Backend.Should().Be("Ollama");

        new InferenceException("failed", inner).InnerException.Should().BeSameAs(inner);

        var full = new InferenceException("failed", "llama3.2", "Ollama", inner);
        full.Model.Should().Be("llama3.2");
        full.InnerException.Should().BeSameAs(inner);
        full.Should().BeAssignableTo<InControlException>();
    }

    [Fact]
    public void ModelNotFoundException_NamesTheModel()
    {
        var inner = new KeyNotFoundException();

        var plain = new ModelNotFoundException("ghost:7b");
        plain.Model.Should().Be("ghost:7b");
        plain.Message.Should().Be("Model 'ghost:7b' was not found.");
        plain.ErrorCode.Should().Be("MODEL_NOT_FOUND");

        var wrapped = new ModelNotFoundException("ghost:7b", inner);
        wrapped.InnerException.Should().BeSameAs(inner);
        wrapped.Model.Should().Be("ghost:7b");
    }
}
