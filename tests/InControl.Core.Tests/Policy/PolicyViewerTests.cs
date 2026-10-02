using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy viewer admin UX features.
/// </summary>
public class PolicyViewerTests
{
    #region Status View Tests

    [Fact]
    public void GetStatusView_ReturnsHealthyWithNoPolicy()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var status = viewer.GetStatusView();

        Assert.True(status.IsHealthy);
        Assert.Equal(0, status.ActiveSourceCount);
        Assert.Equal(0, status.TotalRuleCount);
    }

    [Fact]
    public void GetStatusView_CountsSourcesAndRules()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["a", "b"] }
        });
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { Deny = ["x"] }
        });
        var viewer = new PolicyViewer(engine);

        var status = viewer.GetStatusView();

        Assert.Equal(2, status.ActiveSourceCount);
    }

    #endregion

    #region Source Views Tests

    [Fact]
    public void GetSourceViews_ShowsAllSources()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var views = viewer.GetSourceViews();

        Assert.Equal(4, views.Count); // Org, Team, User, Session
    }

    [Fact]
    public void GetSourceViews_IdentifiesActiveSources()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Description = "Org policy"
        });
        var viewer = new PolicyViewer(engine);

        var views = viewer.GetSourceViews();

        var orgView = views.First(v => v.Source == PolicySource.Organization);
        Assert.True(orgView.IsActive);

        var userView = views.First(v => v.Source == PolicySource.User);
        Assert.False(userView.IsActive);
        Assert.Null(userView.Version);
    }

    #endregion

    #region Tool Views Tests

    [Fact]
    public void GetToolView_ReturnsAllowedByDefault()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetToolView("any-tool");

        Assert.Equal(PolicyDecision.Allow, view.Decision);
        Assert.True(view.CanExecute);
        Assert.False(view.RequiresApproval);
        Assert.Equal("default allow", view.EvaluationPath);
    }

    [Fact]
    public void GetToolView_ShowsDenied()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["shell-*"] }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetToolView("shell-exec");

        Assert.Equal(PolicyDecision.Deny, view.Decision);
        Assert.False(view.CanExecute);
        Assert.Contains("Organization", view.EvaluationPath);
        Assert.Contains("deny", view.EvaluationPath);
    }

    [Fact]
    public void GetToolView_ShowsApprovalRequired()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-*"] }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetToolView("web-search");

        Assert.True(view.RequiresApproval);
        Assert.False(view.CanExecute);
    }

    [Fact]
    public void GetToolView_ShowsConstraints()
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
                        Id = "limit",
                        Tool = "web-search",
                        Decision = PolicyDecision.AllowWithConstraints,
                        Constraints = new Dictionary<string, object> { ["max"] = 10, ["timeout"] = 30 }
                    }
                ]
            }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetToolView("web-search");

        Assert.True(view.HasConstraints);
        Assert.NotNull(view.ConstraintSummary);
        Assert.Contains("max=10", view.ConstraintSummary);
    }

    [Fact]
    public void GetToolViews_ReturnsMultiple()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["shell-*"] }
        });
        var viewer = new PolicyViewer(engine);

        var views = viewer.GetToolViews(["shell-exec", "read-file", "web-search"]);

        Assert.Equal(3, views.Count);
        Assert.False(views.First(v => v.ToolId == "shell-exec").CanExecute);
        Assert.True(views.First(v => v.ToolId == "read-file").CanExecute);
    }

    #endregion

    #region Plugin Views Tests

    [Fact]
    public void GetPluginView_ReturnsDefaultBehavior()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetPluginView("any-plugin");

        // Default plugin behavior per schema is AllowWithApproval
        Assert.Equal(PolicySource.Default, view.Source);
    }

    [Fact]
    public void GetPluginView_ShowsDenied()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { Deny = ["bad-*"] }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetPluginView("bad-plugin");

        Assert.True(view.IsDenied);
        Assert.False(view.CanLoad);
    }

    [Fact]
    public void GetPluginView_ShowsTrustedAuthor()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { TrustedAuthors = ["corp"] }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetPluginView("my-plugin", author: "corp");

        Assert.True(view.CanLoad);
        Assert.Contains("trustedAuthor", view.EvaluationPath);
    }

    #endregion

    #region Memory Views Tests

    [Fact]
    public void GetMemoryView_DefaultValues()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetMemoryView();

        Assert.True(view.IsAllowed);
    }

    [Fact]
    public void GetMemoryView_ShowsRestrictions()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules
            {
                MaxMemories = 500,
                MaxRetentionDays = 60,
                ExcludeCategories = ["secrets", "pii"]
            }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetMemoryView();

        Assert.Equal(500, view.MaxEntries);
        Assert.Equal(60, view.RetentionDays);
        Assert.Equal(2, view.ExcludedCategories.Count);
        Assert.Contains("500", view.Summary);
        Assert.Contains("60", view.Summary);
    }

    [Fact]
    public void GetMemoryView_ShowsDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules { Enabled = false }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetMemoryView();

        Assert.False(view.IsAllowed);
        Assert.Contains("disabled", view.Summary);
    }

    #endregion

    #region Connectivity Views Tests

    [Fact]
    public void GetConnectivityView_DefaultValues()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetConnectivityView();

        Assert.True(view.AllowTelemetry);
    }

    [Fact]
    public void GetConnectivityView_ShowsRestrictions()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedModes = ["connected", "local"],
                BlockedDomains = ["blocked.com", "*.blocked.net"],
                AllowTelemetry = false
            }
        });
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetConnectivityView();

        Assert.Equal(2, view.AllowedModes.Count);
        Assert.Equal(2, view.BlockedDomainCount);
        Assert.False(view.AllowTelemetry);
        Assert.Contains("telemetry blocked", view.Summary);
    }

    [Fact]
    public void CheckDomain_ReturnsAllowedByDefault()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var check = viewer.CheckDomain("example.com");

        Assert.True(check.IsAllowed);
        Assert.Equal(PolicyDecision.Allow, check.Decision);
    }

    [Fact]
    public void CheckDomain_ShowsBlocked()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com"] // Blocks blocked.com and *.blocked.com
            }
        });
        var viewer = new PolicyViewer(engine);

        var check = viewer.CheckDomain("api.blocked.com");

        Assert.False(check.IsAllowed);
        Assert.Equal(PolicyDecision.Deny, check.Decision);
    }

    #endregion

    #region Update Views Tests

    [Fact]
    public void GetUpdateView_DefaultValues()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetUpdateView();

        Assert.True(view.AutoUpdateEnabled);
        Assert.True(view.CheckOnStartup);
        Assert.Null(view.RequiredChannel);
    }

    [Fact]
    public void GetUpdateView_ShowsRestrictions()
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
        var viewer = new PolicyViewer(engine);

        var view = viewer.GetUpdateView();

        Assert.False(view.AutoUpdateEnabled);
        Assert.Equal("stable", view.RequiredChannel);
        Assert.Equal(14, view.DeferDays);
        Assert.Equal("2.0.0", view.MinimumVersion);
        Assert.Contains("auto-update disabled", view.Summary);
        Assert.Contains("stable", view.Summary);
    }

    #endregion

    #region Export Tests

    [Fact]
    public void ExportAsText_ContainsAllSections()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Description = "Test policy",
            Tools = new ToolPolicyRules { Deny = ["shell-*"] },
            Plugins = new PluginPolicyRules { TrustedAuthors = ["corp"] },
            Memory = new MemoryPolicyRules { MaxMemories = 100 },
            Connectivity = new ConnectivityPolicyRules { AllowTelemetry = false },
            Updates = new UpdatePolicyRules { RequiredChannel = "stable" }
        });
        var viewer = new PolicyViewer(engine);

        var text = viewer.ExportAsText();

        Assert.Contains("Policy Configuration Report", text);
        Assert.Contains("Active Policy Sources", text);
        Assert.Contains("Memory Policy", text);
        Assert.Contains("Connectivity Policy", text);
        Assert.Contains("Update Policy", text);
        Assert.Contains("Validation", text);
    }

    [Fact]
    public void ExportAsJson_ReturnsValidJson()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["test"] }
        });
        var viewer = new PolicyViewer(engine);

        var json = viewer.ExportAsJson();

        // Should be valid JSON (contains expected structure)
        Assert.Contains("HasOrgPolicy", json);
        Assert.Contains("HasUserPolicy", json);
    }

    #endregion

    #region Diagnostics Integration Tests

    [Fact]
    public void Diagnostics_IsAccessible()
    {
        var engine = new PolicyEngine();
        var viewer = new PolicyViewer(engine);

        Assert.NotNull(viewer.Diagnostics);
    }

    [Fact]
    public void Diagnostics_SharesEngine()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["test"] }
        });
        var viewer = new PolicyViewer(engine);

        // Diagnostics should see the same policy
        var explanation = viewer.Diagnostics.ExplainToolPolicy("test");
        Assert.Equal(PolicyDecision.Deny, explanation.FinalDecision);
    }

    #endregion
}
