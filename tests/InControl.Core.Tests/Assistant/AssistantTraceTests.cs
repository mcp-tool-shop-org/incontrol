using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class AssistantTraceTests
{
    private readonly AssistantTrace _trace;

    public AssistantTraceTests()
    {
        _trace = new AssistantTrace();
    }

    [Fact]
    public void Reason_AddsReasoningEntry()
    {
        var entry = _trace.Reason("Considering user request", "context data");

        entry.Type.Should().Be(TraceType.Reasoning);
        entry.Content.Should().Be("Considering user request");
        entry.Context.Should().Be("context data");
        _trace.Entries.Should().Contain(entry);
    }

    [Fact]
    public void Decide_AddsDecisionEntry()
    {
        var entry = _trace.Decide("Use approach A", "It's more efficient");

        entry.Type.Should().Be(TraceType.Decision);
        entry.Content.Should().Be("Use approach A");
        entry.Context.Should().Be("It's more efficient");
    }

    [Fact]
    public void Decide_RecordsAlternatives()
    {
        var alternatives = new List<string> { "Approach B", "Approach C" };
        _trace.Decide("Use approach A", "Best fit", alternatives);

        var entry = _trace.Entries.Last();
        entry.Alternatives.Should().BeEquivalentTo(alternatives);
    }

    [Fact]
    public void Act_AddsActionEntry()
    {
        var entry = _trace.Act("Execute command");

        entry.Type.Should().Be(TraceType.Action);
        entry.Content.Should().Be("Execute command");
    }

    [Fact]
    public void Act_RecordsParameters()
    {
        var parameters = new Dictionary<string, object?> { ["path"] = "/test", ["force"] = true };
        _trace.Act("Write file", parameters);

        var entry = _trace.Entries.Last();
        entry.ActionParameters.Should().ContainKey("path");
        entry.ActionParameters!["path"].Should().Be("/test");
    }

    [Fact]
    public void Observe_AddsObservationEntry()
    {
        var entry = _trace.Observe("User clicked button", "UI event");

        entry.Type.Should().Be(TraceType.Observation);
        entry.Content.Should().Be("User clicked button");
        entry.Context.Should().Be("UI event");
    }

    [Fact]
    public void ToolCall_AddsToolCallEntry()
    {
        var parameters = new Dictionary<string, object?> { ["query"] = "test" };
        var entry = _trace.ToolCall("search-tool", parameters, "Found 5 results");

        entry.Type.Should().Be(TraceType.ToolCall);
        entry.ToolId.Should().Be("search-tool");
        entry.ToolResult.Should().Be("Found 5 results");
    }

    [Fact]
    public void Error_AddsErrorEntry()
    {
        var entry = _trace.Error("Network timeout", "Retrying in 5s");

        entry.Type.Should().Be(TraceType.Error);
        entry.Content.Should().Be("Network timeout");
        entry.Context.Should().Be("Retrying in 5s");
    }

    [Fact]
    public void MemoryAccess_AddsMemoryAccessEntry()
    {
        var entry = _trace.MemoryAccess("Read", "user-preference", true);

        entry.Type.Should().Be(TraceType.MemoryAccess);
        entry.Content.Should().Contain("Read");
        entry.Content.Should().Contain("user-preference");
        entry.Content.Should().Contain("found: True");
    }

    [Fact]
    public void StateChange_AddsStateChangeEntry()
    {
        var entry = _trace.StateChange(AssistantState.Idle, AssistantState.Listening, "User input");

        entry.Type.Should().Be(TraceType.StateChange);
        entry.Content.Should().Contain("Idle");
        entry.Content.Should().Contain("Listening");
        entry.Context.Should().Be("User input");
    }

    [Fact]
    public void GetByType_FiltersCorrectly()
    {
        _trace.Reason("Thought 1");
        _trace.Decide("Decision 1", "Rationale");
        _trace.Reason("Thought 2");
        _trace.Act("Action 1");

        var reasonings = _trace.GetByType(TraceType.Reasoning);

        reasonings.Should().HaveCount(2);
        reasonings.All(e => e.Type == TraceType.Reasoning).Should().BeTrue();
    }

    [Fact]
    public void GetByTimeRange_FiltersCorrectly()
    {
        var before = DateTimeOffset.UtcNow;
        _trace.Reason("Entry 1");
        Thread.Sleep(10);
        var middle = DateTimeOffset.UtcNow;
        Thread.Sleep(10);
        _trace.Reason("Entry 2");
        var after = DateTimeOffset.UtcNow;

        var filtered = _trace.GetByTimeRange(middle, after);

        filtered.Should().HaveCount(1);
        filtered[0].Content.Should().Be("Entry 2");
    }

    [Fact]
    public void GetRecent_ReturnsLastNEntries()
    {
        for (int i = 1; i <= 10; i++)
        {
            _trace.Reason($"Entry {i}");
        }

        var recent = _trace.GetRecent(3);

        recent.Should().HaveCount(3);
        recent[0].Content.Should().Be("Entry 8");
        recent[1].Content.Should().Be("Entry 9");
        recent[2].Content.Should().Be("Entry 10");
    }

    [Fact]
    public void ExplainAction_BuildsExplanationChain()
    {
        _trace.Observe("User requested file read");
        _trace.Reason("Need to check file permissions");
        _trace.Decide("Proceed with read", "File is accessible");
        var action = _trace.Act("Read file");

        var explanation = _trace.ExplainAction(action.Id);

        explanation.Chain.Should().HaveCount(4);
        explanation.Chain[0].Type.Should().Be(TraceType.Observation);
        explanation.Chain[3].Type.Should().Be(TraceType.Action);
        explanation.Summary.Should().Contain("Observed:");
        explanation.Summary.Should().Contain("Action:");
    }

    [Fact]
    public void ExplainAction_ReturnsNotFound_ForInvalidId()
    {
        var explanation = _trace.ExplainAction(Guid.NewGuid());

        explanation.Summary.Should().Be("Action not found");
        explanation.Chain.Should().BeEmpty();
    }

    [Fact]
    public void ExplainAction_StopsAtPreviousAction()
    {
        _trace.Observe("First observation");
        _trace.Act("First action");
        _trace.Observe("Second observation");
        _trace.Reason("Second reasoning");
        var secondAction = _trace.Act("Second action");

        var explanation = _trace.ExplainAction(secondAction.Id);

        explanation.Chain.Should().HaveCount(3);
        explanation.Chain.Should().NotContain(e => e.Content == "First observation");
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _trace.Reason("Entry 1");
        _trace.Reason("Entry 2");

        _trace.Clear();

        _trace.Entries.Should().BeEmpty();
    }

    [Fact]
    public void EntryAdded_RaisesEvent()
    {
        TraceEntryEventArgs? capturedArgs = null;
        _trace.EntryAdded += (_, args) => capturedArgs = args;

        var entry = _trace.Reason("Test");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Entry.Id.Should().Be(entry.Id);
    }

    [Fact]
    public void MaxEntries_TrimOldEntries()
    {
        var smallTrace = new AssistantTrace(maxEntries: 5);

        for (int i = 1; i <= 10; i++)
        {
            smallTrace.Reason($"Entry {i}");
        }

        smallTrace.Entries.Should().HaveCount(5);
        smallTrace.Entries[0].Content.Should().Be("Entry 6");
        smallTrace.Entries[4].Content.Should().Be("Entry 10");
    }

    [Fact]
    public void ExportToJson_ProducesValidJson()
    {
        _trace.Reason("Test entry");
        _trace.Act("Test action");

        var json = _trace.ExportToJson();

        json.Should().Contain("\"Type\": \"Reasoning\"");
        json.Should().Contain("\"Type\": \"Action\"");
        json.Should().Contain("Test entry");
    }

    [Fact]
    public void Entries_IsThreadSafe()
    {
        Parallel.For(0, 100, i =>
        {
            _trace.Reason($"Parallel entry {i}");
        });

        _trace.Entries.Count.Should().Be(100);
    }

    [Fact]
    public void Timestamp_IsRecorded()
    {
        var before = DateTimeOffset.UtcNow;
        var entry = _trace.Reason("Test");
        var after = DateTimeOffset.UtcNow;

        entry.Timestamp.Should().BeOnOrAfter(before);
        entry.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Id_IsUnique()
    {
        var entry1 = _trace.Reason("Entry 1");
        var entry2 = _trace.Reason("Entry 2");

        entry1.Id.Should().NotBe(entry2.Id);
    }
}

public class TraceFormatterTests
{
    [Fact]
    public void FormatLine_IncludesIcon()
    {
        var entry = new TraceEntry(
            Id: Guid.NewGuid(),
            Type: TraceType.Reasoning,
            Content: "Test thought",
            Context: null,
            Timestamp: DateTimeOffset.UtcNow,
            Alternatives: null,
            ActionParameters: null,
            ToolId: null,
            ToolResult: null
        );

        var line = TraceFormatter.FormatLine(entry);

        line.Should().Contain("💭");
        line.Should().Contain("Test thought");
    }

    [Fact]
    public void FormatLine_IncludesContext()
    {
        var entry = new TraceEntry(
            Id: Guid.NewGuid(),
            Type: TraceType.Decision,
            Content: "Chose A",
            Context: "Because it's better",
            Timestamp: DateTimeOffset.UtcNow,
            Alternatives: null,
            ActionParameters: null,
            ToolId: null,
            ToolResult: null
        );

        var line = TraceFormatter.FormatLine(entry);

        line.Should().Contain("Because it's better");
    }

    [Theory]
    [InlineData(TraceType.Reasoning, "💭")]
    [InlineData(TraceType.Decision, "🎯")]
    [InlineData(TraceType.Action, "⚡")]
    [InlineData(TraceType.Observation, "👁️")]
    [InlineData(TraceType.ToolCall, "🔧")]
    [InlineData(TraceType.Error, "❌")]
    [InlineData(TraceType.MemoryAccess, "💾")]
    [InlineData(TraceType.StateChange, "🔄")]
    public void FormatLine_UsesCorrectIcon(TraceType type, string expectedIcon)
    {
        var entry = new TraceEntry(
            Id: Guid.NewGuid(),
            Type: type,
            Content: "Test",
            Context: null,
            Timestamp: DateTimeOffset.UtcNow,
            Alternatives: null,
            ActionParameters: null,
            ToolId: null,
            ToolResult: null
        );

        var line = TraceFormatter.FormatLine(entry);

        line.Should().Contain(expectedIcon);
    }

    [Fact]
    public void FormatTimeline_JoinsEntriesWithNewlines()
    {
        var entries = new List<TraceEntry>
        {
            new(Guid.NewGuid(), TraceType.Reasoning, "Entry 1", null, DateTimeOffset.UtcNow, null, null, null, null),
            new(Guid.NewGuid(), TraceType.Action, "Entry 2", null, DateTimeOffset.UtcNow, null, null, null, null)
        };

        var timeline = TraceFormatter.FormatTimeline(entries);

        timeline.Should().Contain("Entry 1");
        timeline.Should().Contain("Entry 2");
        timeline.Should().Contain(Environment.NewLine);
    }

    [Fact]
    public void FormatExplanation_IncludesSummary()
    {
        var chain = new ExplanationChain(
            Chain: new List<TraceEntry>
            {
                new(Guid.NewGuid(), TraceType.Observation, "Saw X", null, DateTimeOffset.UtcNow, null, null, null, null),
                new(Guid.NewGuid(), TraceType.Action, "Did Y", null, DateTimeOffset.UtcNow, null, null, null, null)
            },
            Summary: "Observed X then did Y"
        );

        var formatted = TraceFormatter.FormatExplanation(chain);

        formatted.Should().Contain("=== Explanation ===");
        formatted.Should().Contain("Saw X");
        formatted.Should().Contain("Did Y");
        formatted.Should().Contain("Summary: Observed X then did Y");
    }

    [Fact]
    public void FormatExplanation_IncludesAlternatives()
    {
        var chain = new ExplanationChain(
            Chain: new List<TraceEntry>
            {
                new(Guid.NewGuid(), TraceType.Decision, "Chose A", "Best option", DateTimeOffset.UtcNow,
                    new List<string> { "B", "C" }, null, null, null)
            },
            Summary: "Decided on A"
        );

        var formatted = TraceFormatter.FormatExplanation(chain);

        formatted.Should().Contain("Alternatives considered: B, C");
    }
}

public class TraceTypeTests
{
    [Theory]
    [InlineData(TraceType.Reasoning)]
    [InlineData(TraceType.Decision)]
    [InlineData(TraceType.Action)]
    [InlineData(TraceType.Observation)]
    [InlineData(TraceType.ToolCall)]
    [InlineData(TraceType.Error)]
    [InlineData(TraceType.MemoryAccess)]
    [InlineData(TraceType.StateChange)]
    public void TraceType_AllValuesAreDefined(TraceType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }
}
