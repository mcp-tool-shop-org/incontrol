using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class AssistantMemoryItemTests
{
    [Fact]
    public void Create_SetsAllRequiredFields()
    {
        var item = AssistantMemoryItem.Create(
            MemoryType.Preference,
            MemoryScope.User,
            MemorySource.ExplicitUser,
            "response_style",
            "concise",
            "User requested brief responses"
        );

        item.Id.Should().NotBe(Guid.Empty);
        item.Type.Should().Be(MemoryType.Preference);
        item.Scope.Should().Be(MemoryScope.User);
        item.Source.Should().Be(MemorySource.ExplicitUser);
        item.Key.Should().Be("response_style");
        item.Value.Should().Be("concise");
        item.Justification.Should().Be("User requested brief responses");
        item.Confidence.Should().Be(1.0);
        item.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithLowerConfidence_ForInferred()
    {
        var item = AssistantMemoryItem.Create(
            MemoryType.Fact,
            MemoryScope.Session,
            MemorySource.Inferred,
            "project_name",
            "InControl",
            "Inferred from conversation context",
            confidence: 0.8
        );

        item.Confidence.Should().Be(0.8);
        item.Source.Should().Be(MemorySource.Inferred);
    }

    [Theory]
    [InlineData(MemoryType.Preference)]
    [InlineData(MemoryType.Fact)]
    [InlineData(MemoryType.Instruction)]
    [InlineData(MemoryType.Entity)]
    [InlineData(MemoryType.Decision)]
    public void MemoryType_AllValuesAreDefined(MemoryType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Theory]
    [InlineData(MemoryScope.Session)]
    [InlineData(MemoryScope.User)]
    [InlineData(MemoryScope.Global)]
    public void MemoryScope_AllValuesAreDefined(MemoryScope scope)
    {
        Enum.IsDefined(scope).Should().BeTrue();
    }

    [Theory]
    [InlineData(MemorySource.ExplicitUser)]
    [InlineData(MemorySource.Inferred)]
    [InlineData(MemorySource.System)]
    public void MemorySource_AllValuesAreDefined(MemorySource source)
    {
        Enum.IsDefined(source).Should().BeTrue();
    }
}

public class AssistantMemoryStoreTests
{
    [Fact]
    public void Add_IncreasesCount()
    {
        var store = new AssistantMemoryStore();
        var item = CreateTestMemory();

        store.Add(item);

        store.Count.Should().Be(1);
    }

    [Fact]
    public void Get_ReturnsAddedItem()
    {
        var store = new AssistantMemoryStore();
        var item = CreateTestMemory();
        store.Add(item);

        var retrieved = store.Get(item.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Key.Should().Be(item.Key);
    }

    [Fact]
    public void Get_ReturnsNull_WhenNotFound()
    {
        var store = new AssistantMemoryStore();

        var retrieved = store.Get(Guid.NewGuid());

        retrieved.Should().BeNull();
    }

    [Fact]
    public void Remove_RemovesItem()
    {
        var store = new AssistantMemoryStore();
        var item = CreateTestMemory();
        store.Add(item);

        var result = store.Remove(item.Id);

        result.Should().BeTrue();
        store.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenNotFound()
    {
        var store = new AssistantMemoryStore();

        var result = store.Remove(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void Update_UpdatesExistingItem()
    {
        var store = new AssistantMemoryStore();
        var item = CreateTestMemory();
        store.Add(item);

        var updated = item with { Value = "updated value" };
        var result = store.Update(updated);

        result.Should().BeTrue();
        store.Get(item.Id)!.Value.Should().Be("updated value");
    }

    [Fact]
    public void Update_ReturnsFalse_WhenNotFound()
    {
        var store = new AssistantMemoryStore();
        var item = CreateTestMemory();

        var result = store.Update(item);

        result.Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var store = new AssistantMemoryStore();
        store.Add(CreateTestMemory("key1"));
        store.Add(CreateTestMemory("key2"));
        store.Add(CreateTestMemory("key3"));

        store.Clear();

        store.Count.Should().Be(0);
    }

    [Fact]
    public void All_ReturnsAllItems()
    {
        var store = new AssistantMemoryStore();
        store.Add(CreateTestMemory("key1"));
        store.Add(CreateTestMemory("key2"));

        var all = store.All;

        all.Should().HaveCount(2);
    }

    [Fact]
    public void FindByType_FiltersCorrectly()
    {
        var store = new AssistantMemoryStore();
        store.Add(CreateTestMemory("pref1", MemoryType.Preference));
        store.Add(CreateTestMemory("fact1", MemoryType.Fact));
        store.Add(CreateTestMemory("pref2", MemoryType.Preference));

        var preferences = store.FindByType(MemoryType.Preference);

        preferences.Should().HaveCount(2);
        preferences.Should().AllSatisfy(m => m.Type.Should().Be(MemoryType.Preference));
    }

    [Fact]
    public void FindByScope_FiltersCorrectly()
    {
        var store = new AssistantMemoryStore();
        store.Add(CreateTestMemory("session1", scope: MemoryScope.Session));
        store.Add(CreateTestMemory("user1", scope: MemoryScope.User));
        store.Add(CreateTestMemory("session2", scope: MemoryScope.Session));

        var sessionMemories = store.FindByScope(MemoryScope.Session);

        sessionMemories.Should().HaveCount(2);
    }

    [Fact]
    public void FindByKey_FindsPartialMatch()
    {
        var store = new AssistantMemoryStore();
        store.Add(CreateTestMemory("response_style"));
        store.Add(CreateTestMemory("response_format"));
        store.Add(CreateTestMemory("project_name"));

        var matches = store.FindByKey("response");

        matches.Should().HaveCount(2);
    }

    [Fact]
    public void ClearSessionMemories_OnlyRemovesSessionScoped()
    {
        var store = new AssistantMemoryStore();
        store.Add(CreateTestMemory("session1", scope: MemoryScope.Session));
        store.Add(CreateTestMemory("user1", scope: MemoryScope.User));
        store.Add(CreateTestMemory("session2", scope: MemoryScope.Session));

        var removed = store.ClearSessionMemories();

        removed.Should().Be(2);
        store.Count.Should().Be(1);
        store.All.Single().Scope.Should().Be(MemoryScope.User);
    }

    [Fact]
    public void ExportToJson_ProducesValidJson()
    {
        var store = new AssistantMemoryStore();
        store.Add(CreateTestMemory("key1"));
        store.Add(CreateTestMemory("key2"));

        var json = store.ExportToJson();

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("key1");
        json.Should().Contain("key2");
    }

    [Fact]
    public void MemoryAdded_EventFires()
    {
        var store = new AssistantMemoryStore();
        MemoryChangedEventArgs? capturedArgs = null;
        store.MemoryAdded += (_, args) => capturedArgs = args;

        var item = CreateTestMemory();
        store.Add(item);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.ChangeType.Should().Be(MemoryChangeType.Added);
        capturedArgs.Memory.Id.Should().Be(item.Id);
    }

    [Fact]
    public void MemoryRemoved_EventFires()
    {
        var store = new AssistantMemoryStore();
        var item = CreateTestMemory();
        store.Add(item);

        MemoryChangedEventArgs? capturedArgs = null;
        store.MemoryRemoved += (_, args) => capturedArgs = args;

        store.Remove(item.Id);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.ChangeType.Should().Be(MemoryChangeType.Removed);
    }

    [Fact]
    public void MemoryUpdated_EventFires()
    {
        var store = new AssistantMemoryStore();
        var item = CreateTestMemory();
        store.Add(item);

        MemoryChangedEventArgs? capturedArgs = null;
        store.MemoryUpdated += (_, args) => capturedArgs = args;

        store.Update(item with { Value = "new value" });

        capturedArgs.Should().NotBeNull();
        capturedArgs!.ChangeType.Should().Be(MemoryChangeType.Updated);
    }

    private static AssistantMemoryItem CreateTestMemory(
        string key = "test_key",
        MemoryType type = MemoryType.Preference,
        MemoryScope scope = MemoryScope.User)
    {
        return AssistantMemoryItem.Create(
            type,
            scope,
            MemorySource.ExplicitUser,
            key,
            "test_value"
        );
    }
}
