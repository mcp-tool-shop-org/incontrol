using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy types and evaluation results.
/// </summary>
public class PolicyTypesTests
{
    #region PolicyEvaluationResult Tests

    [Fact]
    public void Allow_CreatesCorrectResult()
    {
        var result = PolicyEvaluationResult.Allow("Tool is permitted", PolicySource.User, "/path/to/policy.json");

        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Equal("Tool is permitted", result.Reason);
        Assert.Equal(PolicySource.User, result.Source);
        Assert.Equal("/path/to/policy.json", result.SourcePath);
        Assert.Null(result.Constraints);
    }

    [Fact]
    public void Deny_CreatesCorrectResult()
    {
        var result = PolicyEvaluationResult.Deny(
            "Tool blocked by organization policy",
            PolicySource.Organization,
            "/etc/incontrol/policy.json",
            "tool.deny.internet-search");

        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Equal("Tool blocked by organization policy", result.Reason);
        Assert.Equal(PolicySource.Organization, result.Source);
        Assert.Equal("tool.deny.internet-search", result.RuleId);
    }

    [Fact]
    public void RequireApproval_CreatesCorrectResult()
    {
        var result = PolicyEvaluationResult.RequireApproval(
            "This tool requires approval before use",
            PolicySource.Team);

        Assert.Equal(PolicyDecision.AllowWithApproval, result.Decision);
        Assert.Equal("This tool requires approval before use", result.Reason);
    }

    [Fact]
    public void AllowConstrained_CreatesCorrectResult()
    {
        var constraints = new Dictionary<string, object>
        {
            ["max_requests_per_minute"] = 10,
            ["allowed_domains"] = new[] { "weather.com", "api.weather.gov" }
        };

        var result = PolicyEvaluationResult.AllowConstrained(
            "Tool permitted with rate limits",
            PolicySource.Organization,
            constraints);

        Assert.Equal(PolicyDecision.AllowWithConstraints, result.Decision);
        Assert.NotNull(result.Constraints);
        Assert.Equal(10, result.Constraints["max_requests_per_minute"]);
    }

    [Fact]
    public void EvaluationResult_IncludesTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var result = PolicyEvaluationResult.Allow("Test", PolicySource.Default);
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(result.EvaluatedAt, before, after);
    }

    #endregion

    #region PolicySource Precedence Tests

    [Fact]
    public void PolicySource_OrgHasHighestPrecedence()
    {
        Assert.True(PolicySource.Organization < PolicySource.Team);
        Assert.True(PolicySource.Team < PolicySource.User);
        Assert.True(PolicySource.User < PolicySource.Session);
        Assert.True(PolicySource.Session < PolicySource.Default);
    }

    #endregion

    #region PolicyAuditEntry Tests

    [Fact]
    public void PolicyAuditEntry_CapturesAllFields()
    {
        var entry = new PolicyAuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = PolicyCategory.Tools,
            Subject = "internet-search",
            Action = "execute",
            Decision = PolicyDecision.Deny,
            Reason = "Blocked by organization policy",
            Source = PolicySource.Organization,
            SourcePath = "/etc/incontrol/policy.json",
            RuleId = "tools.deny.internet"
        };

        Assert.Equal(PolicyCategory.Tools, entry.Category);
        Assert.Equal("internet-search", entry.Subject);
        Assert.Equal("execute", entry.Action);
        Assert.Equal(PolicyDecision.Deny, entry.Decision);
        Assert.NotNull(entry.SourcePath);
        Assert.NotNull(entry.RuleId);
    }

    #endregion

    #region EffectivePolicyInfo Tests

    [Fact]
    public void EffectivePolicyInfo_Summary_ShowsOrgLocked()
    {
        var info = new EffectivePolicyInfo
        {
            HasOrgPolicy = true,
            IsLocked = true,
            OrgPolicyPath = "/path/policy.json",
            LoadedAt = DateTimeOffset.UtcNow
        };

        Assert.Contains("Managed by Organization", info.Summary);
        Assert.Contains("locked", info.Summary);
    }

    [Fact]
    public void EffectivePolicyInfo_Summary_ShowsOrgUnlocked()
    {
        var info = new EffectivePolicyInfo
        {
            HasOrgPolicy = true,
            IsLocked = false,
            LoadedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("Organization policy active", info.Summary);
    }

    [Fact]
    public void EffectivePolicyInfo_Summary_ShowsTeam()
    {
        var info = new EffectivePolicyInfo
        {
            HasOrgPolicy = false,
            HasTeamPolicy = true,
            LoadedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("Team policy active", info.Summary);
    }

    [Fact]
    public void EffectivePolicyInfo_Summary_ShowsUser()
    {
        var info = new EffectivePolicyInfo
        {
            HasOrgPolicy = false,
            HasTeamPolicy = false,
            HasUserPolicy = true,
            LoadedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("User policy active", info.Summary);
    }

    [Fact]
    public void EffectivePolicyInfo_Summary_ShowsDefault()
    {
        var info = new EffectivePolicyInfo
        {
            HasOrgPolicy = false,
            HasTeamPolicy = false,
            HasUserPolicy = false,
            LoadedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("Default policy", info.Summary);
    }

    #endregion

    #region PolicyPaths Tests

    [Fact]
    public void PolicyPaths_OrgPath_IsAbsolute()
    {
        var path = PolicyPaths.GetOrgPolicyPath();

        Assert.True(Path.IsPathRooted(path));
        Assert.Contains("policy.json", path);
    }

    [Fact]
    public void PolicyPaths_UserPath_IsAbsolute()
    {
        var path = PolicyPaths.GetUserPolicyPath();

        Assert.True(Path.IsPathRooted(path));
        Assert.Contains("user-policy.json", path);
    }

    [Fact]
    public void PolicyPaths_TeamPath_IsAbsolute()
    {
        var path = PolicyPaths.GetTeamPolicyPath();

        Assert.True(Path.IsPathRooted(path));
        Assert.Contains("team-policy.json", path);
    }

    [Fact]
    public void PolicyPaths_AllPathsAreDifferent()
    {
        var orgPath = PolicyPaths.GetOrgPolicyPath();
        var teamPath = PolicyPaths.GetTeamPolicyPath();
        var userPath = PolicyPaths.GetUserPolicyPath();

        Assert.NotEqual(orgPath, teamPath);
        Assert.NotEqual(teamPath, userPath);
        Assert.NotEqual(orgPath, userPath);
    }

    #endregion

    #region PolicyCategory Tests

    [Fact]
    public void PolicyCategory_AllCategoriesDefined()
    {
        var categories = Enum.GetValues<PolicyCategory>();

        Assert.Contains(PolicyCategory.Tools, categories);
        Assert.Contains(PolicyCategory.Plugins, categories);
        Assert.Contains(PolicyCategory.Memory, categories);
        Assert.Contains(PolicyCategory.Connectivity, categories);
        Assert.Contains(PolicyCategory.Updates, categories);
    }

    #endregion
}
