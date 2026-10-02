using FluentAssertions;
using InControl.Core.Models;
using InControl.Core.State;
using Xunit;

namespace InControl.Core.Tests.Models;

public class ConversationProjectTests
{
    [Fact]
    public void Create_WithoutProject_LeavesProjectEmpty()
    {
        Conversation.Create("Old").ProjectId.Should().BeNull();
    }

    [Fact]
    public void Create_WithProject_FilesTheSession()
    {
        var id = ChatProject.GeneralId;
        Conversation.Create("Filed", projectId: id).ProjectId.Should().Be(id);
    }

    [Fact]
    public void MissingProjectId_StillDeserializes()
    {
        const string json = """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "title": "Old",
              "createdAt": "2026-01-01T00:00:00+00:00",
              "modifiedAt": "2026-01-01T00:00:00+00:00",
              "messages": []
            }
            """;

        var result = StateSerializer.Deserialize<Conversation>(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Old");
        result.Value.ProjectId.Should().BeNull();
    }
}
