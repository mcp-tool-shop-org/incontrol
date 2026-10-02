using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class MemoryRecallTests
{
    private static readonly Guid Project = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherProject = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Session = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Sibling = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void Select_IncludesProjectNote_AndThisSessionNote()
    {
        var notes = new[]
        {
            Note("dock", "The dock closes at dusk", Project, sessionId: null),
            Note("tone", "Keep this session brief", Project, Session)
        };

        var selected = MemoryRecall.Select(notes, Project, Session, "When does the dock close?");

        selected.Select(note => note.Key).Should().BeEquivalentTo("dock", "tone");
    }

    [Fact]
    public void Select_ExcludesSiblingSession_AndOtherProject()
    {
        var notes = new[]
        {
            Note("shared", "Project wide fact", Project, sessionId: null),
            Note("secret", "Sibling secret", Project, Sibling),
            Note("foreign", "Other project fact about the dock", OtherProject, sessionId: null)
        };

        var selected = MemoryRecall.Select(notes, Project, Session, "dock");

        selected.Should().ContainSingle();
        selected[0].Key.Should().Be("shared");
    }

    [Fact]
    public void Select_CapsAtFive_AndRanksKeywordAboveUnrelated()
    {
        var notes = new List<AssistantMemoryItem>();
        for (var i = 0; i < 5; i++)
        {
            notes.Add(Note("plain" + i, "Unrelated grocery list", Project, sessionId: null) with
            {
                LastAccessedAt = DateTimeOffset.UnixEpoch.AddMinutes(i)
            });
        }

        notes.Add(Note("light", "The lighthouse lamp was repaired", Project, sessionId: null) with
        {
            LastAccessedAt = DateTimeOffset.UnixEpoch
        });

        var selected = MemoryRecall.Select(notes, Project, Session, "lighthouse");

        selected.Should().HaveCount(MemoryRecall.Cap);
        selected[0].Key.Should().Be("light");
    }

    [Fact]
    public void Select_WhenNothingMatches_ReturnsNewestEligibleNotes()
    {
        var notes = Enumerable.Range(0, 6).Select(i =>
            Note("note" + i, "No overlap here", Project, sessionId: null) with
            {
                LastAccessedAt = DateTimeOffset.UnixEpoch.AddHours(i)
            }).ToList();

        var selected = MemoryRecall.Select(notes, Project, Session, query: null);

        selected.Should().HaveCount(5);
        selected.Select(note => note.Key).Should().BeEquivalentTo("note5", "note4", "note3", "note2", "note1");
        selected[0].Key.Should().Be("note5");
    }

    [Fact]
    public void Select_SessionNoteRequiresMatchingSession()
    {
        var note = Note("local", "Only for one chat", Project, Session);

        MemoryRecall.Select([note], Project, sessionId: null, "chat").Should().BeEmpty();
        MemoryRecall.Select([note], Project, Session, "chat").Should().ContainSingle();
    }

    [Fact]
    public void Format_IncludesInstructionsSeparately_AndOmitsEmptySections()
    {
        var notes = new[] { Note("dock", "Closes at dusk", Project, sessionId: null) };

        var formatted = MemoryRecall.Format("Speak plainly.", notes);

        formatted.Should().Contain("Project instructions:");
        formatted.Should().Contain("Speak plainly.");
        formatted.Should().Contain("[project] dock: Closes at dusk");
        MemoryRecall.Format(null, []).Should().BeEmpty();
        MemoryRecall.Format("   ", []).Should().BeEmpty();
    }

    [Fact]
    public void Format_TagsSessionNotes()
    {
        var note = Note("tone", "Keep it brief", Project, Session);

        MemoryRecall.Format(null, [note]).Should().Contain("[session] tone: Keep it brief");
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
