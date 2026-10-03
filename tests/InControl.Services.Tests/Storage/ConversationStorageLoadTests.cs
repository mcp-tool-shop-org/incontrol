using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Core.Models;
using InControl.Core.State;
using InControl.Services.Storage;
using Xunit;

namespace InControl.Services.Tests.Storage;

public class ConversationStorageLoadTests : IDisposable
{
    private readonly string _root;

    public ConversationStorageLoadTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"InControlTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private JsonConversationStorage NewStorage() =>
        new(new FileStore(new Mock<ILogger<FileStore>>().Object, _root),
            new Mock<ILogger<JsonConversationStorage>>().Object);

    [Fact]
    public async Task LoadAll_WhenCancelled_ThrowsInsteadOfReturningAPartialList()
    {
        var storage = NewStorage();
        await storage.SaveAsync(Conversation.Create("One", "m", null, ChatProject.GeneralId));
        await storage.SaveAsync(Conversation.Create("Two", "m", null, ChatProject.GeneralId));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await storage.LoadAllAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadAll_StillSkipsACorruptFile()
    {
        var storage = NewStorage();
        var good = Conversation.Create("Good", "m", null, ChatProject.GeneralId);
        await storage.SaveAsync(good);
        await File.WriteAllTextAsync(Path.Combine(_root, "sessions", $"{Guid.NewGuid()}.json"), "{ not json");

        var all = await storage.LoadAllAsync();

        all.Should().ContainSingle(c => c.Id == good.Id);
    }
}
