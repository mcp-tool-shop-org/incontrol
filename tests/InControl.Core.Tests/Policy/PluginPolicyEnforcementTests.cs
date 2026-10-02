using InControl.Core.Plugins;
using InControl.Core.Plugins.Samples;
using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy-governed plugin loading and execution.
/// </summary>
public class PluginPolicyEnforcementTests
{
    private static (PluginHost Host, PolicyGovernedPluginHost Governed) CreateGovernedHost(PolicyEngine? engine = null)
    {
        var sandbox = new TestPluginSandbox();
        var auditLog = new TestPluginAuditLog();
        var host = new PluginHost(sandbox, auditLog);
        var policyEngine = engine ?? new PolicyEngine();
        var governed = host.WithPolicyEnforcement(policyEngine);
        return (host, governed);
    }

    #region Policy Check Tests

    [Fact]
    public void CheckPluginPolicy_RequiresApprovalByDefault()
    {
        var (_, governed) = CreateGovernedHost();

        var check = governed.CheckPluginPolicy("com.example.plugin");

        Assert.False(check.CanLoad);
        Assert.Equal(PolicyDecision.AllowWithApproval, check.Decision);
        Assert.True(check.RequiresApproval);
    }

    [Fact]
    public void CheckPluginPolicy_RespectsExplicitAllow()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Allow = ["com.trusted.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var check = governed.CheckPluginPolicy("com.trusted.plugin");

        Assert.True(check.CanLoad);
        Assert.Equal(PolicyDecision.Allow, check.Decision);
        Assert.False(check.RequiresApproval);
    }

    [Fact]
    public void CheckPluginPolicy_RespectsExplicitDeny()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Deny = ["com.blocked.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var check = governed.CheckPluginPolicy("com.blocked.malware");

        Assert.False(check.CanLoad);
        Assert.Equal(PolicyDecision.Deny, check.Decision);
        Assert.False(check.RequiresApproval);
    }

    [Fact]
    public void CheckPluginPolicy_RespectsTrustedAuthors()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                TrustedAuthors = ["Verified Publisher"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var check = governed.CheckPluginPolicy("any.plugin", author: "Verified Publisher");

        Assert.True(check.CanLoad);
        Assert.Contains("trusted", check.Reason);
    }

    [Fact]
    public void CheckPluginPolicy_RespectsRiskLevelLimit()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                MaxRiskLevel = PluginRiskLevelPolicy.LocalMutation
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var networkCheck = governed.CheckPluginPolicy("plugin", riskLevel: PluginRiskLevel.Network);
        var localCheck = governed.CheckPluginPolicy("plugin", riskLevel: PluginRiskLevel.LocalMutation);
        var readCheck = governed.CheckPluginPolicy("plugin", riskLevel: PluginRiskLevel.ReadOnly);

