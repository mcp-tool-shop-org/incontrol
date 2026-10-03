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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectId_SurvivesSerializeAndDeserialize(bool compact)
    {
        var projectId = Guid.NewGuid();
        var original = Conversation.Create("Filed", model: "llama3.2", projectId: projectId)
            .WithMessage(Message.User("hello"));

        var json = StateSerializer.Serialize(original, compact);
        var loaded = StateSerializer.Deserialize<Conversation>(json);

        json.Should().Contain("\"projectId\"").And.Contain(projectId.ToString());
        loaded.IsSuccess.Should().BeTrue();
        loaded.Value!.ProjectId.Should().Be(projectId);
        loaded.Value.Id.Should().Be(original.Id);
        loaded.Value.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void ProjectId_SurvivesUtf8BytesRoundTrip()
    {
        var projectId = Guid.NewGuid();
        var original = Conversation.Create("Filed", projectId: projectId);

        var bytes = StateSerializer.SerializeToBytes(original);
        var loaded = StateSerializer.Deserialize<Conversation>(bytes);

        loaded.IsSuccess.Should().BeTrue();
        loaded.Value!.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task ProjectId_SurvivesSaveToFileAndLoad()
    {
        var projectId = Guid.NewGuid();
        var original = Conversation.Create("Filed", projectId: projectId);
        var path = Path.Combine(Path.GetTempPath(), $"conversation-project-{Guid.NewGuid()}.json");
        try
        {
            await using (var write = File.Create(path))
            {
                await StateSerializer.SerializeAsync(write, original);
            }

            await using var read = File.OpenRead(path);
            var loaded = await StateSerializer.DeserializeAsync<Conversation>(read);

            loaded.IsSuccess.Should().BeTrue();
            loaded.Value!.ProjectId.Should().Be(projectId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NullProjectId_RoundTripsAsNull()
    {
        var json = StateSerializer.Serialize(Conversation.Create("Old"));

        json.Should().NotContain("projectId");
        StateSerializer.Deserialize<Conversation>(json).Value!.ProjectId.Should().BeNull();
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
