using InControl.Core.Models;
using InControl.Services.Interfaces;

namespace InControl.ViewModels.Sessions;

/// <summary>
/// Removes every note from the session memory store. Chats are not touched.
/// </summary>
public static class SessionMemoryClearer
{
    /// <summary>
    /// Clears the notes for every project and every stored session.
    /// </summary>
    public static async Task ClearAllAsync(
        ISessionMemory memory,
        IProjectLibrary projects,
        IChatService chats,
        CancellationToken ct = default)
    {
        await projects.EnsureAsync(ct);
        var projectList = await projects.AllAsync(ct);
        var conversations = await chats.GetConversationsAsync(ct);

        await ClearAsync(
            memory,
            projectList.Select(project => project.Id),
            conversations.Select(conversation => conversation.Id),
            ct);
    }

    /// <summary>
    /// Forgets every project note and every note that belongs to one of the sessions.
    /// </summary>
    public static async Task ClearAsync(
        ISessionMemory memory,
        IEnumerable<Guid> projectIds,
        IEnumerable<Guid> sessionIds,
        CancellationToken ct = default)
    {
        await memory.EnsureAsync(ct);

        var projectSet = new HashSet<Guid>(projectIds) { ChatProject.GeneralId };
        foreach (var projectId in projectSet)
        {
            var notes = await memory.ListAsync(projectId, null, ct);
            foreach (var note in notes)
                await memory.ForgetAsync(note.Id, ct);
        }

        foreach (var sessionId in new HashSet<Guid>(sessionIds))
            await memory.ForgetSessionAsync(sessionId, ct);
    }
}
