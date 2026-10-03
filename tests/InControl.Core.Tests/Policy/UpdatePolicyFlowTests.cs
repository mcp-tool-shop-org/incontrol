using FluentAssertions;
using InControl.Core.Policy;
using InControl.Core.Updates;
using Xunit;
using SysVersion = System.Version;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Update policy end to end: channel limits, deferral, minimum version, and the
/// check, download and install steps that sit behind them.
/// </summary>
public class UpdatePolicyFlowTests : IDisposable
{
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"update-flow-{Guid.NewGuid()}.json");
    private readonly FakeChecker _checker = new();
    private readonly FakeInstaller _installer = new();

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    private PolicyGovernedUpdateManager Governed(UpdatePolicyRules? rules = null, string channel = "stable")
    {
        var engine = new PolicyEngine();
        if (rules is not null)
        {
            engine.SetPolicy(PolicySource.Organization, new PolicyDocument { Version = "1.0", Updates = rules });
        }

        return new UpdateManager(_checker, _installer, _settingsPath).WithPolicyEnforcement(engine, channel);
    }

    private static UpdateInfo Update(int daysOld, bool critical = false) => new(
        Version: new SysVersion(2, 0, 0),
        Title: "Update",
        Description: "desc",
        ChangelogUrl: "https://example.com/changelog",
        DownloadUrl: "https://example.com/download",
        SizeBytes: 100,
        Checksum: "abc",
        ReleasedAt: DateTimeOffset.UtcNow.AddDays(-daysOld),
        IsCritical: critical,
        IsPrerelease: false);

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var manager = new UpdateManager(_checker, _installer, _settingsPath);

        var noManager = () => new PolicyGovernedUpdateManager(null!, new PolicyEngine());
        var noEngine = () => new PolicyGovernedUpdateManager(manager, null!);

        noManager.Should().Throw<ArgumentNullException>();
        noEngine.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Passthroughs_ReflectTheInnerManager()
    {
        var governed = Governed();

        governed.CurrentVersion.Should().Be(governed.InnerManager.CurrentVersion);
        governed.State.Should().Be(UpdateState.Idle);
        governed.CanCheckOnStartup().Should().BeTrue();
    }

    [Fact]
    public void CanCheckOnStartup_FollowsPolicy()
    {
        Governed(new UpdatePolicyRules { CheckOnStartup = false }).CanCheckOnStartup().Should().BeFalse();
    }

    #region Channels and minimum version

    [Fact]
    public void IsChannelAllowed_RequiredChannelWins_AndIgnoresTheAllowedList()
    {
        var governed = Governed(new UpdatePolicyRules { RequiredChannel = "stable", AllowedChannels = ["beta"] });

        governed.IsChannelAllowed("STABLE").Should().BeTrue();
        governed.IsChannelAllowed("beta").Should().BeFalse();
    }

    [Fact]
    public void IsChannelAllowed_AllowedListIsCaseInsensitive_AndNoRestrictionAllowsAll()
    {
        var restricted = Governed(new UpdatePolicyRules { AllowedChannels = ["stable", "Beta"] });
        var open = Governed();

        restricted.IsChannelAllowed("beta").Should().BeTrue();
        restricted.IsChannelAllowed("dev").Should().BeFalse();
        open.IsChannelAllowed("canary").Should().BeTrue();
    }

    [Fact]
    public void CheckUpdatePolicy_ExplainsTheFirstProblem_InPriorityOrder()
    {
        var autoOff = Governed(new UpdatePolicyRules { AutoUpdate = false, RequiredChannel = "beta", MinimumVersion = "9.0.0" }).CheckUpdatePolicy();
        var wrongChannel = Governed(new UpdatePolicyRules { RequiredChannel = "beta", MinimumVersion = "9.0.0" }).CheckUpdatePolicy();
        var tooOld = Governed(new UpdatePolicyRules { MinimumVersion = "9.0.0" }).CheckUpdatePolicy();
        var fine = Governed().CheckUpdatePolicy();

        autoOff.Reason.Should().Contain("Auto-update is disabled");
        wrongChannel.Reason.Should().Contain("Channel 'stable' is not allowed");
        wrongChannel.IsChannelAllowed.Should().BeFalse();
        tooOld.Reason.Should().Contain("below minimum required: 9.0.0");
        tooOld.MeetsMinimumVersion.Should().BeFalse();
        fine.Reason.Should().BeNull();
        fine.AllowedChannels.Should().BeNull();
    }

    [Fact]
    public void CheckUpdatePolicy_ReportsTheAllowedChannelsAndDeferral()
    {
        var status = Governed(new UpdatePolicyRules { AllowedChannels = ["stable", "beta"], DeferDays = 5 }).CheckUpdatePolicy();

        status.AllowedChannels.Should().Equal("stable", "beta");
        status.DeferDays.Should().Be(5);
        status.CurrentChannel.Should().Be("stable");
    }

    [Fact]
    public void MeetsMinimumVersion_AnUnparseableMinimum_DoesNotBlock()
    {
        // The validator refuses such a value when a file is loaded; the engine itself fails open.
        Governed(new UpdatePolicyRules { MinimumVersion = "soon" }).MeetsMinimumVersion().Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumVersion_TheCurrentVersionItself_Counts()
    {
        var current = Governed().CurrentVersion.ToString();

        Governed(new UpdatePolicyRules { MinimumVersion = current }).MeetsMinimumVersion().Should().BeTrue();
    }

    [Fact]
    public void ComplianceInfo_NamesTheWrongChannel_AndPutsBelowMinimumFirst()
    {
        var channel = Governed(new UpdatePolicyRules { RequiredChannel = "beta" }).GetComplianceInfo();
        var both = Governed(new UpdatePolicyRules { RequiredChannel = "beta", MinimumVersion = "9.0.0" }).GetComplianceInfo();
        var allowedListOnly = Governed(new UpdatePolicyRules { AllowedChannels = ["beta"] }).GetComplianceInfo();

        channel.Status.Should().Be(ComplianceStatus.WrongChannel);
        channel.Message.Should().Contain("'stable' is not allowed").And.Contain("Required: beta");
        both.Status.Should().Be(ComplianceStatus.BelowMinimum);
        allowedListOnly.Status.Should().Be(ComplianceStatus.WrongChannel);
        allowedListOnly.Message.Should().Contain("Required: N/A");
    }

    #endregion

    #region Deferral

    [Fact]
    public void Deferral_NoDeferDays_NeverDefers()
    {
        var result = Governed().CheckDeferral(Update(daysOld: 0));

        result.ShouldDefer.Should().BeFalse();
        result.DeferUntil.Should().BeNull();
        result.DaysRemaining.Should().Be(0);
    }

    [Fact]
    public void Deferral_FreshReleaseWithinTheWindow_DefersAndCountsRemainingDaysUp()
    {
        var result = Governed(new UpdatePolicyRules { DeferDays = 7 }).CheckDeferral(Update(daysOld: 2));

        result.ShouldDefer.Should().BeTrue();
        result.DaysRemaining.Should().Be(5);
        result.DeferUntil.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(5), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Deferral_ReleaseOlderThanTheWindow_DoesNotDefer()
    {
        Governed(new UpdatePolicyRules { DeferDays = 7 }).CheckDeferral(Update(daysOld: 8)).ShouldDefer.Should().BeFalse();
    }

    #endregion

    #region Mode

    [Theory]
    [InlineData(UpdateMode.AutoDownload)]
    [InlineData(UpdateMode.AutoInstall)]
    public void SetMode_AutoModesAreBlockedWhenPolicyDisablesAutoUpdate(UpdateMode mode)
    {
        var governed = Governed(new UpdatePolicyRules { AutoUpdate = false });
        var raised = new List<AutoUpdateBlockedEventArgs>();
        governed.AutoUpdateBlocked += (_, e) => raised.Add(e);
        var before = governed.Mode;

        var result = governed.SetMode(mode);

        result.IsSuccess.Should().BeFalse();
        result.WasBlocked.Should().BeTrue();
        governed.Mode.Should().Be(before);
        raised.Should().ContainSingle().Which.RequestedMode.Should().Be(mode);
    }

    #endregion

    #region Check

    [Fact]
    public async Task Check_WrongChannel_NamesTheAllowedListWhenNoneIsRequired_AndNeverAsksTheServer()
    {
        _checker.Next = Update(daysOld: 30);
        var governed = Governed(new UpdatePolicyRules { AllowedChannels = ["stable", "beta"] }, channel: "dev");
        ChannelBlockedEventArgs? raised = null;
        governed.ChannelBlocked += (_, e) => raised = e;

        var result = await governed.CheckForUpdateAsync();

        result.WasChannelBlocked.Should().BeTrue();
        result.RequiredChannel.Should().BeNull();
        raised!.RequiredChannel.Should().Be("stable, beta");
        _checker.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Check_WithAnUpdateOutsideTheDeferWindow_IsAvailable()
    {
        _checker.Next = Update(daysOld: 30);

        var result = await Governed(new UpdatePolicyRules { DeferDays = 7 }).CheckForUpdateAsync();

        result.HasUpdate.Should().BeTrue();
        result.WasDeferred.Should().BeFalse();
        result.Update.Should().NotBeNull();
    }

    [Fact]
    public async Task Check_FreshUpdate_IsDeferred_AndRaisesTheEvent()
    {
        _checker.Next = Update(daysOld: 1);
        var governed = Governed(new UpdatePolicyRules { DeferDays = 7 });
        UpdateDeferredEventArgs? raised = null;
        governed.UpdateDeferred += (_, e) => raised = e;

        var result = await governed.CheckForUpdateAsync();

        result.HasUpdate.Should().BeTrue();
        result.WasDeferred.Should().BeTrue();
        result.DaysRemaining.Should().Be(6);
        result.DeferredUntil.Should().NotBeNull();
        raised.Should().NotBeNull();
        raised!.DaysRemaining.Should().Be(6);
        raised.Update.Should().BeSameAs(result.Update);
        raised.DeferredUntil.Should().Be(result.DeferredUntil!.Value);
    }

    [Fact]
    public async Task Check_CriticalUpdate_IsNeverDeferred()
    {
        _checker.Next = Update(daysOld: 0, critical: true);

        var result = await Governed(new UpdatePolicyRules { DeferDays = 30 }).CheckForUpdateAsync();

        result.WasDeferred.Should().BeFalse();
        result.HasUpdate.Should().BeTrue();
    }

    #endregion

    #region Download and install

    [Fact]
    public async Task Download_WithNothingChecked_SaysThereIsNoUpdate()
    {
        var result = await Governed().DownloadUpdateAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No update available");
    }

    [Fact]
    public async Task Download_OfADeferredUpdate_IsRefusedWithTheDate()
    {
        _checker.Next = Update(daysOld: 1);
        var governed = Governed(new UpdatePolicyRules { DeferDays = 7 });
        await governed.CheckForUpdateAsync();

        var result = await governed.DownloadUpdateAsync();

        result.IsSuccess.Should().BeFalse();
        result.WasDeferred.Should().BeTrue();
        result.DaysRemaining.Should().Be(6);
        result.DeferredUntil.Should().NotBeNull();
        _installer.Downloads.Should().Be(0);
    }

    [Fact]
    public async Task Download_OfACriticalUpdate_IgnoresTheDeferral()
    {
        _checker.Next = Update(daysOld: 0, critical: true);
        var governed = Governed(new UpdatePolicyRules { DeferDays = 30 });
        await governed.CheckForUpdateAsync();

        var result = await governed.DownloadUpdateAsync();

        result.IsSuccess.Should().BeTrue();
        result.DownloadPath.Should().Be("C:\\downloads\\update.msix");
        governed.State.Should().Be(UpdateState.ReadyToInstall);
    }

    [Fact]
    public async Task Download_WhenTheInstallerFails_ReportsTheReason()
    {
        _checker.Next = Update(daysOld: 30);
        _installer.DownloadError = new IOException("disk full");
        var governed = Governed();
        await governed.CheckForUpdateAsync();

        var result = await governed.DownloadUpdateAsync();

        result.IsSuccess.Should().BeFalse();
        result.WasDeferred.Should().BeFalse();
        result.Error.Should().Be("disk full");
    }

    [Fact]
    public async Task Install_Success_ReportsTheRestartFlag()
    {
        var result = await Governed().InstallUpdateAsync("C:\\downloads\\update.msix", requiresRestart: true);

        result.IsSuccess.Should().BeTrue();
        result.RequiresRestart.Should().BeTrue();
    }

    [Fact]
    public async Task Install_WhenTheInstallerDeclines_ReportsAFailure()
    {
        _installer.InstallResult = false;

        var result = await Governed().InstallUpdateAsync("C:\\downloads\\update.msix", requiresRestart: false);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Installation failed");
    }

    [Fact]
    public async Task Install_WhenTheInstallerThrows_ReportsTheMessage()
    {
        _installer.InstallError = new InvalidOperationException("signature mismatch");

        var result = await Governed().InstallUpdateAsync("C:\\downloads\\update.msix", requiresRestart: false);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("signature mismatch");
    }

    [Fact]
    public async Task Statistics_AfterACheck_IncludeTheAvailableUpdateAndLastCheckTime()
    {
        _checker.Next = Update(daysOld: 30);
        var governed = Governed(new UpdatePolicyRules { RequiredChannel = "stable" });
        await governed.CheckForUpdateAsync();

        var stats = governed.GetStatistics();

        stats.AvailableUpdate.Should().NotBeNull();
        stats.LastChecked.Should().NotBeNull();
        stats.CurrentState.Should().Be(UpdateState.Idle);
        stats.AutoUpdateAllowed.Should().BeTrue();
        stats.CheckOnStartupAllowed.Should().BeTrue();
        stats.CurrentVersion.Should().Be(governed.CurrentVersion);
    }

    #endregion

    #region Result factories

    [Fact]
    public void ResultFactories_SetTheFieldsTheyName()
    {
        var info = Update(daysOld: 1);
        var until = DateTimeOffset.UtcNow.AddDays(3);

        var available = PolicyGovernedCheckResult.Available(info);
        available.HasUpdate.Should().BeTrue();
        available.Update.Should().BeSameAs(info);

        var deferred = PolicyGovernedCheckResult.Deferred(info, until, 3);
        deferred.WasDeferred.Should().BeTrue();
        deferred.DeferredUntil.Should().Be(until);
        deferred.DaysRemaining.Should().Be(3);

        PolicyGovernedCheckResult.NoUpdate().HasUpdate.Should().BeFalse();

        var blocked = PolicyGovernedCheckResult.ChannelBlocked("dev", "stable");
        blocked.WasChannelBlocked.Should().BeTrue();
        blocked.BlockedChannel.Should().Be("dev");
        blocked.RequiredChannel.Should().Be("stable");

        PolicyGovernedDownloadResult.Success("p").IsSuccess.Should().BeTrue();
        PolicyGovernedDownloadResult.Failed("e").Error.Should().Be("e");
        var deferredDownload = PolicyGovernedDownloadResult.Deferred(until, 3);
        deferredDownload.WasDeferred.Should().BeTrue();
        deferredDownload.DaysRemaining.Should().Be(3);

        PolicyGovernedInstallResult.Success(true).RequiresRestart.Should().BeTrue();
        PolicyGovernedInstallResult.Failed("x").Error.Should().Be("x");
    }

    #endregion

    private sealed class FakeChecker : IUpdateChecker
    {
        public UpdateInfo? Next { get; set; }

        public int Calls { get; private set; }

        public Task<UpdateInfo?> CheckAsync(SysVersion currentVersion, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Next);
        }

        public Task<string?> GetChangelogAsync(string changelogUrl, CancellationToken ct = default) =>
            Task.FromResult<string?>("changelog");
    }

    private sealed class FakeInstaller : IUpdateInstaller
    {
        public int Downloads { get; private set; }

        public Exception? DownloadError { get; set; }

        public Exception? InstallError { get; set; }

        public bool InstallResult { get; set; } = true;

        public Task<string> DownloadAsync(UpdateInfo update, CancellationToken ct = default)
        {
            Downloads++;
            if (DownloadError is not null)
            {
                throw DownloadError;
            }

            return Task.FromResult("C:\\downloads\\update.msix");
        }

        public Task<bool> InstallAsync(string downloadPath, bool requiresRestart, CancellationToken ct = default)
        {
            if (InstallError is not null)
            {
                throw InstallError;
            }

            return Task.FromResult(InstallResult);
        }

        public Task<bool> RollbackAsync(SysVersion targetVersion, CancellationToken ct = default) => Task.FromResult(true);

        public IReadOnlyList<RollbackOption> GetRollbackOptions() => [];
    }
}
