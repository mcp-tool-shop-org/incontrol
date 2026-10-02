using System.Text.Json;
using System.Text.Json.Serialization;

namespace InControl.Core.Assistant;

/// <summary>
/// Records the assistant's reasoning process for explainability.
/// Every significant decision should be traceable to its reasoning.
/// </summary>
public sealed class AssistantTrace
{
    private readonly List<TraceEntry> _entries = [];
    private readonly object _lock = new();
    private readonly int _maxEntries;

    /// <summary>
    /// Event raised when a new trace entry is added.
    /// </summary>
    public event EventHandler<TraceEntryEventArgs>? EntryAdded;

    public AssistantTrace(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    /// <summary>
    /// All trace entries in chronological order.
    /// </summary>
    public IReadOnlyList<TraceEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    /// <summary>
    /// Records a reasoning step.
    /// </summary>
    public TraceEntry Reason(string thought, string? context = null)
    {
        return AddEntry(TraceType.Reasoning, thought, context);
    }

    /// <summary>
    /// Records a decision point.
    /// </summary>
    public TraceEntry Decide(string decision, string rationale, IReadOnlyList<string>? alternatives = null)
    {
        var entry = AddEntry(TraceType.Decision, decision, rationale);
        if (alternatives != null && alternatives.Count > 0)
        {
            lock (_lock)
            {
                var index = _entries.IndexOf(entry);
                if (index >= 0)
                {
                    entry = entry with { Alternatives = alternatives };
                    _entries[index] = entry;
                }
            }
        }
        return entry;
    }

    /// <summary>
    /// Records an action taken.
    /// </summary>
    public TraceEntry Act(string action, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var entry = AddEntry(TraceType.Action, action);
        if (parameters != null)
        {
            lock (_lock)
            {
                var index = _entries.IndexOf(entry);
                if (index >= 0)
                {
                    entry = entry with { ActionParameters = parameters };
                    _entries[index] = entry;
                }
            }
        }
        return entry;
    }

    /// <summary>
    /// Records an observation from the environment.
    /// </summary>
    public TraceEntry Observe(string observation, string? source = null)
    {
        return AddEntry(TraceType.Observation, observation, source);
    }

    /// <summary>
    /// Records a tool invocation.
    /// </summary>
    public TraceEntry ToolCall(string toolId, IReadOnlyDictionary<string, object?>? parameters = null, string? result = null)
    {
        var entry = AddEntry(TraceType.ToolCall, $"Invoked tool: {toolId}");
        lock (_lock)
        {
            var index = _entries.IndexOf(entry);
            if (index >= 0)
            {
                entry = entry with
                {
                    ToolId = toolId,
                    ActionParameters = parameters,
                    ToolResult = result
                };
                _entries[index] = entry;
            }
        }
        return entry;
    }

    /// <summary>
    /// Records an error or exception.
    /// </summary>
    public TraceEntry Error(string error, string? recovery = null)
    {
        return AddEntry(TraceType.Error, error, recovery);
    }

    /// <summary>
    /// Records a memory access.
    /// </summary>
    public TraceEntry MemoryAccess(string operation, string key, bool found)
    {
        return AddEntry(TraceType.MemoryAccess, $"{operation}: {key} (found: {found})");
    }

    /// <summary>
    /// Records a state transition.
    /// </summary>
    public TraceEntry StateChange(AssistantState from, AssistantState to, string? trigger = null)
    {
        return AddEntry(TraceType.StateChange, $"State: {from} → {to}", trigger);
    }

    /// <summary>
    /// Gets entries of a specific type.
    /// </summary>
    public IReadOnlyList<TraceEntry> GetByType(TraceType type)
    {
        lock (_lock)
        {
            return _entries.Where(e => e.Type == type).ToList();
        }
    }

    /// <summary>
    /// Gets entries within a time range.
    /// </summary>
    public IReadOnlyList<TraceEntry> GetByTimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        lock (_lock)
        {
            return _entries.Where(e => e.Timestamp >= start && e.Timestamp <= end).ToList();
        }
    }

    /// <summary>
    /// Gets the most recent entries.
    /// </summary>
    public IReadOnlyList<TraceEntry> GetRecent(int count)
    {
        lock (_lock)
        {
            return _entries.TakeLast(count).ToList();
        }
    }

    /// <summary>
    /// Explains why a specific action was taken by tracing back through decisions.
    /// </summary>
    public ExplanationChain ExplainAction(Guid actionId)
    {
        lock (_lock)
        {
            var action = _entries.FirstOrDefault(e => e.Id == actionId && e.Type == TraceType.Action);
            if (action == null)
            {
                return new ExplanationChain([], "Action not found");
            }

            var chain = new List<TraceEntry> { action };
            var actionIndex = _entries.IndexOf(action);

            // Walk backwards to find the reasoning chain
            for (var i = actionIndex - 1; i >= 0 && chain.Count < 10; i--)
            {
                var entry = _entries[i];
                if (entry.Type is TraceType.Reasoning or TraceType.Decision or TraceType.Observation)
                {
                    chain.Insert(0, entry);
                }
                else if (entry.Type == TraceType.Action)
                {
                    // Stop at previous action
                    break;
                }
            }

            return new ExplanationChain(chain, BuildExplanationSummary(chain));
        }
    }

