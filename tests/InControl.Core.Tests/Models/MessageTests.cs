using FluentAssertions;
using InControl.Core.Models;
using Xunit;

namespace InControl.Core.Tests.Models;

public class MessageTests
{
    [Fact]
    public void User_CreatesUserMessage_WithCorrectRole()
    {
        // Arrange & Act
        var message = Message.User("Hello, world!");

        // Assert
        message.Role.Should().Be(MessageRole.User);
        message.Content.Should().Be("Hello, world!");
        message.Id.Should().NotBeEmpty();
        message.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        message.Model.Should().BeNull();
    }

    [Fact]
    public void Assistant_CreatesAssistantMessage_WithCorrectRole()
    {
        // Arrange & Act
        var message = Message.Assistant("Hello! How can I help?", "llama3.2");

        // Assert
        message.Role.Should().Be(MessageRole.Assistant);
        message.Content.Should().Be("Hello! How can I help?");
        message.Model.Should().Be("llama3.2");
    }

    [Fact]
    public void System_CreatesSystemMessage_WithCorrectRole()
    {
        // Arrange & Act
        var message = Message.System("You are a helpful assistant.");

        // Assert
        message.Role.Should().Be(MessageRole.System);
        message.Content.Should().Be("You are a helpful assistant.");
    }

    [Fact]
    public void Messages_HaveUniqueIds()
    {
        // Arrange & Act
        var message1 = Message.User("First");
        var message2 = Message.User("Second");

        // Assert
        message1.Id.Should().NotBe(message2.Id);
    }
}
