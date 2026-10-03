using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using InControl.Core.Models;
using InControl.Inference.Interfaces;
using InControl.Services.Interfaces;

namespace InControl.Services.Chat;

/// <summary>
/// Chat service that orchestrates conversations using the inference client.
/// Manages conversation lifecycle, delegates inference to IInferenceClient,
/// and persists conversations to disk via IConversationStorage.
/// </summary>
public sealed class ChatService : IChatService
{
    private readonly IInferenceClient _inferenceClient;
    private readonly IConversationStorage _storage;
    private readonly ILogger<ChatService> _logger;
    private readonly IProjectLibrary? _projects;
    private readonly ISessionMemory? _memory;
    private readonly Dictionary<Guid, Conversation> _conversations = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _activeGenerations = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    // Saves and deletes take turns, so a delete cannot land in the middle of a save.
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _generationGate = new();
    private bool _loaded;

    public event EventHandler<ConversationEventArgs>? ConversationCreated;
    public event EventHandler<ConversationEventArgs>? ConversationUpdated;
    public event EventHandler<ConversationEventArgs>? ConversationDeleted;
    public event EventHandler<ConversationPersistenceFailedEventArgs>? PersistenceFailed;

    public ChatService(
        IInferenceClient inferenceClient,
        IConversationStorage storage,
        ILogger<ChatService> logger,
        IProjectLibrary? projects = null,
        ISessionMemory? memory = null)
    {
        _inferenceClient = inferenceClient;
        _storage = storage;
        _logger = logger;
        _projects = projects;
        _memory = memory;
    }

    /// <summary>
    /// Loads all persisted conversations into memory.
    /// Called once on first access.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded) return;

        await _loadGate.WaitAsync(ct);
        try
        {
            if (_loaded) return;

            try
            {
                var conversations = await _storage.LoadAllAsync(ct);
                foreach (var c in conversations)
                {
                    // Older files have no project. Show them in General without bumping ModifiedAt.
                    var stored = c.ProjectId is null
                        ? c with { ProjectId = ChatProject.GeneralId }
                        : c;
                    _conversations[stored.Id] = stored;
                    if (c.ProjectId is null)
                        await SaveQuietly(stored, ct);
                }
                _logger.LogInformation("Loaded {Count} conversations from storage", conversations.Count);
            }
            catch (OperationCanceledException)
            {
                // A cancelled load is not a finished one. The next call tries again.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load conversations from storage");
            }

            _loaded = true;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public async Task<Conversation> CreateConversationAsync(
        string? title = null,
        string? model = null,
        string? systemPrompt = null,
        Guid? projectId = null,
        CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        var conversation = Conversation.Create(title, model, systemPrompt, projectId ?? ChatProject.GeneralId);
        _conversations[conversation.Id] = conversation;

        // Persist immediately
        await SaveQuietly(conversation, ct);

        _logger.LogInformation("Created conversation {Id} with model {Model}", conversation.Id, model);
        ConversationCreated?.Invoke(this, new ConversationEventArgs { Conversation = conversation });

        return conversation;
    }

    public async Task<Conversation?> DuplicateConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        if (!_conversations.TryGetValue(conversationId, out var source))
            return null;

        var copy = Conversation.Create(
            source.Title + " (copy)",
            source.Model,
            source.SystemPrompt,
            source.ProjectId ?? ChatProject.GeneralId);
        foreach (var message in source.Messages)
            copy = copy.WithMessage(message);

        _conversations[copy.Id] = copy;

        // A copy that is not on disk would vanish on restart. Do not show one.
        if (!await SaveQuietly(copy, ct))
        {
            _conversations.Remove(copy.Id);
            return null;
        }

        ConversationCreated?.Invoke(this, new ConversationEventArgs { Conversation = copy });
        return copy;
    }