        Assert.False(networkCheck.CanLoad);
        Assert.Equal(PolicyDecision.Deny, networkCheck.Decision);
        Assert.True(localCheck.RequiresApproval); // Default requires approval
        Assert.True(readCheck.RequiresApproval);
    }

    [Fact]
    public void CheckManifestPolicy_UsesManifestInfo()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                TrustedAuthors = ["InControl Team"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var manifest = HelloWorldPlugin.CreateManifest();
        var check = governed.CheckManifestPolicy(manifest);

        Assert.True(check.CanLoad);
    }

    #endregion

    #region Approval Tests

    [Fact]
    public void ApprovePlugin_AllowsLoading()
    {
        var (_, governed) = CreateGovernedHost();

        var beforeCheck = governed.CheckPluginPolicy("com.example.plugin");
        Assert.True(beforeCheck.RequiresApproval);

        governed.ApprovePlugin("com.example.plugin", "admin");

        var afterCheck = governed.CheckPluginPolicy("com.example.plugin");
        Assert.True(afterCheck.CanLoad);
        Assert.Equal(PolicySource.Session, afterCheck.Source);
        Assert.Contains("Pre-approved", afterCheck.Reason);
    }

    [Fact]
    public void RevokeApproval_BlocksLoading()
    {
        var (_, governed) = CreateGovernedHost();

        governed.ApprovePlugin("com.example.plugin");
        Assert.True(governed.CheckPluginPolicy("com.example.plugin").CanLoad);

        governed.RevokeApproval("com.example.plugin");

        Assert.False(governed.CheckPluginPolicy("com.example.plugin").CanLoad);
    }

    [Fact]
    public void ApprovePlugin_DoesNotOverrideDeny()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Deny = ["com.blocked.plugin"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        governed.ApprovePlugin("com.blocked.plugin");

        var check = governed.CheckPluginPolicy("com.blocked.plugin");
        Assert.False(check.CanLoad);
        Assert.Equal(PolicyDecision.Deny, check.Decision);
    }

    [Fact]
    public void GetApprovedPlugins_ReturnsAll()
    {
        var (_, governed) = CreateGovernedHost();

        governed.ApprovePlugin("plugin1", "user1");
        governed.ApprovePlugin("plugin2", "user2");

        var approved = governed.GetApprovedPlugins();

        Assert.Equal(2, approved.Count);
        Assert.Contains(approved, a => a.PluginId == "plugin1" && a.ApprovedBy == "user1");
        Assert.Contains(approved, a => a.PluginId == "plugin2" && a.ApprovedBy == "user2");
    }

    [Fact]
    public void ClearApprovals_RemovesAll()
    {
        var (_, governed) = CreateGovernedHost();

        governed.ApprovePlugin("plugin1");
        governed.ApprovePlugin("plugin2");

        governed.ClearApprovals();

        Assert.Empty(governed.GetApprovedPlugins());
    }

    #endregion

    #region Loading Tests

    [Fact]
    public async Task LoadPluginAsync_BlockedByPolicy()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Deny = ["com.incontrol.samples.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();

        var result = await governed.LoadPluginAsync(manifest, plugin);

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
        Assert.Equal(PolicySource.Organization, result.BlockSource);
    }

    [Fact]
    public async Task LoadPluginAsync_RequiresApproval()
    {
        var (_, governed) = CreateGovernedHost();

        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();

        var result = await governed.LoadPluginAsync(manifest, plugin);

        Assert.False(result.Success);
        Assert.True(result.RequiredApproval);
    }

    [Fact]
    public async Task LoadPluginAsync_SucceedsWhenApproved()
    {
        var (_, governed) = CreateGovernedHost();

        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();

        governed.ApprovePlugin(manifest.Id);
        var result = await governed.LoadPluginAsync(manifest, plugin);

        Assert.True(result.Success);
        Assert.Single(governed.LoadedPlugins);
    }

    [Fact]
    public async Task LoadPluginAsync_SucceedsWhenAllowed()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Allow = ["com.incontrol.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();

        var result = await governed.LoadPluginAsync(manifest, plugin);

        Assert.True(result.Success);
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task ExecuteAsync_FailsIfPluginNotLoaded()
    {
        var (_, governed) = CreateGovernedHost();

        var result = await governed.ExecuteAsync(
            "nonexistent",
            "action",
            new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("not loaded", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsForLoadedPlugin()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Allow = ["com.incontrol.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        await governed.LoadPluginAsync(manifest, plugin);

        var result = await governed.ExecuteAsync(
            manifest.Id,
            "greet",
            new Dictionary<string, object?> { ["name"] = "Test" });

        Assert.True(result.Success);
        Assert.False(result.WasBlocked);
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task LoadPluginAsync_RaisesPluginBlockedEvent()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { Deny = ["*"] }
        });
        var (_, governed) = CreateGovernedHost(engine);

        PluginBlockedEventArgs? capturedArgs = null;
        governed.PluginBlocked += (_, args) => capturedArgs = args;

        var manifest = HelloWorldPlugin.CreateManifest();
        await governed.LoadPluginAsync(manifest, new HelloWorldPlugin());

        Assert.NotNull(capturedArgs);
        Assert.Equal(manifest.Id, capturedArgs.PluginId);
    }

    [Fact]
    public async Task LoadPluginAsync_RaisesApprovalRequiredEvent()
    {
        var (_, governed) = CreateGovernedHost();

        PluginApprovalRequiredEventArgs? capturedArgs = null;
        governed.ApprovalRequired += (_, args) => capturedArgs = args;

        var manifest = HelloWorldPlugin.CreateManifest();
        await governed.LoadPluginAsync(manifest, new HelloWorldPlugin());

        Assert.NotNull(capturedArgs);
        Assert.Equal(manifest.Id, capturedArgs.PluginId);
        Assert.Equal(manifest.Name, capturedArgs.PluginName);
        Assert.Equal(manifest.Author, capturedArgs.Author);
    }

    #endregion

    #region Query Tests

    [Fact]
    public void GetLoadablePlugins_ExcludesDenied()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Deny = ["com.blocked.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var manifests = new[]
        {
            HelloWorldPlugin.CreateManifest(),
            new PluginManifestBuilder()
                .WithId("com.blocked.bad")
                .WithName("Bad Plugin")
                .WithVersion("1.0.0")
                .WithAuthor("Bad Actor")
                .WithDescription("A blocked plugin")
                .WithRiskLevel(PluginRiskLevel.ReadOnly)
                .Build()
        };

        var loadable = governed.GetLoadablePlugins(manifests);

        Assert.Single(loadable);
        Assert.Equal("com.incontrol.samples.hello-world", loadable[0].Manifest.Id);
    }

    [Fact]
    public void GetAllPluginsWithPolicy_IncludesAll()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Deny = ["com.blocked.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var manifests = new[]
        {
            HelloWorldPlugin.CreateManifest(),
            new PluginManifestBuilder()
                .WithId("com.blocked.bad")
                .WithName("Bad Plugin")
                .WithVersion("1.0.0")
                .WithAuthor("Bad Actor")
                .WithDescription("A blocked plugin")
                .WithRiskLevel(PluginRiskLevel.ReadOnly)
                .Build()
        };

        var all = governed.GetAllPluginsWithPolicy(manifests);

        Assert.Equal(2, all.Count);
        var blocked = all.First(p => p.Manifest.Id == "com.blocked.bad");
        Assert.Equal(PolicyDecision.Deny, blocked.PolicyStatus.Decision);
    }

    [Fact]
    public async Task GetLoadedPluginsWithPolicy_ShowsStatus()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Allow = ["com.incontrol.*"]
            }
        });
        var (_, governed) = CreateGovernedHost(engine);

        var manifest = HelloWorldPlugin.CreateManifest();
        await governed.LoadPluginAsync(manifest, new HelloWorldPlugin());

        var loaded = governed.GetLoadedPluginsWithPolicy();

        Assert.Single(loaded);
        Assert.Equal(PolicyDecision.Allow, loaded[0].PolicyStatus.Decision);
    }

    #endregion

    #region Extension Tests

    [Fact]
    public void WithPolicyEnforcement_CreatesWrapper()
    {
        var sandbox = new TestPluginSandbox();
        var auditLog = new TestPluginAuditLog();
        var host = new PluginHost(sandbox, auditLog);
        var engine = new PolicyEngine();

        var governed = host.WithPolicyEnforcement(engine);

        Assert.NotNull(governed);
        Assert.Same(engine, governed.PolicyEngine);
        Assert.Same(host, governed.InnerHost);
    }

    #endregion

    #region Test Implementations

    private sealed class TestPluginSandbox : IPluginSandbox
    {
        public IPluginContext CreateContext(PluginManifest manifest)
        {
            return PluginTestHelpers.CreateTestContext(manifest);
        }
    }

    private sealed class TestPluginAuditLog : IPluginAuditLog
    {
        public void LogPluginLoaded(string pluginId, string version) { }
        public void LogPluginUnloaded(string pluginId) { }
        public void LogPluginEnabled(string pluginId) { }
        public void LogPluginDisabled(string pluginId) { }
        public void LogPluginError(string pluginId, string action, string error) { }
        public void LogActionStarted(string pluginId, string actionId, Guid executionId) { }
        public void LogActionCompleted(string pluginId, string actionId, Guid executionId, bool success, TimeSpan duration) { }
        public void LogActionFailed(string pluginId, string actionId, Guid executionId, string error, TimeSpan duration) { }
        public void LogResourceAccess(string pluginId, ResourceAccessType resourceType, string resource, bool permitted, string? details = null) { }
        public void LogPermissionCheck(string pluginId, PermissionType permissionType, PermissionAccess access, string? scope, bool allowed) { }
        public IReadOnlyList<PluginAuditEntry> GetRecentEntries(int count = 100) => [];
        public IReadOnlyList<PluginAuditEntry> GetEntriesForPlugin(string pluginId, int count = 100) => [];
        public IReadOnlyList<PluginAuditEntry> GetEntriesByType(PluginAuditEventType eventType, int count = 100) => [];
        public IReadOnlyList<PluginAuditEntry> GetEntriesInRange(DateTimeOffset start, DateTimeOffset end) => [];
        public PluginAuditStatistics GetStatistics() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null, new Dictionary<string, int>());
        public PluginAuditStatistics GetStatisticsForPlugin(string pluginId) => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null, new Dictionary<string, int>());
        public PluginAuditExport ExportEntries(DateTimeOffset? start = null, DateTimeOffset? end = null) => new(DateTimeOffset.UtcNow, start, end, 0, [], GetStatistics());
        public void Clear() { }
    }

    #endregion
}
