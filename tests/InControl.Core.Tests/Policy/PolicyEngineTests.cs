using InControl.Core.Policy;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for the PolicyEngine - deterministic policy evaluation.
/// </summary>
public class PolicyEngineTests
{
    #region Basic Engine Tests

    [Fact]
    public void PolicyEngine_StartsEmpty()
    {
        var engine = new PolicyEngine();
        var info = engine.GetEffectivePolicy();

        Assert.False(info.HasOrgPolicy);
        Assert.False(info.HasTeamPolicy);
        Assert.False(info.HasUserPolicy);
        Assert.Equal("Default policy", info.Summary);
    }

    [Fact]
    public void PolicyEngine_TracksLoadedPolicies()
    {
        var engine = new PolicyEngine();
        var orgPolicy = new PolicyDocument { Version = "1.0", Name = "Org Policy" };
        var userPolicy = new PolicyDocument { Version = "1.0", Name = "User Policy" };

        engine.SetPolicy(PolicySource.Organization, orgPolicy);
        engine.SetPolicy(PolicySource.User, userPolicy);

        var info = engine.GetEffectivePolicy();
        Assert.True(info.HasOrgPolicy);
        Assert.False(info.HasTeamPolicy);
        Assert.True(info.HasUserPolicy);
    }

    [Fact]
    public void PolicyEngine_ClearsPolicies()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument { Version = "1.0" });

        engine.ClearPolicies();

        var info = engine.GetEffectivePolicy();
        Assert.False(info.HasOrgPolicy);
    }

    #endregion

    #region Tool Evaluation Tests

    [Fact]
    public void EvaluateTool_DefaultAllows()
    {
        var engine = new PolicyEngine();

        var result = engine.EvaluateTool("any-tool");

        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Equal(PolicySource.Default, result.Source);
    }

    [Fact]
    public void EvaluateTool_RespectsExplicitAllow()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Allow = ["my-tool"]
            }
        });

        var result = engine.EvaluateTool("my-tool");

        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Equal(PolicySource.User, result.Source);
        Assert.Contains("explicitly allowed", result.Reason);
    }

    [Fact]
    public void EvaluateTool_RespectsExplicitDeny()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Deny = ["dangerous-tool"]
            }
        });

        var result = engine.EvaluateTool("dangerous-tool");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Equal(PolicySource.User, result.Source);
        Assert.Contains("blocked", result.Reason);
    }

    [Fact]
    public void EvaluateTool_RespectsRequireApproval()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                RequireApproval = ["web-search"]
            }
        });

        var result = engine.EvaluateTool("web-search");

        Assert.Equal(PolicyDecision.AllowWithApproval, result.Decision);
        Assert.Contains("requires approval", result.Reason);
    }

    [Fact]
    public void EvaluateTool_DenyTakesPrecedenceOverAllow()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Allow = ["tool"],
                Deny = ["tool"] // Same tool in both - deny wins
            }
        });

        var result = engine.EvaluateTool("tool");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public void EvaluateTool_OrgPolicyTakesPrecedence()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["tool"] }
        });
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Allow = ["tool"] }
        });

        var result = engine.EvaluateTool("tool");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Equal(PolicySource.Organization, result.Source);
    }

    [Fact]
    public void EvaluateTool_DetailedRuleWithConstraints()
    {
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
                        Id = "rate-limit-search",
                        Tool = "web-search",
                        Decision = PolicyDecision.AllowWithConstraints,
                        Reason = "Rate limited for cost control",
                        Constraints = new Dictionary<string, object> { ["max_per_hour"] = 50 }
                    }
                ]
            }
        });

        var result = engine.EvaluateTool("web-search");

        Assert.Equal(PolicyDecision.AllowWithConstraints, result.Decision);
        Assert.NotNull(result.Constraints);
        Assert.Equal(50, Convert.ToInt32(result.Constraints["max_per_hour"]));
    }

    [Fact]
    public void EvaluateTool_WildcardPatternMatches()
    {
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
                        Id = "deny-file-ops",
                        Tool = "file-*",
                        Decision = PolicyDecision.Deny,
                        Reason = "File operations restricted"
                    }
                ]
            }
        });

        var readResult = engine.EvaluateTool("file-read");
        var writeResult = engine.EvaluateTool("file-write");
        var otherResult = engine.EvaluateTool("web-search");

        Assert.Equal(PolicyDecision.Deny, readResult.Decision);
        Assert.Equal(PolicyDecision.Deny, writeResult.Decision);
        Assert.Equal(PolicyDecision.Allow, otherResult.Decision); // Default
    }

    [Fact]
    public void EvaluateTool_LockedOrgPolicyUsesDefault()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Locked = true,
            Tools = new ToolPolicyRules
            {
                Default = PolicyDecision.AllowWithApproval
            }
        });

        var result = engine.EvaluateTool("any-tool");

        Assert.Equal(PolicyDecision.AllowWithApproval, result.Decision);
        Assert.Equal(PolicySource.Organization, result.Source);
    }

    #endregion

    #region Plugin Evaluation Tests

    [Fact]
    public void EvaluatePlugin_DefaultRequiresApproval()
    {
        var engine = new PolicyEngine();

        var result = engine.EvaluatePlugin("com.example.plugin");

        Assert.Equal(PolicyDecision.AllowWithApproval, result.Decision);
        Assert.Equal(PolicySource.Default, result.Source);
    }

    [Fact]
    public void EvaluatePlugin_RespectsExplicitAllow()
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

        var result = engine.EvaluatePlugin("com.trusted.plugin");

        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void EvaluatePlugin_RespectsExplicitDeny()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Deny = ["com.malicious.*"]
            }
        });

        var result = engine.EvaluatePlugin("com.malicious.plugin");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public void EvaluatePlugin_RespectsTrustedAuthors()
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

        var result = engine.EvaluatePlugin("com.example.plugin", author: "Verified Publisher");

        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Contains("trusted", result.Reason);
    }

    [Fact]
    public void EvaluatePlugin_DisabledDeniesAll()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Enabled = false
            }
        });

        var result = engine.EvaluatePlugin("any-plugin");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Contains("disabled", result.Reason);
    }

    [Fact]
    public void EvaluatePlugin_RespectsMaxRiskLevel()
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

        var networkResult = engine.EvaluatePlugin("plugin", riskLevel: PluginRiskLevel.Network);
        var localResult = engine.EvaluatePlugin("plugin", riskLevel: PluginRiskLevel.LocalMutation);
        var readResult = engine.EvaluatePlugin("plugin", riskLevel: PluginRiskLevel.ReadOnly);

        Assert.Equal(PolicyDecision.Deny, networkResult.Decision);
        Assert.Equal(PolicyDecision.AllowWithApproval, localResult.Decision); // Default
        Assert.Equal(PolicyDecision.AllowWithApproval, readResult.Decision);
    }

    #endregion

    #region Memory Policy Tests

    [Fact]
    public void EvaluateMemoryPolicy_ReturnsDefaults()
    {
        var engine = new PolicyEngine();

        var result = engine.EvaluateMemoryPolicy();

        Assert.True(result.Enabled);
        Assert.Equal(0, result.MaxRetentionDays); // Unlimited
        Assert.Equal(10000, result.MaxMemories);
        Assert.True(result.EncryptAtRest);
        Assert.True(result.AutoFormation);
    }

    [Fact]
    public void EvaluateMemoryPolicy_RespectsOrgSettings()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules
            {
                MaxRetentionDays = 90,
                MaxMemories = 5000,
                AutoFormation = false,
                AllowExport = false
            }
        });

        var result = engine.EvaluateMemoryPolicy();

        Assert.Equal(90, result.MaxRetentionDays);
        Assert.Equal(5000, result.MaxMemories);
        Assert.False(result.AutoFormation);
        Assert.False(result.AllowExport);
    }

    [Fact]
    public void EvaluateMemoryPolicy_MergesExcludeCategories()
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
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules
            {
                ExcludeCategories = ["personal", "pii"] // Duplicate pii
            }
        });

        var result = engine.EvaluateMemoryPolicy();

        Assert.Equal(3, result.ExcludeCategories.Count); // credentials, pii, personal
        Assert.Contains("credentials", result.ExcludeCategories);
        Assert.Contains("personal", result.ExcludeCategories);
    }

    #endregion

    #region Connectivity Policy Tests

    [Fact]
    public void EvaluateConnectivityPolicy_ReturnsDefaults()
    {
        var engine = new PolicyEngine();

        var result = engine.EvaluateConnectivityPolicy();

        Assert.Null(result.AllowedModes);
        Assert.Equal("online", result.DefaultMode);
        Assert.True(result.AllowModeChange);
        Assert.True(result.AllowTelemetry);
    }

    [Fact]
    public void EvaluateConnectivityPolicy_RespectsRestrictions()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedModes = ["offline"],
                DefaultMode = "offline",
                AllowModeChange = false,
                AllowTelemetry = false
            }
        });

        var result = engine.EvaluateConnectivityPolicy();

        Assert.Single(result.AllowedModes!);
        Assert.Equal("offline", result.DefaultMode);
        Assert.False(result.AllowModeChange);
        Assert.False(result.AllowTelemetry);
    }

    [Fact]
    public void EvaluateConnectivityPolicy_MergesBlockedDomains()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["malware.com"]
            }
        });
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["distraction.com"]
            }
        });

        var result = engine.EvaluateConnectivityPolicy();

        Assert.Equal(2, result.BlockedDomains.Count);
        Assert.Contains("malware.com", result.BlockedDomains);
        Assert.Contains("distraction.com", result.BlockedDomains);
    }

    [Fact]
    public void EvaluateDomain_AllowsByDefault()
    {
        var engine = new PolicyEngine();

        var result = engine.EvaluateDomain("example.com");

        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void EvaluateDomain_RespectsBlockedDomains()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com"]
            }
        });

        var result = engine.EvaluateDomain("blocked.com");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public void EvaluateDomain_BlocksSubdomains()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com"]
            }
        });

        var result = engine.EvaluateDomain("sub.blocked.com");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    [Fact]
    public void EvaluateDomain_AllowedDomainsRestrictsAccess()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedDomains = ["internal.corp.com"]
            }
        });

        var internalResult = engine.EvaluateDomain("internal.corp.com");
        var externalResult = engine.EvaluateDomain("external.com");

        Assert.Equal(PolicyDecision.Allow, internalResult.Decision);
        Assert.Equal(PolicyDecision.Deny, externalResult.Decision);
    }

    #endregion

    #region Update Policy Tests

    [Fact]
    public void EvaluateUpdatePolicy_ReturnsDefaults()
    {
        var engine = new PolicyEngine();

        var result = engine.EvaluateUpdatePolicy();

        Assert.True(result.AutoUpdate);
        Assert.True(result.CheckOnStartup);
        Assert.Equal(0, result.DeferDays);
        Assert.Null(result.RequiredChannel);
    }

    [Fact]
    public void EvaluateUpdatePolicy_RespectsOrgSettings()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules
            {
                AutoUpdate = false,
                RequiredChannel = "stable",
                DeferDays = 14,
                MinimumVersion = "2.0.0"
            }
        });

        var result = engine.EvaluateUpdatePolicy();

        Assert.False(result.AutoUpdate);
        Assert.Equal("stable", result.RequiredChannel);
        Assert.Equal(14, result.DeferDays);
        Assert.Equal("2.0.0", result.MinimumVersion);
    }

    #endregion

    #region Audit Log Tests

    [Fact]
    public void PolicyEngine_LogsEvaluations()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Allow = ["tool1"] }
        });

        engine.EvaluateTool("tool1");
        engine.EvaluateTool("tool2");

        var log = engine.GetAuditLog();
        Assert.Equal(2, log.Count);
        Assert.Equal("tool2", log[0].Subject); // Most recent first
        Assert.Equal("tool1", log[1].Subject);
    }

    [Fact]
    public void PolicyEngine_RaisesEvaluationEvent()
    {
        var engine = new PolicyEngine();
        PolicyAuditEntry? capturedEntry = null;
        engine.PolicyEvaluated += (_, e) => capturedEntry = e;

        engine.EvaluateTool("test-tool");

        Assert.NotNull(capturedEntry);
        Assert.Equal("test-tool", capturedEntry.Subject);
        Assert.Equal(PolicyCategory.Tools, capturedEntry.Category);
    }

    [Fact]
    public void PolicyEngine_ClearsAuditLog()
    {
        var engine = new PolicyEngine();
        engine.EvaluateTool("tool");

        engine.ClearAuditLog();

        Assert.Empty(engine.GetAuditLog());
    }

    #endregion

    #region Precedence Tests

    [Fact]
    public void PolicyEngine_OrgOverridesTeam()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["tool"] }
        });
        engine.SetPolicy(PolicySource.Team, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Allow = ["tool"] }
        });

        var result = engine.EvaluateTool("tool");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Equal(PolicySource.Organization, result.Source);
    }

    [Fact]
    public void PolicyEngine_TeamOverridesUser()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Team, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["tool"] }
        });
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Allow = ["tool"] }
        });

        var result = engine.EvaluateTool("tool");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Equal(PolicySource.Team, result.Source);
    }

    [Fact]
    public void PolicyEngine_UserOverridesSession()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["tool"] }
        });
        engine.SetPolicy(PolicySource.Session, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Allow = ["tool"] }
        });

        var result = engine.EvaluateTool("tool");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Equal(PolicySource.User, result.Source);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void PolicyEngine_ComplexOrgPolicy()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Locked = true,
            Tools = new ToolPolicyRules
            {
                Default = PolicyDecision.AllowWithApproval,
                Allow = ["read-file"],
                Deny = ["execute-shell", "delete-*"],
                RequireApproval = ["web-search"]
            },
            Plugins = new PluginPolicyRules
            {
                Enabled = true,
                MaxRiskLevel = PluginRiskLevelPolicy.LocalMutation,
                TrustedAuthors = ["InControl Team"]
            },
            Memory = new MemoryPolicyRules
            {
                MaxRetentionDays = 90,
                AutoFormation = false
            },
            Connectivity = new ConnectivityPolicyRules
            {
                AllowModeChange = false,
                BlockedDomains = ["competitor.com"]
            }
        });

        // Tool evaluations
        Assert.Equal(PolicyDecision.Allow, engine.EvaluateTool("read-file").Decision);
        Assert.Equal(PolicyDecision.Deny, engine.EvaluateTool("execute-shell").Decision);
        Assert.Equal(PolicyDecision.Deny, engine.EvaluateTool("delete-file").Decision);
        Assert.Equal(PolicyDecision.AllowWithApproval, engine.EvaluateTool("web-search").Decision);
        Assert.Equal(PolicyDecision.AllowWithApproval, engine.EvaluateTool("unknown-tool").Decision); // Locked default

        // Plugin evaluations
        Assert.Equal(PolicyDecision.Deny, engine.EvaluatePlugin("plugin", riskLevel: PluginRiskLevel.Network).Decision);
        Assert.Equal(PolicyDecision.Allow, engine.EvaluatePlugin("plugin", author: "InControl Team").Decision);

        // Memory evaluation
        var memory = engine.EvaluateMemoryPolicy();
        Assert.Equal(90, memory.MaxRetentionDays);
        Assert.False(memory.AutoFormation);

        // Connectivity evaluation
        var connectivity = engine.EvaluateConnectivityPolicy();
        Assert.False(connectivity.AllowModeChange);
        Assert.Contains("competitor.com", connectivity.BlockedDomains);
    }

    #endregion
}
