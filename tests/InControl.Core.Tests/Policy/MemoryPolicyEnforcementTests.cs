using InControl.Core.Assistant;
using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy-governed memory operations.
/// </summary>
public class MemoryPolicyEnforcementTests
{
    private static (AssistantMemoryStore Store, MemoryConsentManager Consent, PolicyGovernedMemoryManager Governed) CreateGovernedMemory(PolicyEngine? engine = null)
    {
        var store = new AssistantMemoryStore();
        var consent = new MemoryConsentManager(store);
        var policyEngine = engine ?? new PolicyEngine();
        var governed = consent.WithPolicyEnforcement(store, policyEngine);
        return (store, consent, governed);
    }

    #region Basic Policy Tests

    [Fact]
    public void CheckMemoryPolicy_AllowsByDefault()
    {
        var (_, _, governed) = CreateGovernedMemory();

        var status = governed.CheckMemoryPolicy();

        Assert.True(status.CanRemember);
        Assert.True(status.CanExport);
        Assert.True(status.CanImport);
        Assert.True(status.AutoFormationAllowed);
    }

    [Fact]
    public void CheckMemoryPolicy_RespectsDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { Enabled = false }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        var status = governed.CheckMemoryPolicy();

        Assert.False(status.CanRemember);
        Assert.Contains("disabled", status.Reason);
    }

    [Fact]
    public void CheckMemoryPolicy_RespectsCapacityLimit()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { MaxMemories = 5 }
        });
        var (store, _, governed) = CreateGovernedMemory(engine);

        // Add 5 memories
        for (int i = 0; i < 5; i++)
        {
            store.Add(AssistantMemoryItem.Create(
                MemoryType.Fact, MemoryScope.User, MemorySource.ExplicitUser,
                $"key{i}", $"value{i}"));
        }

        var status = governed.CheckMemoryPolicy();

        Assert.False(status.CanRemember);
        Assert.Contains("limit", status.Reason);
        Assert.Equal(5, status.MemoryCount);
        Assert.Equal(5, status.MaxMemories);
    }

    [Fact]
    public void CheckMemoryPolicy_RespectsExportSetting()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { AllowExport = false }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        var status = governed.CheckMemoryPolicy();

        Assert.False(status.CanExport);
        Assert.True(status.CanRemember); // Still can remember
    }

    [Fact]
    public void CheckMemoryPolicy_RespectsAutoFormation()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { AutoFormation = false }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        var status = governed.CheckMemoryPolicy();

        Assert.False(status.AutoFormationAllowed);
        Assert.True(status.CanRemember); // Explicit still works
    }

    #endregion

    #region Category Exclusion Tests

    [Fact]
    public void IsCategoryAllowed_TrueByDefault()
    {
        var (_, _, governed) = CreateGovernedMemory();

        Assert.True(governed.IsCategoryAllowed("any-category"));
    }

    [Fact]
    public void IsCategoryAllowed_RespectsExclusions()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules
            {
                ExcludeCategories = ["credentials", "pii"]
            }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        Assert.False(governed.IsCategoryAllowed("credentials"));
        Assert.False(governed.IsCategoryAllowed("PII")); // Case insensitive
        Assert.True(governed.IsCategoryAllowed("facts"));
    }

    [Fact]
    public void RequestRemember_BlockedByCategory()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules
            {
                ExcludeCategories = ["passwords"]
            }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        var result = governed.RequestRemember(
            MemoryType.Fact,
            "user_password",
            "secret123",
            "User mentioned password",
            category: "passwords");

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
        Assert.Contains("excluded", result.BlockReason);
    }

    #endregion

    #region Remember Tests

    [Fact]
    public void RequestRemember_CreatesConsentRequest()
    {
        var (_, _, governed) = CreateGovernedMemory();

        var result = governed.RequestRemember(
            MemoryType.Preference,
            "coding_style",
            "prefers tabs",
            "User mentioned preference");

        Assert.False(result.Success); // Not created yet, pending approval
        Assert.True(result.IsPending);
        Assert.NotNull(result.ConsentRequest);
        Assert.Equal("coding_style", result.ConsentRequest.Key);
    }

    [Fact]
    public void RequestRemember_BlockedWhenDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { Enabled = false }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        var result = governed.RequestRemember(
            MemoryType.Fact,
            "key",
            "value",
            "justification");

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
        Assert.Contains("disabled", result.BlockReason);
    }

    [Fact]
    public void RequestRemember_BlockedWhenAutoFormationDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { AutoFormation = false }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        var result = governed.RequestRemember(
            MemoryType.Fact,
            "key",
            "value",
            "justification",
            source: MemorySource.Inferred); // Inferred = auto-formation

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
        Assert.Contains("Auto", result.BlockReason);
    }

    [Fact]
    public void RememberExplicit_CreatesMemory()
    {
        var (store, _, governed) = CreateGovernedMemory();

        var result = governed.RememberExplicit(
            MemoryType.Preference,
            "theme",
            "dark");

        Assert.True(result.Success);
        Assert.NotNull(result.CreatedMemory);
        Assert.Equal("theme", result.CreatedMemory.Key);
        Assert.Single(store.All);
    }

    [Fact]
    public void RememberExplicit_BlockedWhenAtCapacity()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { MaxMemories = 2 }
        });
        var (store, _, governed) = CreateGovernedMemory(engine);

        // Fill to capacity
        store.Add(AssistantMemoryItem.Create(MemoryType.Fact, MemoryScope.User, MemorySource.ExplicitUser, "k1", "v1"));
        store.Add(AssistantMemoryItem.Create(MemoryType.Fact, MemoryScope.User, MemorySource.ExplicitUser, "k2", "v2"));

        var result = governed.RememberExplicit(MemoryType.Fact, "k3", "v3");

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
    }

    #endregion

    #region Export Tests

    [Fact]
    public void Export_SucceedsWhenAllowed()
    {
        var (store, _, governed) = CreateGovernedMemory();
        store.Add(AssistantMemoryItem.Create(MemoryType.Fact, MemoryScope.User, MemorySource.ExplicitUser, "key", "value"));

        var result = governed.Export();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Json);
        Assert.Equal(1, result.MemoryCount);
    }

    [Fact]
    public void Export_BlockedByPolicy()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { AllowExport = false }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        var result = governed.Export();

        Assert.False(result.IsSuccess);
        Assert.Contains("disabled", result.BlockReason);
    }

    #endregion

    #region Retention Tests

    [Fact]
    public void ApplyRetentionPolicy_NoLimitReturnsZero()
    {
        var (store, _, governed) = CreateGovernedMemory();
        store.Add(AssistantMemoryItem.Create(MemoryType.Fact, MemoryScope.User, MemorySource.ExplicitUser, "key", "value"));

        var purged = governed.ApplyRetentionPolicy();

        Assert.Equal(0, purged);
        Assert.Single(store.All);
    }

    [Fact]
    public void ApplyRetentionPolicy_RemovesExpiredMemories()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { MaxRetentionDays = 30 }
        });
        var (store, _, governed) = CreateGovernedMemory(engine);

        // Add an old memory (simulate by creating with old date)
        var oldMemory = new AssistantMemoryItem
        {
            Id = Guid.NewGuid(),
            Type = MemoryType.Fact,
            Scope = MemoryScope.User,
            Source = MemorySource.ExplicitUser,
            Key = "old",
            Value = "expired",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-60), // 60 days old
            LastAccessedAt = DateTimeOffset.UtcNow.AddDays(-60)
        };
        store.Add(oldMemory);

        // Add a new memory
        store.Add(AssistantMemoryItem.Create(MemoryType.Fact, MemoryScope.User, MemorySource.ExplicitUser, "new", "fresh"));

        var purged = governed.ApplyRetentionPolicy();

        Assert.Equal(1, purged);
        Assert.Single(store.All);
        Assert.Equal("new", store.All[0].Key);
    }

    [Fact]
    public void EnforceCountLimit_RemovesOldest()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { MaxMemories = 2 }
        });
        var (store, _, governed) = CreateGovernedMemory(engine);

        // Add memories with different times
        store.Add(new AssistantMemoryItem
        {
            Id = Guid.NewGuid(),
            Type = MemoryType.Fact,
            Scope = MemoryScope.User,
            Source = MemorySource.ExplicitUser,
            Key = "oldest",
            Value = "v1",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3),
            LastAccessedAt = DateTimeOffset.UtcNow
        });
        store.Add(new AssistantMemoryItem
        {
            Id = Guid.NewGuid(),
            Type = MemoryType.Fact,
            Scope = MemoryScope.User,
            Source = MemorySource.ExplicitUser,
            Key = "middle",
            Value = "v2",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            LastAccessedAt = DateTimeOffset.UtcNow
        });
        store.Add(new AssistantMemoryItem
        {
            Id = Guid.NewGuid(),
            Type = MemoryType.Fact,
            Scope = MemoryScope.User,
            Source = MemorySource.ExplicitUser,
            Key = "newest",
            Value = "v3",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            LastAccessedAt = DateTimeOffset.UtcNow
        });

        var removed = governed.EnforceCountLimit();

        Assert.Equal(1, removed);
        Assert.Equal(2, store.Count);
        Assert.DoesNotContain(store.All, m => m.Key == "oldest");
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_ReturnsComprehensiveInfo()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules
            {
                MaxMemories = 100,
                MaxRetentionDays = 90,
                ExcludeCategories = ["pii"]
            }
        });
        var (store, _, governed) = CreateGovernedMemory(engine);

        store.Add(AssistantMemoryItem.Create(MemoryType.Fact, MemoryScope.User, MemorySource.ExplicitUser, "k1", "v1"));
        store.Add(AssistantMemoryItem.Create(MemoryType.Preference, MemoryScope.User, MemorySource.Inferred, "k2", "v2"));
        store.Add(AssistantMemoryItem.Create(MemoryType.Fact, MemoryScope.Session, MemorySource.System, "k3", "v3"));

        var stats = governed.GetStatistics();

        Assert.Equal(3, stats.TotalMemories);
        Assert.Equal(100, stats.MaxMemories);
        Assert.Equal(3.0, stats.CapacityUsedPercent);
        Assert.Equal(90, stats.RetentionDays);
        Assert.Equal(2, stats.ByType[MemoryType.Fact]);
        Assert.Equal(1, stats.ByType[MemoryType.Preference]);
        Assert.Equal(2, stats.ByScope[MemoryScope.User]);
        Assert.Contains("pii", stats.ExcludedCategories);
        Assert.True(stats.AutoFormationEnabled);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void RequestRemember_RaisesBlockedEvent()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { Enabled = false }
        });
        var (_, _, governed) = CreateGovernedMemory(engine);

        MemoryBlockedEventArgs? capturedArgs = null;
        governed.MemoryBlocked += (_, args) => capturedArgs = args;

        governed.RequestRemember(MemoryType.Fact, "key", "value", "justification");

        Assert.NotNull(capturedArgs);
        Assert.Equal("key", capturedArgs.Key);
        Assert.Equal(MemoryBlockReason.PolicyDisabled, capturedArgs.BlockReason);
    }

    [Fact]
    public void ApplyRetentionPolicy_RaisesPurgedEvent()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { MaxRetentionDays = 7 }
        });
        var (store, _, governed) = CreateGovernedMemory(engine);

        store.Add(new AssistantMemoryItem
        {
            Id = Guid.NewGuid(),
            Type = MemoryType.Fact,
            Scope = MemoryScope.User,
            Source = MemorySource.ExplicitUser,
            Key = "old",
            Value = "expired",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastAccessedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });

        MemoriesPurgedEventArgs? capturedArgs = null;
        governed.MemoriesPurged += (_, args) => capturedArgs = args;

        governed.ApplyRetentionPolicy();

        Assert.NotNull(capturedArgs);
        Assert.Equal(1, capturedArgs.Count);
        Assert.Equal(7, capturedArgs.RetentionDays);
    }

    #endregion

    #region Extension Tests

    [Fact]
    public void WithPolicyEnforcement_CreatesWrapper()
    {
        var store = new AssistantMemoryStore();
        var consent = new MemoryConsentManager(store);
        var engine = new PolicyEngine();

        var governed = consent.WithPolicyEnforcement(store, engine);

        Assert.NotNull(governed);
        Assert.Same(engine, governed.PolicyEngine);
        Assert.Same(store, governed.Store);
        Assert.Same(consent, governed.ConsentManager);
    }

    #endregion
}
