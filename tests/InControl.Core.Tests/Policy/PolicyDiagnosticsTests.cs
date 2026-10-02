using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy diagnostics and explanation features.
/// </summary>
public class PolicyDiagnosticsTests
{
    #region Policy Summary Tests

    [Fact]
    public void GetPolicySummary_ReturnsEmptyForNoPolicy()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);

        var summary = diagnostics.GetPolicySummary();

        Assert.Empty(summary.ActiveSources);
        Assert.Equal(0, summary.ToolRuleCount);
        Assert.Equal(0, summary.PluginRuleCount);
    }

    [Fact]
    public void GetPolicySummary_IncludesActiveSources()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Description = "Org policy",
            Tools = new ToolPolicyRules { Deny = ["shell-*"] }
        });
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "2.0",
            Description = "User policy",
            Plugins = new PluginPolicyRules { TrustedAuthors = ["corp"] }
        });
        var diagnostics = new PolicyDiagnostics(engine);

        var summary = diagnostics.GetPolicySummary();

        Assert.Equal(2, summary.ActiveSources.Count);
        Assert.Contains(summary.ActiveSources, s => s.Source == PolicySource.Organization);
        Assert.Contains(summary.ActiveSources, s => s.Source == PolicySource.User);
    }

    #endregion

    #region Tool Explanation Tests

    [Fact]
    public void ExplainToolPolicy_DefaultAllow()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);

        var explanation = diagnostics.ExplainToolPolicy("any-tool");

        Assert.Equal(PolicyDecision.Allow, explanation.FinalDecision);
        Assert.Equal(PolicySource.Default, explanation.FinalSource);
        Assert.Empty(explanation.EvaluationSteps);
    }

    [Fact]
    public void ExplainToolPolicy_ShowsDenyStep()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { Deny = ["shell-*"] }
        });
        var diagnostics = new PolicyDiagnostics(engine);

        var explanation = diagnostics.ExplainToolPolicy("shell-execute");

        Assert.Equal(PolicyDecision.Deny, explanation.FinalDecision);
        Assert.Single(explanation.EvaluationSteps);
        var step = explanation.EvaluationSteps[0];
        Assert.Equal(PolicySource.Organization, step.Source);
        Assert.Equal("deny", step.RuleName);
        Assert.True(step.IsDecisive);
    }

    [Fact]
    public void ExplainToolPolicy_ShowsApprovalStep()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules { RequireApproval = ["web-*"] }
        });
        var diagnostics = new PolicyDiagnostics(engine);

        var explanation = diagnostics.ExplainToolPolicy("web-search");

        Assert.Equal(PolicyDecision.AllowWithApproval, explanation.FinalDecision);
        Assert.Single(explanation.EvaluationSteps);
        Assert.Equal("requireApproval", explanation.EvaluationSteps[0].RuleName);
    }

    [Fact]
    public void ExplainToolPolicy_ShowsCustomRuleWithConstraints()
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
                        Id = "limit-search",
                        Tool = "web-search",
                        Decision = PolicyDecision.AllowWithConstraints,
                        Constraints = new Dictionary<string, object> { ["max"] = 10 }
                    }
                ]
            }
        });
        var diagnostics = new PolicyDiagnostics(engine);

        var explanation = diagnostics.ExplainToolPolicy("web-search");

        Assert.Equal(PolicyDecision.AllowWithConstraints, explanation.FinalDecision);
        Assert.NotNull(explanation.Constraints);
    }

    #endregion

    #region Plugin Explanation Tests

    [Fact]
    public void ExplainPluginPolicy_DefaultAllow()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);

        var explanation = diagnostics.ExplainPluginPolicy("any-plugin");

        // Default for plugins is AllowWithApproval per the schema
        Assert.Equal(PolicySource.Default, explanation.FinalSource);
    }

    [Fact]
    public void ExplainPluginPolicy_ShowsTrustedAuthor()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { TrustedAuthors = ["corp-dev"] }
        });
        var diagnostics = new PolicyDiagnostics(engine);

        var explanation = diagnostics.ExplainPluginPolicy("my-plugin", author: "corp-dev");

        Assert.Equal(PolicyDecision.Allow, explanation.FinalDecision);
        Assert.Single(explanation.EvaluationSteps);
        Assert.Equal("trustedAuthor", explanation.EvaluationSteps[0].RuleName);
    }

    [Fact]
    public void ExplainPluginPolicy_ShowsDenyOverridesTrusted()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Deny = ["bad-plugin"],
                TrustedAuthors = ["any-author"]
            }
        });
        var diagnostics = new PolicyDiagnostics(engine);

        var explanation = diagnostics.ExplainPluginPolicy("bad-plugin", author: "any-author");

        Assert.Equal(PolicyDecision.Deny, explanation.FinalDecision);
        Assert.Equal("deny", explanation.EvaluationSteps[0].RuleName);
    }

    #endregion

    #region Tracing Tests

    [Fact]
    public void Tracing_DisabledByDefault()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);

        Assert.False(diagnostics.TracingEnabled);
    }

    [Fact]
    public void RecordEvaluation_IgnoredWhenTracingDisabled()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);
        diagnostics.TracingEnabled = false;

        diagnostics.RecordEvaluation(
            PolicyCategory.Tools,
            "test-tool",
            new PolicyEvaluationResult { Decision = PolicyDecision.Allow, Source = PolicySource.Default, Reason = "allowed" });

        Assert.Empty(diagnostics.GetRecentTraces());
    }

    [Fact]
    public void RecordEvaluation_RecordsWhenTracingEnabled()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);
        diagnostics.TracingEnabled = true;

        diagnostics.RecordEvaluation(
            PolicyCategory.Tools,
            "test-tool",
            new PolicyEvaluationResult { Decision = PolicyDecision.Deny, Source = PolicySource.Organization, Reason = "blocked" });

        var traces = diagnostics.GetRecentTraces();
        Assert.Single(traces);
        Assert.Equal("test-tool", traces[0].Subject);
        Assert.Equal(PolicyDecision.Deny, traces[0].Decision);
    }

    [Fact]
    public void GetTracesForSubject_FiltersCorrectly()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);
        diagnostics.TracingEnabled = true;

        diagnostics.RecordEvaluation(PolicyCategory.Tools, "tool-a",
            new PolicyEvaluationResult { Decision = PolicyDecision.Allow, Source = PolicySource.Default, Reason = "ok" });
        diagnostics.RecordEvaluation(PolicyCategory.Tools, "tool-b",
            new PolicyEvaluationResult { Decision = PolicyDecision.Deny, Source = PolicySource.User, Reason = "blocked" });
        diagnostics.RecordEvaluation(PolicyCategory.Tools, "tool-a",
            new PolicyEvaluationResult { Decision = PolicyDecision.Allow, Source = PolicySource.Default, Reason = "ok" });

        var tracesA = diagnostics.GetTracesForSubject("tool-a");
        Assert.Equal(2, tracesA.Count);

        var tracesB = diagnostics.GetTracesForSubject("tool-b");
        Assert.Single(tracesB);
    }

    [Fact]
    public void GetDeniedTraces_FiltersCorrectly()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);
        diagnostics.TracingEnabled = true;

        diagnostics.RecordEvaluation(PolicyCategory.Tools, "tool-a",
            new PolicyEvaluationResult { Decision = PolicyDecision.Allow, Source = PolicySource.Default, Reason = "ok" });
        diagnostics.RecordEvaluation(PolicyCategory.Tools, "tool-b",
            new PolicyEvaluationResult { Decision = PolicyDecision.Deny, Source = PolicySource.User, Reason = "blocked" });
        diagnostics.RecordEvaluation(PolicyCategory.Plugins, "plugin-x",
            new PolicyEvaluationResult { Decision = PolicyDecision.Deny, Source = PolicySource.Organization, Reason = "blocked" });

        var denied = diagnostics.GetDeniedTraces();
        Assert.Equal(2, denied.Count);
        Assert.All(denied, t => Assert.Equal(PolicyDecision.Deny, t.Decision));
    }

    [Fact]
    public void GetTraceStatistics_ReturnsCorrectCounts()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);
        diagnostics.TracingEnabled = true;

        diagnostics.RecordEvaluation(PolicyCategory.Tools, "t1",
            new PolicyEvaluationResult { Decision = PolicyDecision.Allow, Source = PolicySource.Default, Reason = "ok" });
        diagnostics.RecordEvaluation(PolicyCategory.Tools, "t2",
            new PolicyEvaluationResult { Decision = PolicyDecision.Deny, Source = PolicySource.User, Reason = "blocked" });
        diagnostics.RecordEvaluation(PolicyCategory.Plugins, "p1",
            new PolicyEvaluationResult { Decision = PolicyDecision.AllowWithApproval, Source = PolicySource.Organization, Reason = "approval" });

        var stats = diagnostics.GetTraceStatistics();

        Assert.Equal(3, stats.TotalEvaluations);
        Assert.Equal(1, stats.AllowCount);
        Assert.Equal(1, stats.DenyCount);
        Assert.Equal(1, stats.ApprovalRequiredCount);
        Assert.Equal(2, stats.ByCategory[PolicyCategory.Tools]);
        Assert.Equal(1, stats.ByCategory[PolicyCategory.Plugins]);
    }

    [Fact]
    public void Tracing_TrimsByMaxEntries()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);
        diagnostics.TracingEnabled = true;
        diagnostics.MaxTraceEntries = 5;

        for (int i = 0; i < 10; i++)
        {
            diagnostics.RecordEvaluation(PolicyCategory.Tools, $"tool-{i}",
                new PolicyEvaluationResult { Decision = PolicyDecision.Allow, Source = PolicySource.Default, Reason = "ok" });
        }

        var traces = diagnostics.GetRecentTraces(100);
        Assert.Equal(5, traces.Count);
        // Should have the last 5: tool-5 through tool-9
        Assert.Contains(traces, t => t.Subject == "tool-9");
        Assert.DoesNotContain(traces, t => t.Subject == "tool-0");
    }

    [Fact]
    public void ClearTraces_RemovesAll()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);
        diagnostics.TracingEnabled = true;

        diagnostics.RecordEvaluation(PolicyCategory.Tools, "test",
            new PolicyEvaluationResult { Decision = PolicyDecision.Allow, Source = PolicySource.Default, Reason = "ok" });
        Assert.Single(diagnostics.GetRecentTraces());

        diagnostics.ClearTraces();

        Assert.Empty(diagnostics.GetRecentTraces());
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateCurrentPolicy_ValidWithNoPolicy()
    {
        var engine = new PolicyEngine();
        var diagnostics = new PolicyDiagnostics(engine);

        var report = diagnostics.ValidateCurrentPolicy();

        Assert.True(report.IsValid);
    }

    [Fact]
    public void ValidateCurrentPolicy_SuggestsUnconfiguredSections()
    {
        var engine = new PolicyEngine();
        // No policies set, so MEMORY_NOT_CONFIGURED and UPDATES_NOT_CONFIGURED should be reported
        var diagnostics = new PolicyDiagnostics(engine);

        var report = diagnostics.ValidateCurrentPolicy();

        Assert.Contains(report.Issues, i => i.Code == "MEMORY_NOT_CONFIGURED");
        Assert.Contains(report.Issues, i => i.Code == "UPDATES_NOT_CONFIGURED");
    }

    #endregion
}
