using System.Text;

namespace InControl.Core.Assistant;

/// <summary>
/// Picks a few notes for the next prompt. Project notes are shared inside one project.
/// Session notes stay in that session. Other projects, and other sessions' notes, stay out.
/// </summary>
public static class MemoryRecall
{
    /// <summary>
    /// How many notes a single prompt may include. Standing instructions are separate.
    /// </summary>
    public const int Cap = 5;

    /// <summary>
    /// Selects notes eligible for this project and session, ranked by keyword overlap,
    /// then by most recently touched. A query that matches nothing still returns the
    /// most recently touched eligible notes, up to <paramref name="cap"/>.
    /// </summary>
    public static IReadOnlyList<AssistantMemoryItem> Select(
        IEnumerable<AssistantMemoryItem> notes,
        Guid projectId,
        Guid? sessionId,
        string? query,
        int cap = Cap)
    {
        if (cap < 1)
            return [];

        var tokens = Tokenize(query);
        return notes
            .Where(note => IsEligible(note, projectId, sessionId))
            .Select(note => (Note: note, Score: Score(note, tokens)))
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Note.LastAccessedAt)
            .Take(cap)
            .Select(pair => pair.Note)
            .ToList();
    }

    /// <summary>
    /// Renders standing instructions and the already-capped notes. Empty sections are omitted.
    /// </summary>
    public static string Format(string? instructions, IReadOnlyList<AssistantMemoryItem> notes)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            parts.Add("Project instructions:\n" + instructions.Trim());
        }

        if (notes.Count > 0)
        {
            var lines = notes.Select(note =>
            {
                var tag = note.SessionId is null ? "[project]" : "[session]";
                return $"{tag} {OneLine(note.Key)}: {OneLine(note.Value)}";
            });
            parts.Add("Recalled notes:\n" + string.Join("\n", lines));
        }

        return string.Join("\n\n", parts);
    }

    private static bool IsEligible(AssistantMemoryItem note, Guid projectId, Guid? sessionId)
    {
        if (note.ProjectId != projectId)
            return false;

        if (note.SessionId is null)
            return true;

        return sessionId is not null && note.SessionId == sessionId;
    }

    private static int Score(AssistantMemoryItem note, HashSet<string> queryTokens)
    {
        if (queryTokens.Count == 0)
            return 0;

        var haystack = Tokenize(note.Key + " " + note.Value);
        var score = 0;
        foreach (var token in queryTokens)
        {
            if (haystack.Contains(token))
                score++;
        }

        return score;
    }

    private static HashSet<string> Tokenize(string? text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return set;

        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
                continue;
            }

            AddToken(set, current);
        }

        AddToken(set, current);
        return set;
    }

    private static void AddToken(HashSet<string> set, StringBuilder current)
    {
        if (current.Length >= 3)
            set.Add(current.ToString());

        current.Clear();
    }

    private static string OneLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= 500 ? flat : flat[..500];
    }
}