    public async Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _conversations.TryGetValue(conversationId, out var conversation);
        return conversation;
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        var list = _conversations.Values
            .OrderByDescending(c => c.ModifiedAt)
            .ToList();
        return list;
    }

    public async Task<Conversation> UpdateConversationAsync(
        Guid conversationId,
        string? title = null,
        string? model = null,
        CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            throw new KeyNotFoundException($"Conversation {conversationId} not found");
        }

        if (title is not null)
        {
            conversation = conversation.WithTitle(title);
        }

        if (model is not null)
        {
            conversation = conversation with { Model = model, ModifiedAt = DateTimeOffset.UtcNow };
        }

        _conversations[conversationId] = conversation;

        // Persist
        await SaveQuietly(conversation, ct);

        ConversationUpdated?.Invoke(this, new ConversationEventArgs { Conversation = conversation });

        return conversation;
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        if (!_conversations.TryGetValue(conversationId, out var conversation))
            return true;

        bool deleted;
        await _writeGate.WaitAsync(ct);
        try
        {
            // A save that held the gate has finished. Delete after it so the file does not come back.
            if (!_conversations.ContainsKey(conversationId))
                return true;

            // False also means there was no file. Only a file that is still there is a failure.
            deleted = await _storage.DeleteAsync(conversationId, ct)
                || !await _storage.ExistsAsync(conversationId, ct);
            if (deleted)
                _conversations.Remove(conversationId);
        }
        finally
        {
            _writeGate.Release();
        }

        if (!deleted)
        {
            _logger.LogWarning("Could not delete the file for conversation {Id}; keeping the session", conversationId);
            PersistenceFailed?.Invoke(this, new ConversationPersistenceFailedEventArgs
            {
                ConversationId = conversationId,
                Operation = ConversationPersistenceOperation.Delete
            });
            return false;
        }

        await ForgetSessionNotesAsync(conversationId, ct);

        ConversationDeleted?.Invoke(this, new ConversationEventArgs { Conversation = conversation });
        return true;
    }

    public async IAsyncEnumerable<string> SendMessageAsync(
        Guid conversationId,
        string message,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            throw new KeyNotFoundException($"Conversation {conversationId} not found");
        }

        var model = conversation.Model
            ?? throw new InvalidOperationException("No model selected for this conversation");

        // Keep the prompt only after the model is known, and write it before the stream.
        // A missing model must not leave an unanswered line in the open session.
        var userMessage = Message.User(message);
        conversation = conversation.WithMessage(userMessage);
        _conversations[conversationId] = conversation;
        await SaveQuietly(conversation, ct);

        // Build the chat request from this session's transcript, plus a few recalled notes.
        var request = await WithRecallAsync(conversation, ChatRequest.FromConversation(conversation), ct);

        _logger.LogDebug("Sending message to {Model}, conversation {Id}", model, conversationId);

        // Track active generation for cancellation
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (_generationGate) _activeGenerations[conversationId] = cts;

        var responseContent = new System.Text.StringBuilder();

        try
        {
            await foreach (var token in _inferenceClient.StreamChatAsync(request, cts.Token))
            {
                responseContent.Append(token);
                yield return token;
            }

            // A delete during the reply wins. Do not write the session back.
            // SaveQuietly checks again under the write gate, so a delete during the save wins too.
            if (_conversations.ContainsKey(conversationId))
            {
                var assistantMessage = Message.Assistant(responseContent.ToString(), model);
                conversation = conversation.WithMessage(assistantMessage);
                _conversations[conversationId] = conversation;
                await SaveQuietly(conversation);
                ConversationUpdated?.Invoke(this, new ConversationEventArgs { Conversation = conversation });
            }
        }
        finally
        {
            lock (_generationGate) _activeGenerations.Remove(conversationId);
            cts.Dispose();
        }
    }

    public async IAsyncEnumerable<string> RegenerateLastResponseAsync(
        Guid conversationId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            throw new KeyNotFoundException($"Conversation {conversationId} not found");
        }

        // Remove the last assistant message if present
        var messages = conversation.Messages.ToList();
        if (messages.Count > 0 && messages[^1].Role == MessageRole.Assistant)
        {
            messages.RemoveAt(messages.Count - 1);
        }

        // Get the last user message to regenerate from
        var lastUserMessage = messages.LastOrDefault(m => m.Role == MessageRole.User);
        if (lastUserMessage is null)
        {
            yield break;
        }

        // Build the request without the last assistant message, but leave the
        // stored transcript alone until the new answer is saved. A cancel or a
        // failed stream must not make the next save delete the previous answer.
        conversation = conversation with
        {
            Messages = messages,
            ModifiedAt = DateTimeOffset.UtcNow
        };

        var model = conversation.Model
            ?? throw new InvalidOperationException("No model selected for this conversation");

        var request = await WithRecallAsync(conversation, ChatRequest.FromConversation(conversation), ct);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (_generationGate) _activeGenerations[conversationId] = cts;

        var responseContent = new System.Text.StringBuilder();

        try
        {
            await foreach (var token in _inferenceClient.StreamChatAsync(request, cts.Token))
            {
                responseContent.Append(token);
                yield return token;
            }

            if (_conversations.ContainsKey(conversationId))
            {
                var assistantMessage = Message.Assistant(responseContent.ToString(), model);
                conversation = conversation.WithMessage(assistantMessage);
                _conversations[conversationId] = conversation;
                await SaveQuietly(conversation);
            }
        }
        finally
        {
            lock (_generationGate) _activeGenerations.Remove(conversationId);
            cts.Dispose();
        }
    }

    public async Task<Conversation> RemoveMessageAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            throw new KeyNotFoundException($"Conversation {conversationId} not found");
        }

        conversation = conversation.WithoutMessage(messageId);
        _conversations[conversationId] = conversation;

        await SaveQuietly(conversation, ct);

        _logger.LogInformation("Removed message {MessageId} from conversation {ConversationId}", messageId, conversationId);
        ConversationUpdated?.Invoke(this, new ConversationEventArgs { Conversation = conversation });

        return conversation;
    }

    public void StopGeneration(Guid conversationId)
    {
        CancellationTokenSource? cts;
        lock (_generationGate)
            _activeGenerations.TryGetValue(conversationId, out cts);

        if (cts is null)
            return;

        _logger.LogDebug("Stopping generation for conversation {Id}", conversationId);
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The reply finished and released its source between the lookup and the cancel.
        }
    }

    /// <summary>
    /// Appends project instructions and a few matching notes. The transcript itself is unchanged.
    /// </summary>
    private async Task<ChatRequest> WithRecallAsync(Conversation conversation, ChatRequest request, CancellationToken ct)
    {
        if (_memory is null)
            return request;

        try
        {
            var projectId = conversation.ProjectId ?? ChatProject.GeneralId;
            string? instructions = null;
            if (_projects is not null)
            {
                var project = await _projects.FindAsync(projectId, ct);
                instructions = project?.Instructions;
            }

            var query = conversation.Messages.LastOrDefault(message => message.Role == MessageRole.User)?.Content;
            var block = await _memory.BuildRecallAsync(projectId, conversation.Id, instructions, query, ct);
            if (string.IsNullOrWhiteSpace(block))
                return request;

            var prompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
                ? block
                : request.SystemPrompt.TrimEnd() + "\n\n" + block;
            return request with { SystemPrompt = prompt };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Skipping recall for conversation {Id}", conversation.Id);
            return request;
        }
    }

    private async Task ForgetSessionNotesAsync(Guid conversationId, CancellationToken ct)
    {
        if (_memory is null)
            return;

        try
        {
            await _memory.ForgetSessionAsync(conversationId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to forget session notes for {Id}", conversationId);
        }
    }

    /// <summary>
    /// Saves a conversation without throwing on failure. Returns false and raises
    /// <see cref="PersistenceFailed"/> when the file was not written. A cancel still propagates.
    /// </summary>
    private async Task<bool> SaveQuietly(Conversation conversation, CancellationToken ct = default)
    {
        try
        {
            await _writeGate.WaitAsync(ct);
            try
            {
                // A session deleted before this write got its turn stays deleted.
                if (!_conversations.ContainsKey(conversation.Id))
                    return true;

                await _storage.SaveAsync(conversation, ct);
            }
            finally
            {
                _writeGate.Release();
            }
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist conversation {Id}", conversation.Id);
            PersistenceFailed?.Invoke(this, new ConversationPersistenceFailedEventArgs
            {
                ConversationId = conversation.Id,
                Operation = ConversationPersistenceOperation.Save
            });
            return false;
        }
    }
}