    /// <summary>
    /// Clears all trace entries.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    /// <summary>
    /// Exports trace to JSON format.
    /// </summary>
    public string ExportToJson()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_entries, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }
    }

    private TraceEntry AddEntry(TraceType type, string content, string? context = null)
    {
        var entry = new TraceEntry(
            Id: Guid.NewGuid(),
            Type: type,
            Content: content,
            Context: context,
            Timestamp: DateTimeOffset.UtcNow,
            Alternatives: null,
            ActionParameters: null,
            ToolId: null,
            ToolResult: null
        );

        lock (_lock)
        {
            _entries.Add(entry);

            // Trim old entries if we exceed max
            while (_entries.Count > _maxEntries)
            {
                _entries.RemoveAt(0);
            }
        }

        EntryAdded?.Invoke(this, new TraceEntryEventArgs(entry));

        return entry;
    }

    private static string BuildExplanationSummary(List<TraceEntry> chain)
    {
        if (chain.Count == 0) return "No explanation available";

        var parts = new List<string>();

        foreach (var entry in chain)
        {
            var prefix = entry.Type switch
            {
                TraceType.Observation => "Observed:",
                TraceType.Reasoning => "Considered:",
                TraceType.Decision => "Decided:",
                TraceType.Action => "Action:",
                _ => ""
            };

            if (!string.IsNullOrEmpty(prefix))
            {
                parts.Add($"{prefix} {entry.Content}");
            }
        }

        return string.Join(" → ", parts);
    }
}

/// <summary>
/// A single entry in the reasoning trace.
/// </summary>
public sealed record TraceEntry(
    Guid Id,
    TraceType Type,
    string Content,
    string? Context,
    DateTimeOffset Timestamp,
    IReadOnlyList<string>? Alternatives,
    IReadOnlyDictionary<string, object?>? ActionParameters,
    string? ToolId,
    string? ToolResult
);

/// <summary>
/// Types of trace entries.
/// </summary>
public enum TraceType
{
    /// <summary>Internal reasoning step.</summary>
    Reasoning,

    /// <summary>A decision point with chosen path.</summary>
    Decision,

    /// <summary>An action taken.</summary>
    Action,

    /// <summary>An observation from the environment.</summary>
    Observation,

    /// <summary>A tool invocation.</summary>
    ToolCall,

    /// <summary>An error or exception.</summary>
    Error,

    /// <summary>A memory access (read/write).</summary>
    MemoryAccess,

    /// <summary>A state machine transition.</summary>
    StateChange
}

/// <summary>
/// A chain of trace entries explaining an action.
/// </summary>
public sealed record ExplanationChain(
    IReadOnlyList<TraceEntry> Chain,
    string Summary
);

/// <summary>
/// Event args for trace entries.
/// </summary>
public sealed class TraceEntryEventArgs : EventArgs
{
    public TraceEntry Entry { get; }

    public TraceEntryEventArgs(TraceEntry entry)
    {
        Entry = entry;
    }
}

/// <summary>
/// Formats trace entries for human-readable display.
/// </summary>
public static class TraceFormatter
{
    /// <summary>
    /// Formats a trace entry as a single line.
    /// </summary>
    public static string FormatLine(TraceEntry entry)
    {
        var icon = entry.Type switch
        {
            TraceType.Reasoning => "💭",
            TraceType.Decision => "🎯",
            TraceType.Action => "⚡",
            TraceType.Observation => "👁️",
            TraceType.ToolCall => "🔧",
            TraceType.Error => "❌",
            TraceType.MemoryAccess => "💾",
            TraceType.StateChange => "🔄",
            _ => "•"
        };

        var time = entry.Timestamp.ToString("HH:mm:ss.fff");
        var line = $"[{time}] {icon} {entry.Content}";

        if (!string.IsNullOrEmpty(entry.Context))
        {
            line += $" ({entry.Context})";
        }

        return line;
    }

    /// <summary>
    /// Formats multiple entries as a timeline.
    /// </summary>
    public static string FormatTimeline(IReadOnlyList<TraceEntry> entries)
    {
        return string.Join(Environment.NewLine, entries.Select(FormatLine));
    }

    /// <summary>
    /// Formats an explanation chain as readable text.
    /// </summary>
    public static string FormatExplanation(ExplanationChain chain)
    {
        var lines = new List<string>
        {
            "=== Explanation ===",
            ""
        };

        foreach (var entry in chain.Chain)
        {
            lines.Add(FormatLine(entry));

            if (entry.Alternatives != null && entry.Alternatives.Count > 0)
            {
                lines.Add($"    Alternatives considered: {string.Join(", ", entry.Alternatives)}");
            }
        }

        lines.Add("");
        lines.Add($"Summary: {chain.Summary}");

        return string.Join(Environment.NewLine, lines);
    }
}
