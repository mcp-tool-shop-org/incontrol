using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Core.Assistant;
using InControl.Core.Models;
using InControl.Services.Storage;
using Xunit;

namespace InControl.Services.Tests.Storage;

public class ProjectMemoryStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FileStore _files;

    public ProjectMemoryStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"InControlTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _files = new FileStore(new Mock<ILogger<FileStore>>().Object, _root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Projects_RoundTrip_AndAlwaysIncludeGeneral()
    {
        var first = new JsonProjectLibrary(_files, new Mock<ILogger<JsonProjectLibrary>>().Object);
        var created = await first.CreateAsync("Harbour");
        await first.UpdateInstructionsAsync(created.Id, "Speak plainly.");
        await first.RenameAsync(created.Id, "Harbour Gate");

        var second = new JsonProjectLibrary(_files, new Mock<ILogger<JsonProjectLibrary>>().Object);
        var all = await second.AllAsync();

        all.Should().Contain(project => project.Id == ChatProject.GeneralId);
        var harbour = all.Should().ContainSingle(project => project.Id == created.Id).Subject;
        harbour.Name.Should().Be("Harbour Gate");
        harbour.Instructions.Should().Be("Speak plainly.");
    }

    [Fact]
    public async Task CorruptProjectsFile_IsNotReplacedWithGeneral()
    {
        var path = Path.Combine(_root, "projects.json");
        await File.WriteAllTextAsync(path, "{");
        var library = new JsonProjectLibrary(_files, new Mock<ILogger<JsonProjectLibrary>>().Object);

        var act = async () => await library.AllAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await File.ReadAllTextAsync(path)).Should().Be("{");
    }

    [Fact]
    public async Task CorruptNotesFile_IsNotReplaced()
    {
        var path = Path.Combine(_root, "memories.json");
        await File.WriteAllTextAsync(path, "{");
        var memory = new JsonSessionMemory(_files, new Mock<ILogger<JsonSessionMemory>>().Object);
        var act = async () => await memory.RememberAsync(
            Note("kept", "Still on disk", ChatProject.GeneralId, sessionId: null));

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await File.ReadAllTextAsync(path)).Should().Be("{");
    }

    [Fact]
    public async Task Notes_RoundTrip_ForgetSessionLeavesProjectNotes()
    {
        var projectId = ChatProject.GeneralId;
        var sessionId = Guid.NewGuid();
        var otherSession = Guid.NewGuid();
        var memory = new JsonSessionMemory(_files, new Mock<ILogger<JsonSessionMemory>>().Object);

        await memory.RememberAsync(Note("shared", "Project wide", projectId, sessionId: null));
        var secret = await memory.RememberAsync(Note("secret", "Sibling secret", projectId, otherSession));
        await memory.RememberAsync(Note("local", "This session only", projectId, sessionId));

        var reloaded = new JsonSessionMemory(_files, new Mock<ILogger<JsonSessionMemory>>().Object);
        var visible = await reloaded.ListAsync(projectId, sessionId);
        visible.Select(note => note.Key).Should().BeEquivalentTo("shared", "local");

        await reloaded.ForgetSessionAsync(otherSession);
        var after = await reloaded.ListAsync(projectId, sessionId);
        after.Select(note => note.Key).Should().BeEquivalentTo("shared", "local");
        (await reloaded.ListAsync(projectId, otherSession)).Select(note => note.Key).Should().NotContain(secret.Key);

        var block = await reloaded.BuildRecallAsync(projectId, sessionId, "Be brief.", "session");
        block.Should().Contain("Be brief.");
        block.Should().Contain("This session only");
        block.Should().NotContain("Sibling secret");
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
