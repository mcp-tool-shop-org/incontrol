using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Core.Models;
using InControl.Inference.Fakes;
using InControl.Services.Chat;
using InControl.Services.Interfaces;
using Xunit;

namespace InControl.Services.Tests.Chat;

public class ChatServiceDurabilityTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// In-memory storage with switches for the failures the chat service has to survive.
    /// </summary>
    private sealed class ScriptedStorage : IConversationStorage
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, Conversation> _files = new();
        private int _loadCalls;

        public bool DeleteSucceeds { get; set; } = true;
        public bool ThrowOnSave { get; set; }
        public bool CancelFirstLoad { get; set; }
        private bool _armed;
        public TaskCompletionSource? SaveStarted { get; private set; }
        public TaskCompletionSource? ReleaseSave { get; private set; }

        public bool Contains(Guid id)
        {
            lock (_lock) return _files.ContainsKey(id);
        }

        public void Seed(Conversation conversation)
        {
            lock (_lock) _files[conversation.Id] = conversation;
        }

        /// <summary>
        /// The next save signals <see cref="SaveStarted"/> and waits for <see cref="ReleaseSave"/>.
        /// </summary>
        public void BlockNextSave()
        {
            SaveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ReleaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _armed = true;
        }

        public async Task SaveAsync(Conversation conversation, CancellationToken ct = default)
        {
            if (_armed)
            {
                _armed = false;
                SaveStarted!.SetResult();
                await ReleaseSave!.Task;
            }

            if (ThrowOnSave)
                throw new IOException("disk full");

            lock (_lock) _files[conversation.Id] = conversation;
        }

        public Task<Conversation?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            lock (_lock) return Task.FromResult(_files.GetValueOrDefault(id));
        }

        public Task<IReadOnlyList<Conversation>> LoadAllAsync(CancellationToken ct = default)
        {
            if (CancelFirstLoad && _loadCalls++ == 0)
                throw new OperationCanceledException();

            lock (_lock) return Task.FromResult<IReadOnlyList<Conversation>>(_files.Values.ToList());
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (!DeleteSucceeds)
                return Task.FromResult(false);

            lock (_lock) return Task.FromResult(_files.Remove(id));
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Contains(id));

        public Task<string> ExportAsync(Guid id, CancellationToken ct = default) => Task.FromResult(string.Empty);

        public Task<Conversation> ImportAsync(string json, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static ChatService NewChat(ScriptedStorage storage, FakeInferenceClient? fake = null, ISessionMemory? memory = null) =>
        new(
            fake ?? new FakeInferenceClient().SetTokenDelay(TimeSpan.Zero),
            storage,
            new Mock<ILogger<ChatService>>().Object,
            memory: memory);

    private static async Task Drain(IAsyncEnumerable<string> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }

    [Fact]
    public async Task Delete_DuringTheFinalSave_StaysDeleted()
    {
        var storage = new ScriptedStorage();
        var chat = NewChat(storage);
        var session = await chat.CreateConversationAsync(model: "fake");

        var send = Task.Run(async () =>
        {
            var first = true;
            await foreach (var _ in chat.SendMessageAsync(session.Id, "Hello"))
            {
                // The prompt is already saved. Hold the save that stores the answer.
                if (first)
                {
                    first = false;
                    storage.BlockNextSave();
                }
            }
        });

        // Wait until the answer's save is in flight.
        while (storage.SaveStarted is null)
        {
            await Task.Delay(5);
            send.IsFaulted.Should().BeFalse();
        }
        await storage.SaveStarted.Task.WaitAsync(Wait);

        var delete = chat.DeleteConversationAsync(session.Id);
        await Task.Delay(100);
        storage.ReleaseSave!.SetResult();
        await Task.WhenAll(send, delete).WaitAsync(Wait);

        storage.Contains(session.Id).Should().BeFalse("the delete came after the save began and must win");
        (await chat.GetConversationAsync(session.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenTheFileCannotBeDeleted_KeepsTheSessionAndItsNotes()
    {
        var storage = new ScriptedStorage();
        var memory = new Mock<ISessionMemory>();
        var chat = NewChat(storage, memory: memory.Object);
        var session = await chat.CreateConversationAsync(model: "fake");

        var deletedRaised = false;
        ConversationPersistenceFailedEventArgs? failure = null;
        chat.ConversationDeleted += (_, _) => deletedRaised = true;
        chat.PersistenceFailed += (_, e) => failure = e;
        storage.DeleteSucceeds = false;

        var result = await chat.DeleteConversationAsync(session.Id);

        result.Should().BeFalse();
        deletedRaised.Should().BeFalse();
        failure.Should().NotBeNull();
        failure!.ConversationId.Should().Be(session.Id);
        failure.Operation.Should().Be(ConversationPersistenceOperation.Delete);
        memory.Verify(m => m.ForgetSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        (await chat.GetConversationAsync(session.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_WhenThereWasNoFile_StillRemovesTheSession()
    {
        var storage = new ScriptedStorage { ThrowOnSave = true };
        var memory = new Mock<ISessionMemory>();
        var chat = NewChat(storage, memory: memory.Object);
        // The save fails, so the session exists only in memory.
        var session = await chat.CreateConversationAsync(model: "fake");

        var result = await chat.DeleteConversationAsync(session.Id);

        result.Should().BeTrue();
        memory.Verify(m => m.ForgetSessionAsync(session.Id, It.IsAny<CancellationToken>()), Times.Once);
        (await chat.GetConversationAsync(session.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenTheFileIsDeleted_ForgetsNotesAndRaisesDeleted()
    {
        var storage = new ScriptedStorage();
        var memory = new Mock<ISessionMemory>();
        var chat = NewChat(storage, memory: memory.Object);
        var session = await chat.CreateConversationAsync(model: "fake");
        var deletedRaised = false;
        chat.ConversationDeleted += (_, _) => deletedRaised = true;

        var result = await chat.DeleteConversationAsync(session.Id);

        result.Should().BeTrue();
        deletedRaised.Should().BeTrue();
        storage.Contains(session.Id).Should().BeFalse();
        memory.Verify(m => m.ForgetSessionAsync(session.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_WhenTheAnswerCannotBeSaved_ReportsIt()
    {
        var storage = new ScriptedStorage();
        var chat = NewChat(storage);
        var session = await chat.CreateConversationAsync(model: "fake");
        var failures = new List<ConversationPersistenceFailedEventArgs>();
        chat.PersistenceFailed += (_, e) => failures.Add(e);

        storage.ThrowOnSave = true;
        await Drain(chat.SendMessageAsync(session.Id, "Hello"));

        failures.Should().NotBeEmpty();
        failures.Should().OnlyContain(f =>
            f.ConversationId == session.Id && f.Operation == ConversationPersistenceOperation.Save);
    }

    [Fact]
    public async Task EnsureLoaded_AfterACancelledLoad_LoadsOnTheNextCall()
    {
        var storage = new ScriptedStorage { CancelFirstLoad = true };
        var stored = Conversation.Create("Stored", "fake", null, ChatProject.GeneralId);
        storage.Seed(stored);
        var chat = NewChat(storage);

        var first = async () => await chat.EnsureLoadedAsync();
        await first.Should().ThrowAsync<OperationCanceledException>();

        var again = await chat.GetConversationAsync(stored.Id);

        again.Should().NotBeNull("a cancelled load must be retried, not remembered as finished");
    }

    [Fact]
    public async Task StopGeneration_RacingTheEndOfAReply_NeverThrows()
    {
        var storage = new ScriptedStorage();
        var fake = new FakeInferenceClient().SetTokenDelay(TimeSpan.Zero);
        var chat = NewChat(storage, fake);
        var session = await chat.CreateConversationAsync(model: "fake");
        using var done = new CancellationTokenSource();
        var failures = new List<Exception>();

        var stopper = Task.Run(() =>
        {
            while (!done.IsCancellationRequested)
            {
                try
                {
                    chat.StopGeneration(session.Id);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }
        });

        for (var i = 0; i < 400; i++)
        {
            try
            {
                await Drain(chat.SendMessageAsync(session.Id, "Hello"));
            }
            catch (OperationCanceledException)
            {
                // Stopped on purpose.
            }
        }

        await done.CancelAsync();
        await stopper.WaitAsync(Wait);

        failures.Should().BeEmpty();
    }
}
