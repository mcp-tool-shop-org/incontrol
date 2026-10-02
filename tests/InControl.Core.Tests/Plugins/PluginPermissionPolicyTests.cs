using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// Tests for PluginPermissionPolicy.
/// </summary>
public class PluginPermissionPolicyTests : IDisposable
{
    private readonly string _tempPath;
    private readonly string _policyPath;
    private readonly PluginPermissionPolicy _policy;

    public PluginPermissionPolicyTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"policy-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        _policyPath = Path.Combine(_tempPath, "plugin-policies.json");
        _policy = new PluginPermissionPolicy(_policyPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, true); } catch { }
        }
    }

    #region GetPolicy Tests

    [Fact]
    public void GetPolicy_ReturnsNullForUnknownPlugin()
    {
        var result = _policy.GetPolicy("unknown-plugin");
        Assert.Null(result);
    }

    [Fact]
    public void GetPolicy_ReturnsStoredPolicy()
    {
        // Arrange
        _policy.SetTrustLevel("test-plugin", PluginTrustLevel.Trusted);

        // Act
        var result = _policy.GetPolicy("test-plugin");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-plugin", result.PluginId);
        Assert.Equal(PluginTrustLevel.Trusted, result.TrustLevel);
    }

    #endregion

    #region SetTrustLevel Tests

    [Fact]
    public void SetTrustLevel_CreatesNewPolicy()
    {
        // Act
        _policy.SetTrustLevel("new-plugin", PluginTrustLevel.Trusted);

        // Assert
        var policy = _policy.GetPolicy("new-plugin");
        Assert.NotNull(policy);
        Assert.Equal(PluginTrustLevel.Trusted, policy.TrustLevel);
    }

    [Fact]
    public void SetTrustLevel_UpdatesExistingPolicy()
    {
        // Arrange
        _policy.SetTrustLevel("test-plugin", PluginTrustLevel.Default);

        // Act
        _policy.SetTrustLevel("test-plugin", PluginTrustLevel.Blocked);

        // Assert
        var policy = _policy.GetPolicy("test-plugin");
        Assert.NotNull(policy);
        Assert.Equal(PluginTrustLevel.Blocked, policy.TrustLevel);
    }

    [Fact]
    public void SetTrustLevel_RaisesEvent()
    {
        // Arrange
        PolicyUpdatedEventArgs? eventArgs = null;
        _policy.PolicyUpdated += (_, args) => eventArgs = args;

        // Act
        _policy.SetTrustLevel("test-plugin", PluginTrustLevel.Trusted);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal("test-plugin", eventArgs.PluginId);
        Assert.Equal(PolicyUpdateType.TrustLevel, eventArgs.UpdateType);
    }

    #endregion

    #region AddPermissionRule Tests

    [Fact]
    public void AddPermissionRule_AddsRuleToPolicy()
    {
        // Arrange
        var rule = new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow);

        // Act
        _policy.AddPermissionRule("test-plugin", rule);

        // Assert
        var policy = _policy.GetPolicy("test-plugin");
        Assert.NotNull(policy);
        Assert.Single(policy.PermissionRules);
        Assert.Equal(PermissionType.File, policy.PermissionRules[0].Type);
    }

    [Fact]
    public void AddPermissionRule_ReplacesExistingRuleWithSameKey()
    {
        // Arrange
        var rule1 = new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Deny);
        var rule2 = new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow);
        _policy.AddPermissionRule("test-plugin", rule1);

        // Act
        _policy.AddPermissionRule("test-plugin", rule2);

        // Assert
        var policy = _policy.GetPolicy("test-plugin");
        Assert.NotNull(policy);
        Assert.Single(policy.PermissionRules);
        Assert.Equal(PermissionDecision.Allow, policy.PermissionRules[0].Decision);
    }

    [Fact]
    public void AddPermissionRule_AllowsMultipleRulesForDifferentTypes()
    {
        // Arrange & Act
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow));
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.Network, PermissionAccess.Read, "https://api.example.com", PermissionDecision.Deny));

        // Assert
        var policy = _policy.GetPolicy("test-plugin");
        Assert.NotNull(policy);
        Assert.Equal(2, policy.PermissionRules.Count);
    }

    #endregion

    #region RemovePermissionRule Tests

    [Fact]
    public void RemovePermissionRule_RemovesRule()
    {
        // Arrange
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow));

        // Act
        _policy.RemovePermissionRule("test-plugin", PermissionType.File, PermissionAccess.Read, "/data");

        // Assert
        var policy = _policy.GetPolicy("test-plugin");
        Assert.NotNull(policy);
        Assert.Empty(policy.PermissionRules);
    }

    [Fact]
    public void RemovePermissionRule_DoesNothingForUnknownPlugin()
    {
        // Act - should not throw
        _policy.RemovePermissionRule("unknown", PermissionType.File, PermissionAccess.Read, null);
    }

    #endregion

    #region CheckPermission Tests

    [Fact]
    public void CheckPermission_ReturnsNotConfiguredForUnknownPlugin()
    {
        var result = _policy.CheckPermission("unknown", PermissionType.File, PermissionAccess.Read);
        Assert.False(result.IsConfigured);
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void CheckPermission_ReturnsDeniedForBlockedPlugin()
    {
        // Arrange
        _policy.SetTrustLevel("blocked-plugin", PluginTrustLevel.Blocked);

        // Act
        var result = _policy.CheckPermission("blocked-plugin", PermissionType.File, PermissionAccess.Read);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.NotNull(result.DenialReason);
        Assert.Contains("blocked", result.DenialReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckPermission_ReturnsAllowedWhenRuleAllows()
    {
        // Arrange
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow));

        // Act
        var result = _policy.CheckPermission("test-plugin", PermissionType.File, PermissionAccess.Read, "/data/file.txt");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.False(result.RequiresOperatorConsent);
    }

    [Fact]
    public void CheckPermission_ReturnsDeniedWhenRuleDenies()
    {
        // Arrange
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Deny));

        // Act
        var result = _policy.CheckPermission("test-plugin", PermissionType.File, PermissionAccess.Read, "/data/file.txt");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.NotNull(result.DenialReason);
    }

    [Fact]
    public void CheckPermission_ReturnsRequiresConsentWhenRuleAsks()
    {
        // Arrange
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.AskOnce));

        // Act
        var result = _policy.CheckPermission("test-plugin", PermissionType.File, PermissionAccess.Read, "/data/file.txt");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.True(result.RequiresOperatorConsent);
    }

    [Fact]
    public void CheckPermission_ScopeMatchingIsPrefix()
    {
        // Arrange
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow));

        // Act - scope is a subpath
        var result = _policy.CheckPermission("test-plugin", PermissionType.File, PermissionAccess.Read, "/data/subdir/file.txt");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckPermission_ScopeMismatchReturnsNotConfigured()
    {
        // Arrange
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow));

        // Act - different scope
        var result = _policy.CheckPermission("test-plugin", PermissionType.File, PermissionAccess.Read, "/other/file.txt");

        // Assert
        Assert.False(result.IsConfigured);
    }

    #endregion

    #region RequestPermission Tests

    [Fact]
    public async Task RequestPermissionAsync_RaisesEventAndWaitsForResponse()
    {
        // Arrange
        PermissionRequestEventArgs? receivedArgs = null;
        _policy.PermissionRequested += (_, args) =>
        {
            receivedArgs = args;
            // Simulate UI granting the permission
            args.SetResult(new PermissionRequestResult(true, false));
        };

        // Act
        var result = await _policy.RequestPermissionAsync(
            "test-plugin",
            PermissionType.Network,
            PermissionAccess.Read,
            "https://api.example.com",
            "Need to fetch data");

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal("test-plugin", receivedArgs.Request.PluginId);
        Assert.True(result.Granted);
    }

    [Fact]
    public async Task RequestPermissionAsync_RecordsDecisionWhenRememberIsTrue()
    {
        // Arrange
        _policy.PermissionRequested += (_, args) =>
        {
            args.SetResult(new PermissionRequestResult(true, true)); // Remember this
        };

        // Act
        await _policy.RequestPermissionAsync(
            "test-plugin",
            PermissionType.File,
            PermissionAccess.Read,
            "/data",
            "Need to read config");

        // Assert - should have added a permission rule
        var policy = _policy.GetPolicy("test-plugin");
        Assert.NotNull(policy);
        Assert.Contains(policy.PermissionRules, r =>
            r.Type == PermissionType.File &&
            r.Access == PermissionAccess.Read &&
            r.Decision == PermissionDecision.Allow);
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsNotGrantedOnCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _policy.PermissionRequested += (_, args) =>
        {
            // Don't respond, let it be cancelled
            cts.Cancel();
        };

        // Act
        var result = await _policy.RequestPermissionAsync(
            "test-plugin",
            PermissionType.File,
            PermissionAccess.Read,
            null,
            "Test",
            cts.Token);

        // Assert
        Assert.False(result.Granted);
    }

    #endregion

    #region ClearPolicy Tests

    [Fact]
    public void ClearPolicy_RemovesPolicy()
    {
        // Arrange
        _policy.SetTrustLevel("test-plugin", PluginTrustLevel.Trusted);
        _policy.AddPermissionRule("test-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, null, PermissionDecision.Allow));

        // Act
        _policy.ClearPolicy("test-plugin");

        // Assert
        Assert.Null(_policy.GetPolicy("test-plugin"));
    }

    [Fact]
    public void ClearPolicy_RaisesEvent()
    {
        // Arrange
        _policy.SetTrustLevel("test-plugin", PluginTrustLevel.Trusted);
        PolicyUpdatedEventArgs? eventArgs = null;
        _policy.PolicyUpdated += (_, args) => eventArgs = args;

        // Act
        _policy.ClearPolicy("test-plugin");

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(PolicyUpdateType.Cleared, eventArgs.UpdateType);
    }

    #endregion

    #region Persistence Tests

    [Fact]
    public void Policies_PersistAcrossInstances()
    {
        // Arrange
        _policy.SetTrustLevel("persistent-plugin", PluginTrustLevel.Trusted);
        _policy.AddPermissionRule("persistent-plugin",
            new PermissionRule(PermissionType.File, PermissionAccess.Read, "/data", PermissionDecision.Allow));

        // Act - create new instance
        var newPolicy = new PluginPermissionPolicy(_policyPath);

        // Assert
        var loaded = newPolicy.GetPolicy("persistent-plugin");
        Assert.NotNull(loaded);
        Assert.Equal(PluginTrustLevel.Trusted, loaded.TrustLevel);
        Assert.Single(loaded.PermissionRules);
    }

    [Fact]
    public void GetAllPolicies_ReturnsAllStoredPolicies()
    {
        // Arrange
        _policy.SetTrustLevel("plugin-1", PluginTrustLevel.Default);
        _policy.SetTrustLevel("plugin-2", PluginTrustLevel.Trusted);
        _policy.SetTrustLevel("plugin-3", PluginTrustLevel.Blocked);

        // Act
        var all = _policy.GetAllPolicies();

        // Assert
        Assert.Equal(3, all.Count);
        Assert.Contains(all, p => p.PluginId == "plugin-1");
        Assert.Contains(all, p => p.PluginId == "plugin-2");
        Assert.Contains(all, p => p.PluginId == "plugin-3");
    }

    #endregion
}
