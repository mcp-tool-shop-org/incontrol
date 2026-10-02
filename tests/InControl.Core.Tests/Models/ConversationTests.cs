using FluentAssertions;
using InControl.Core.Models;
using Xunit;

namespace InControl.Core.Tests.Models;

public class ConversationTests
{
    [Fact]
    public void Create_ReturnsNewConversation_WithDefaults()
    {
        // Arrange & Act
        var conversation = Conversation.Create();

        // Assert
        conversation.Id.Should().NotBeEmpty();
        conversation.Title.Should().Be("New Conversation");
        conversation.Messages.Should().BeEmpty();
        conversation.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        conversation.ModifiedAt.Should().Be(conversation.CreatedAt);
    }

    [Fact]
    public void Create_WithParameters_SetsValues()
    {
        // Arrange & Act
        var conversation = Conversation.Create(
            title: "Test Chat",
            model: "llama3.2",
            systemPrompt: "Be helpful");

        // Assert
        conversation.Title.Should().Be("Test Chat");
        conversation.Model.Should().Be("llama3.2");
        conversation.SystemPrompt.Should().Be("Be helpful");
    }

    [Fact]
    public void WithMessage_ReturnsNewConversation_WithMessageAppended()
    {
        // Arrange
        var conversation = Conversation.Create();
        var message = Message.User("Hello!");

        // Act
        var updated = conversation.WithMessage(message);

        // Assert
        updated.Should().NotBeSameAs(conversation);
        updated.Messages.Should().HaveCount(1);
        updated.Messages[0].Should().Be(message);
        updated.ModifiedAt.Should().BeOnOrAfter(conversation.ModifiedAt);
    }

    [Fact]
    public void WithMessage_PreservesExistingMessages()
    {
        // Arrange
        var conversation = Conversation.Create();
        var message1 = Message.User("Hello!");
        var message2 = Message.Assistant("Hi there!");

        // Act
        var updated = conversation
            .WithMessage(message1)
            .WithMessage(message2);

        // Assert
        updated.Messages.Should().HaveCount(2);
        updated.Messages[0].Should().Be(message1);
        updated.Messages[1].Should().Be(message2);
    }

    [Fact]
    public void WithTitle_ReturnsNewConversation_WithUpdatedTitle()
    {
        // Arrange
        var conversation = Conversation.Create(title: "Original");

        // Act
        var updated = conversation.WithTitle("Updated Title");

        // Assert
        updated.Should().NotBeSameAs(conversation);
        updated.Title.Should().Be("Updated Title");
        updated.ModifiedAt.Should().BeOnOrAfter(conversation.ModifiedAt);
    }
}
