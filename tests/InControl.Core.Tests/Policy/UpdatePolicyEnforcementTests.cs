using InControl.Core.Policy;
using InControl.Core.Updates;
using Xunit;
using SysVersion = System.Version;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy-governed update operations.
/// </summary>
public class UpdatePolicyEnforcementTests
{
    private static PolicyGovernedUpdateManager CreateGovernedUpdate(PolicyEngine? engine = null, string channel = "stable")
    {
        var checker = new TestUpdateChecker();
        var installer = new TestUpdateInstaller();
        var tempPath = Path.Combine(Path.GetTempPath(), $"update-test-{Guid.NewGuid()}.json");
        var manager = new UpdateManager(checker, installer, tempPath);
        var policyEngine = engine ?? new PolicyEngine();
        return manager.WithPolicyEnforcement(policyEngine, channel);
    }

    #region Basic Policy Tests

    [Fact]
    public void CheckUpdatePolicy_AllowsAutoUpdateByDefault()
    {
        var governed = CreateGovernedUpdate();

        var status = governed.CheckUpdatePolicy();

        Assert.True(status.CanAutoUpdate);
        Assert.True(status.CanCheckOnStartup);
    }

    [Fact]
    public void CheckUpdatePolicy_RespectsAutoUpdateDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { AutoUpdate = false }
        });
        var governed = CreateGovernedUpdate(engine);

        var status = governed.CheckUpdatePolicy();

        Assert.False(status.CanAutoUpdate);
        Assert.Contains("disabled", status.Reason);
    }

    [Fact]
    public void CheckUpdatePolicy_RespectsCheckOnStartup()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { CheckOnStartup = false }
        });
        var governed = CreateGovernedUpdate(engine);

        var status = governed.CheckUpdatePolicy();

        Assert.False(status.CanCheckOnStartup);
    }

    #endregion

    #region Channel Tests

    [Fact]
    public void IsChannelAllowed_TrueByDefault()
    {
        var governed = CreateGovernedUpdate();

        Assert.True(governed.IsChannelAllowed("stable"));
        Assert.True(governed.IsChannelAllowed("beta"));
        Assert.True(governed.IsChannelAllowed("dev"));
    }

    [Fact]
    public void IsChannelAllowed_RespectsRequiredChannel()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { RequiredChannel = "stable" }
        });
        var governed = CreateGovernedUpdate(engine);

        Assert.True(governed.IsChannelAllowed("stable"));
        Assert.False(governed.IsChannelAllowed("beta"));
        Assert.False(governed.IsChannelAllowed("dev"));
    }

    [Fact]
    public void IsChannelAllowed_RespectsAllowedChannels()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules
            {
                AllowedChannels = ["stable", "beta"]
            }
        });
        var governed = CreateGovernedUpdate(engine);

        Assert.True(governed.IsChannelAllowed("stable"));
        Assert.True(governed.IsChannelAllowed("beta"));
        Assert.False(governed.IsChannelAllowed("dev"));
        Assert.False(governed.IsChannelAllowed("canary"));
    }

    [Fact]
    public void CheckUpdatePolicy_ReportsChannelStatus()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { RequiredChannel = "stable" }
        });
        var governed = CreateGovernedUpdate(engine, "beta"); // On wrong channel

        var status = governed.CheckUpdatePolicy();

        Assert.False(status.IsChannelAllowed);
        Assert.Equal("stable", status.RequiredChannel);
        Assert.Contains("not allowed", status.Reason);
    }

    #endregion

    #region Deferral Tests

    [Fact]
    public void CheckDeferral_NoDeferralByDefault()
    {
        var governed = CreateGovernedUpdate();
        var update = CreateTestUpdate(DateTimeOffset.UtcNow.AddDays(-5));

        var result = governed.CheckDeferral(update);

        Assert.False(result.ShouldDefer);
    }

    [Fact]
    public void CheckDeferral_DefersWhenPolicySet()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { DeferDays = 14 }
        });
        var governed = CreateGovernedUpdate(engine);
        var update = CreateTestUpdate(DateTimeOffset.UtcNow.AddDays(-5)); // 5 days old

        var result = governed.CheckDeferral(update);

        Assert.True(result.ShouldDefer);
        Assert.Equal(9, result.DaysRemaining); // 14 - 5 = 9
    }

    [Fact]
    public void CheckDeferral_AllowsAfterDeferralPeriod()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { DeferDays = 7 }
        });
        var governed = CreateGovernedUpdate(engine);
        var update = CreateTestUpdate(DateTimeOffset.UtcNow.AddDays(-10)); // 10 days old

        var result = governed.CheckDeferral(update);

        Assert.False(result.ShouldDefer);
    }

    #endregion

    #region Minimum Version Tests

    [Fact]
    public void MeetsMinimumVersion_TrueByDefault()
    {
        var governed = CreateGovernedUpdate();

        Assert.True(governed.MeetsMinimumVersion());
    }

    [Fact]
    public void MeetsMinimumVersion_ChecksAgainstPolicy()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { MinimumVersion = "2.0.0" }
        });
        var governed = CreateGovernedUpdate(engine);

        // Default version is 1.0.0
        Assert.False(governed.MeetsMinimumVersion());
    }

    [Fact]
    public void GetComplianceInfo_ReportsBelowMinimum()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { MinimumVersion = "5.0.0" }
        });
        var governed = CreateGovernedUpdate(engine);

        var compliance = governed.GetComplianceInfo();

        Assert.Equal(ComplianceStatus.BelowMinimum, compliance.Status);
        Assert.Contains("below minimum", compliance.Message);
    }

    [Fact]
    public void GetComplianceInfo_ReportsCompliant()
    {
        var governed = CreateGovernedUpdate();

        var compliance = governed.GetComplianceInfo();

        Assert.Equal(ComplianceStatus.Compliant, compliance.Status);
        Assert.Null(compliance.Message);
    }

    #endregion

    #region Mode Tests

    [Fact]
    public void SetMode_SucceedsWhenAllowed()
    {
        var governed = CreateGovernedUpdate();

        var result = governed.SetMode(UpdateMode.NotifyOnly);

        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateMode.NotifyOnly, governed.Mode);
    }

    [Fact]
    public void SetMode_BlocksAutoUpdateWhenDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { AutoUpdate = false }
        });
        var governed = CreateGovernedUpdate(engine);

        var result = governed.SetMode(UpdateMode.AutoInstall);

        Assert.False(result.IsSuccess);
        Assert.True(result.WasBlocked);
        Assert.Contains("disabled", result.BlockReason);
    }

    [Fact]
    public void SetMode_AllowsManualWhenAutoDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { AutoUpdate = false }
        });
        var governed = CreateGovernedUpdate(engine);

        var result = governed.SetMode(UpdateMode.NotifyOnly);

        Assert.True(result.IsSuccess);
    }

    #endregion

    #region Check Tests

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNoUpdate()
    {
        var governed = CreateGovernedUpdate();

        var result = await governed.CheckForUpdateAsync();

        Assert.False(result.HasUpdate);
    }

    [Fact]
    public async Task CheckForUpdateAsync_BlocksWrongChannel()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { RequiredChannel = "stable" }
        });
        var governed = CreateGovernedUpdate(engine, "beta");

        var result = await governed.CheckForUpdateAsync();

        Assert.True(result.WasChannelBlocked);
        Assert.Equal("beta", result.BlockedChannel);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void SetMode_RaisesAutoUpdateBlockedEvent()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { AutoUpdate = false }
        });
        var governed = CreateGovernedUpdate(engine);

        AutoUpdateBlockedEventArgs? capturedArgs = null;
        governed.AutoUpdateBlocked += (_, args) => capturedArgs = args;

        governed.SetMode(UpdateMode.AutoInstall);

        Assert.NotNull(capturedArgs);
        Assert.Equal(UpdateMode.AutoInstall, capturedArgs.RequestedMode);
    }

    [Fact]
    public async Task CheckForUpdateAsync_RaisesChannelBlockedEvent()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules { RequiredChannel = "stable" }
        });
        var governed = CreateGovernedUpdate(engine, "dev");

        ChannelBlockedEventArgs? capturedArgs = null;
        governed.ChannelBlocked += (_, args) => capturedArgs = args;

        await governed.CheckForUpdateAsync();

        Assert.NotNull(capturedArgs);
        Assert.Equal("dev", capturedArgs.CurrentChannel);
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
            Updates = new UpdatePolicyRules
            {
                RequiredChannel = "stable",
                DeferDays = 7,
                MinimumVersion = "1.0.0"
            }
        });
        var governed = CreateGovernedUpdate(engine);

        var stats = governed.GetStatistics();

        Assert.Equal("stable", stats.RequiredChannel);
        Assert.True(stats.IsChannelAllowed);
        Assert.Equal(7, stats.DeferDays);
        Assert.True(stats.MeetsMinimumVersion);
    }

    #endregion

    #region Extension Tests

    [Fact]
    public void WithPolicyEnforcement_CreatesWrapper()
    {
        var checker = new TestUpdateChecker();
        var installer = new TestUpdateInstaller();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json");
        var manager = new UpdateManager(checker, installer, tempPath);
        var engine = new PolicyEngine();

        var governed = manager.WithPolicyEnforcement(engine, "beta");

        Assert.NotNull(governed);
        Assert.Same(engine, governed.PolicyEngine);
        Assert.Same(manager, governed.InnerManager);
        Assert.Equal("beta", governed.CurrentChannel);
    }

    #endregion

    #region Helpers

    private static UpdateInfo CreateTestUpdate(DateTimeOffset releasedAt)
    {
        return new UpdateInfo(
            Version: new SysVersion(2, 0, 0),
            Title: "Test Update",
            Description: "Test description",
            ChangelogUrl: "https://example.com/changelog",
            DownloadUrl: "https://example.com/download",
            SizeBytes: 1000000,
            Checksum: "abc123",
            ReleasedAt: releasedAt,
            IsCritical: false,
            IsPrerelease: false);
    }

    #endregion

    #region Test Implementations

    private sealed class TestUpdateChecker : IUpdateChecker
    {
        public UpdateInfo? NextUpdate { get; set; }

        public Task<UpdateInfo?> CheckAsync(SysVersion currentVersion, CancellationToken ct = default)
        {
            return Task.FromResult(NextUpdate);
        }

        public Task<string?> GetChangelogAsync(string changelogUrl, CancellationToken ct = default)
        {
            return Task.FromResult<string?>("Test changelog");
        }
    }

    private sealed class TestUpdateInstaller : IUpdateInstaller
    {
        public Task<string> DownloadAsync(UpdateInfo update, CancellationToken ct = default)
        {
            return Task.FromResult(Path.GetTempFileName());
        }

        public Task<bool> InstallAsync(string downloadPath, bool requiresRestart, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RollbackAsync(SysVersion targetVersion, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public IReadOnlyList<RollbackOption> GetRollbackOptions()
        {
            return [];
        }
    }

    #endregion
}
