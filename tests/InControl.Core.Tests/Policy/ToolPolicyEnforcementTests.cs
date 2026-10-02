using InControl.Core.Assistant;
using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy-governed tool execution.
/// </summary>
public class ToolPolicyEnforcementTests
{
    private static ToolRegistry CreateRegistryWithTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new TestTool("read-file", "Read File", ToolRiskLevel.Low, isReadOnly: true));
        registry.Register(new TestTool("write-file", "Write File", ToolRiskLevel.Medium, isReadOnly: false));
        registry.Register(new TestTool("web-search", "Web Search", ToolRiskLevel.Medium, requiresNetwork: true));
        registry.Register(new TestTool("execute-shell", "Execute Shell", ToolRiskLevel.Critical, isReadOnly: false));
        registry.Register(new TestTool("delete-file", "Delete File", ToolRiskLevel.High, isReadOnly: false));
        return registry;
    }

    #region Basic Policy Check Tests

    [Fact]
    public void CheckToolPolicy_AllowsByDefault()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        var governed = registry.WithPolicyEnforcement(engine);

        var check = governed.CheckToolPolicy("read-file");

        Assert.True(check.CanExecute);
        Assert.Equal(PolicyDecision.Allow, check.Decision);
        Assert.False(check.RequiresApproval);
    }

    [Fact]
    public void CheckToolPolicy_ReturnsNotFoundForMissingTool()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        var governed = registry.WithPolicyEnforcement(engine);

        var check = governed.CheckToolPolicy("nonexistent");

        Assert.False(check.CanExecute);
        Assert.Equal(PolicyDecision.Deny, check.Decision);
        Assert.Contains("not found", check.Reason);
    }

    [Fact]
    public void CheckToolPolicy_RespectsExplicitDeny()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["execute-shell"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var check = governed.CheckToolPolicy("execute-shell");

        Assert.False(check.CanExecute);
        Assert.Equal(PolicyDecision.Deny, check.Decision);
        Assert.Equal(PolicySource.Organization, check.Source);
    }

    [Fact]
    public void CheckToolPolicy_RespectsRequireApproval()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var check = governed.CheckToolPolicy("web-search");

        Assert.False(check.CanExecute); // Can't execute without approval
        Assert.Equal(PolicyDecision.AllowWithApproval, check.Decision);
        Assert.True(check.RequiresApproval);
    }

    [Fact]
    public void CheckToolPolicy_IncludesConstraints()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Rules =
                [
                    new ToolRule
                    {
                        Id = "limit-search",
                        Tool = "web-search",
                        Decision = PolicyDecision.AllowWithConstraints,
                        Constraints = new Dictionary<string, object> { ["max_per_hour"] = 50 }
                    }
                ]
            }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var check = governed.CheckToolPolicy("web-search");

        Assert.True(check.CanExecute);
        Assert.Equal(PolicyDecision.AllowWithConstraints, check.Decision);
        Assert.NotNull(check.Constraints);
    }

    #endregion

    #region Session Approval Tests

    [Fact]
    public void SessionApproval_OverridesRequireApproval()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        // Before approval
        var beforeCheck = governed.CheckToolPolicy("web-search");
        Assert.True(beforeCheck.RequiresApproval);
        Assert.False(beforeCheck.CanExecute);

        // Grant approval
        governed.GrantSessionApproval("web-search");

        // After approval
        var afterCheck = governed.CheckToolPolicy("web-search");
        Assert.False(afterCheck.RequiresApproval);
        Assert.True(afterCheck.CanExecute);
        Assert.Equal(PolicySource.Session, afterCheck.Source);
    }

    [Fact]
    public void SessionApproval_CanBeRevoked()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        governed.GrantSessionApproval("web-search");
        Assert.True(governed.CheckToolPolicy("web-search").CanExecute);

        governed.RevokeSessionApproval("web-search");
        Assert.False(governed.CheckToolPolicy("web-search").CanExecute);
    }

    [Fact]
    public void SessionApproval_DoesNotOverrideDeny()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["execute-shell"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        governed.GrantSessionApproval("execute-shell");

        var check = governed.CheckToolPolicy("execute-shell");
        Assert.False(check.CanExecute); // Still denied
        Assert.Equal(PolicyDecision.Deny, check.Decision);
    }

    [Fact]
    public void ClearSessionApprovals_RevokesAll()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search", "write-file"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        governed.GrantSessionApproval("web-search");
        governed.GrantSessionApproval("write-file");
        Assert.Equal(2, governed.GetSessionApprovedTools().Count);

        governed.ClearSessionApprovals();

        Assert.Empty(governed.GetSessionApprovedTools());
        Assert.False(governed.CheckToolPolicy("web-search").CanExecute);
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task ExecuteAsync_SucceedsWhenAllowed()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        var governed = registry.WithPolicyEnforcement(engine);

        var result = await governed.ExecuteAsync("read-file", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.False(result.WasBlocked);
        Assert.NotNull(result.ToolResult);
    }

    [Fact]
    public async Task ExecuteAsync_BlockedByPolicy()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["execute-shell"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var result = await governed.ExecuteAsync("execute-shell", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.True(result.WasBlocked);
        Assert.Null(result.ToolResult);
        Assert.NotNull(result.BlockReason);
        Assert.Equal(PolicySource.Organization, result.BlockSource);
    }

    [Fact]
    public async Task ExecuteAsync_RequiresApproval()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var result = await governed.ExecuteAsync("web-search", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.True(result.RequiredApproval);
        Assert.False(result.WasBlocked);
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsAfterApproval()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        governed.GrantSessionApproval("web-search");
        var result = await governed.ExecuteAsync("web-search", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.NotNull(result.ToolResult);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesAppliedConstraints()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Rules =
                [
                    new ToolRule
                    {
                        Id = "limit",
                        Tool = "web-search",
                        Decision = PolicyDecision.AllowWithConstraints,
                        Constraints = new Dictionary<string, object> { ["max"] = 10 }
                    }
                ]
            }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var result = await governed.ExecuteAsync("web-search", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.NotNull(result.AppliedConstraints);
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task ExecuteAsync_RaisesToolBlockedEvent()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["execute-shell"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        ToolBlockedEventArgs? capturedArgs = null;
        governed.ToolBlocked += (_, args) => capturedArgs = args;

        await governed.ExecuteAsync("execute-shell", new Dictionary<string, object?>());

        Assert.NotNull(capturedArgs);
        Assert.Equal("execute-shell", capturedArgs.ToolId);
        Assert.Equal(PolicySource.Organization, capturedArgs.Source);
    }

    [Fact]
    public async Task ExecuteAsync_RaisesApprovalRequiredEvent()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        ToolApprovalRequiredEventArgs? capturedArgs = null;
        governed.ApprovalRequired += (_, args) => capturedArgs = args;

        await governed.ExecuteAsync("web-search", new Dictionary<string, object?>());

        Assert.NotNull(capturedArgs);
        Assert.Equal("web-search", capturedArgs.ToolId);
    }

    #endregion

    #region Available Tools Tests

    [Fact]
    public void GetAvailableTools_ExcludesDenied()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["execute-shell", "delete-*"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var available = governed.GetAvailableTools();

        Assert.Equal(3, available.Count); // read-file, write-file, web-search
        Assert.DoesNotContain(available, t => t.Tool.Id == "execute-shell");
        Assert.DoesNotContain(available, t => t.Tool.Id == "delete-file");
    }

    [Fact]
    public void GetAvailableTools_IncludesRequireApproval()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-search"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var available = governed.GetAvailableTools();

        var webSearch = available.FirstOrDefault(t => t.Tool.Id == "web-search");
        Assert.NotNull(webSearch);
        Assert.True(webSearch.PolicyStatus.RequiresApproval);
    }

    [Fact]
    public void GetAllToolsWithPolicy_IncludesAll()
    {
        var registry = CreateRegistryWithTools();
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["execute-shell"] }
        });
        var governed = registry.WithPolicyEnforcement(engine);

        var all = governed.GetAllToolsWithPolicy();

        Assert.Equal(5, all.Count);
        var shell = all.First(t => t.Tool.Id == "execute-shell");
        Assert.Equal(PolicyDecision.Deny, shell.PolicyStatus.Decision);
    }

    #endregion

    #region Extension Tests

    [Fact]
    public void WithPolicyEnforcement_CreatesWrapper()
    {
        var registry = new ToolRegistry();
        var engine = new PolicyEngine();

        var governed = registry.WithPolicyEnforcement(engine);

        Assert.NotNull(governed);
        Assert.Same(engine, governed.PolicyEngine);
    }

    #endregion

    /// <summary>
    /// Test implementation of IAssistantTool.
    /// </summary>
    private sealed class TestTool : IAssistantTool
    {
        public string Id { get; }
        public string Name { get; }
        public string Description => $"Test tool: {Name}";
        public ToolRiskLevel RiskLevel { get; }
        public bool IsReadOnly { get; }
        public bool RequiresNetwork { get; }
        public IReadOnlyList<ToolParameter> Parameters { get; } = [];

        public TestTool(string id, string name, ToolRiskLevel riskLevel, bool isReadOnly = true, bool requiresNetwork = false)
        {
            Id = id;
            Name = name;
            RiskLevel = riskLevel;
            IsReadOnly = isReadOnly;
            RequiresNetwork = requiresNetwork;
        }

        public Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct = default)
        {
            return Task.FromResult(ToolResult.Succeeded($"Executed {Id}", TimeSpan.FromMilliseconds(10)));
        }
    }
}
