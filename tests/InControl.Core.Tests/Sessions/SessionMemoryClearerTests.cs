using FluentAssertions;
using InControl.Core.Assistant;
using InControl.Core.Models;
using InControl.Services.Interfaces;
using InControl.ViewModels.Sessions;
using Xunit;

namespace InControl.Core.Tests.Sessions;

public class SessionMemoryClearerTests
{
    [Fact]
    public async Task ClearAsync_RemovesProjectNotesAndSessionNotes()
    {
        var other = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var memory = new FakeMemory(
            Note(ChatProject.GeneralId, null),
            Note(other, null),
            Note(ChatProject.GeneralId, sessionA),
            Note(other, sessionB));

        await SessionMemoryClearer.ClearAsync(memory, [other], [sessionA, sessionB]);

        memory.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_RemovesASessionNoteFiledUnderAnotherProject()
    {
        var session = Guid.NewGuid();
        var memory = new FakeMemory(Note(Guid.NewGuid(), session));

        await SessionMemoryClearer.ClearAsync(memory, [], [session]);

        memory.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_WithNothingStored_DoesNotThrow()
    {
        var memory = new FakeMemory();

        await SessionMemoryClearer.ClearAsync(memory, [], []);

        memory.Items.Should().BeEmpty();
    }

    private static AssistantMemoryItem Note(Guid projectId, Guid? sessionId) =>
        AssistantMemoryItem.Create(
            MemoryType.Fact,
            sessionId is null ? MemoryScope.User : MemoryScope.Session,
            MemorySource.ExplicitUser,
            "key",
            "value",
            projectId: projectId,
            sessionId: sessionId);

    private sealed class FakeMemory : ISessionMemory
    {
        public FakeMemory(params AssistantMemoryItem[] items) => Items = [.. items];

        public List<AssistantMemoryItem> Items { get; }

        public Task EnsureAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<AssistantMemoryItem> RememberAsync(AssistantMemoryItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task ForgetAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        public Task ForgetSessionAsync(Guid sessionId, CancellationToken ct = default)
        {
            Items.RemoveAll(item => item.SessionId == sessionId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AssistantMemoryItem>> ListAsync(
            Guid projectId,
            Guid? sessionId,
            CancellationToken ct = default)
        {
            IReadOnlyList<AssistantMemoryItem> list = Items
                .Where(item => item.ProjectId == projectId
                    && (item.SessionId is null || item.SessionId == sessionId))
                .ToList();
            return Task.FromResult(list);
        }

        public Task<string> BuildRecallAsync(
            Guid projectId,
            Guid? sessionId,
            string? instructions,
            string? query,
            CancellationToken ct = default) => Task.FromResult(string.Empty);
    }
}
