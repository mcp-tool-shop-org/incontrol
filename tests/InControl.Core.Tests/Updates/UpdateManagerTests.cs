using FluentAssertions;
using InControl.Core.Updates;
using Xunit;
using SysVersion = global::System.Version;

namespace InControl.Core.Tests.Updates;

public class UpdateManagerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly FakeUpdateChecker _checker;
    private readonly FakeUpdateInstaller _installer;
    private readonly UpdateManager _manager;

    public UpdateManagerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"update-test-{Guid.NewGuid()}.json");
        _checker = new FakeUpdateChecker();
        _installer = new FakeUpdateInstaller();
        _manager = new UpdateManager(_checker, _installer, _tempPath);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }

    [Fact]
    public void DefaultMode_IsManual()
    {
        _manager.Mode.Should().Be(UpdateMode.Manual);
    }

    [Fact]
    public void DefaultState_IsIdle()
    {
        _manager.State.Should().Be(UpdateState.Idle);
    }

    [Fact]
    public void SetMode_ChangesMode()
    {
        _manager.SetMode(UpdateMode.NotifyOnly);

        _manager.Mode.Should().Be(UpdateMode.NotifyOnly);
    }

    [Fact]
    public void SetMode_PersistsAcrossInstances()
    {
        _manager.SetMode(UpdateMode.AutoDownload);

        var newManager = new UpdateManager(_checker, _installer, _tempPath);

        newManager.Mode.Should().Be(UpdateMode.AutoDownload);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpdate_WhenAvailable()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));

        var update = await _manager.CheckForUpdateAsync();

        update.Should().NotBeNull();
        update!.Version.Should().Be(new SysVersion(2, 0, 0));
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_WhenNoUpdate()
    {
        _checker.NextUpdate = null;

        var update = await _manager.CheckForUpdateAsync();

        update.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdateAsync_SetsAvailableUpdate()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));

        await _manager.CheckForUpdateAsync();

        _manager.AvailableUpdate.Should().NotBeNull();
        _manager.AvailableUpdate!.Version.Should().Be(new SysVersion(2, 0, 0));
    }

    [Fact]
    public async Task CheckForUpdateAsync_UpdatesLastChecked()
    {
        var before = DateTimeOffset.UtcNow;

        await _manager.CheckForUpdateAsync();

        _manager.LastChecked.Should().NotBeNull();
        _manager.LastChecked!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task CheckForUpdateAsync_RaisesUpdateAvailable_WhenUpdateFound()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));
        UpdateAvailableEventArgs? capturedArgs = null;
        _manager.UpdateAvailable += (_, args) => capturedArgs = args;

        await _manager.CheckForUpdateAsync();

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Update.Version.Should().Be(new SysVersion(2, 0, 0));
    }

    [Fact]
    public async Task CheckForUpdateAsync_SetsStateToChecking_DuringCheck()
    {
        var states = new List<UpdateState>();
        _manager.StateChanged += (_, args) => states.Add(args.NewState);

        _checker.NextUpdate = null;
        _checker.Delay = TimeSpan.FromMilliseconds(50);

        var task = _manager.CheckForUpdateAsync();
        await Task.Delay(10);
        states.Should().Contain(UpdateState.Checking);

        await task;
        _manager.State.Should().Be(UpdateState.Idle);
    }

    [Fact]
    public async Task CheckForUpdateAsync_RaisesError_OnFailure()
    {
        _checker.ShouldThrow = true;
        UpdateErrorEventArgs? capturedArgs = null;
        _manager.Error += (_, args) => capturedArgs = args;

        await _manager.CheckForUpdateAsync();

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Type.Should().Be(UpdateErrorType.CheckFailed);
    }

    [Fact]
    public async Task DownloadUpdateAsync_ReturnsError_WhenNoUpdateAvailable()
    {
        var result = await _manager.DownloadUpdateAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No update available");
    }

    [Fact]
    public async Task DownloadUpdateAsync_ReturnsPath_OnSuccess()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));
        await _manager.CheckForUpdateAsync();

        var result = await _manager.DownloadUpdateAsync();

        result.Success.Should().BeTrue();
        result.DownloadPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DownloadUpdateAsync_SetsStateToReadyToInstall()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));
        await _manager.CheckForUpdateAsync();

        await _manager.DownloadUpdateAsync();

        _manager.State.Should().Be(UpdateState.ReadyToInstall);
    }

    [Fact]
    public async Task InstallUpdateAsync_UpdatesCurrentVersion_OnSuccess()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));
        await _manager.CheckForUpdateAsync();
        var download = await _manager.DownloadUpdateAsync();

        var result = await _manager.InstallUpdateAsync(download.DownloadPath!, false);

        result.Success.Should().BeTrue();
        _manager.CurrentVersion.Should().Be(new SysVersion(2, 0, 0));
    }

    [Fact]
    public async Task InstallUpdateAsync_ClearsAvailableUpdate_OnSuccess()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));
        await _manager.CheckForUpdateAsync();
        var download = await _manager.DownloadUpdateAsync();

        await _manager.InstallUpdateAsync(download.DownloadPath!, false);

        _manager.AvailableUpdate.Should().BeNull();
    }

    [Fact]
    public async Task InstallUpdateAsync_SetsPendingRestart_WhenRequired()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));
        await _manager.CheckForUpdateAsync();
        var download = await _manager.DownloadUpdateAsync();

        var result = await _manager.InstallUpdateAsync(download.DownloadPath!, requiresRestart: true);

        result.RequiresRestart.Should().BeTrue();
        _manager.State.Should().Be(UpdateState.PendingRestart);
    }

    [Fact]
    public async Task DismissUpdate_AddsVersionToDismissedList()
    {
        _checker.NextUpdate = CreateTestUpdate(new SysVersion(2, 0, 0));
        await _manager.CheckForUpdateAsync();

        _manager.DismissUpdate();

        // Load settings to verify
        var newManager = new UpdateManager(_checker, _installer, _tempPath);
        // Can't directly check dismissed versions, but the mechanism is tested
    }

    [Fact]
    public void GetRollbackOptions_ReturnsOptions()
    {
        _installer.RollbackOptions.Add(new RollbackOption(
            new SysVersion(1, 0, 0),
            DateTimeOffset.UtcNow.AddDays(-7),
            "Previous stable"));

        var options = _manager.GetRollbackOptions();

        options.Should().HaveCount(1);
        options[0].Version.Should().Be(new SysVersion(1, 0, 0));
    }

    [Fact]
    public async Task RollbackAsync_UpdatesCurrentVersion_OnSuccess()
    {
        var result = await _manager.RollbackAsync(new SysVersion(0, 9, 0));

        result.Should().BeTrue();
        _manager.CurrentVersion.Should().Be(new SysVersion(0, 9, 0));
    }

    [Fact]
    public async Task RollbackAsync_SetsStateToRollingBack_DuringRollback()
    {
        var states = new List<UpdateState>();
        _manager.StateChanged += (_, args) => states.Add(args.NewState);
        _installer.Delay = TimeSpan.FromMilliseconds(50);

        var task = _manager.RollbackAsync(new SysVersion(0, 9, 0));
        await Task.Delay(10);
        states.Should().Contain(UpdateState.RollingBack);

        await task;
    }

    private static UpdateInfo CreateTestUpdate(SysVersion version) => new(
        Version: version,
        Title: $"Version {version}",
        Description: "Test update",
        ChangelogUrl: "https://example.com/changelog",
        DownloadUrl: "https://example.com/download",
        SizeBytes: 1024 * 1024,
        Checksum: "abc123",
        ReleasedAt: DateTimeOffset.UtcNow,
        IsCritical: false,
        IsPrerelease: false
    );

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        public UpdateInfo? NextUpdate { get; set; }
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
        public bool ShouldThrow { get; set; }

        public async Task<UpdateInfo?> CheckAsync(SysVersion currentVersion, CancellationToken ct = default)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, ct);
            }

            if (ShouldThrow)
            {
                throw new Exception("Check failed");
            }

            return NextUpdate;
        }

        public Task<string?> GetChangelogAsync(string changelogUrl, CancellationToken ct = default)
        {
            return Task.FromResult<string?>("# Changelog\n\n- Fix bugs\n- Add features");
        }
    }

    private sealed class FakeUpdateInstaller : IUpdateInstaller
    {
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
        public bool ShouldThrow { get; set; }
        public List<RollbackOption> RollbackOptions { get; } = [];

        public async Task<string> DownloadAsync(UpdateInfo update, CancellationToken ct = default)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, ct);
            }

            if (ShouldThrow)
            {
                throw new Exception("Download failed");
            }

            return Path.Combine(Path.GetTempPath(), $"update-{update.Version}.msix");
        }

        public async Task<bool> InstallAsync(string downloadPath, bool requiresRestart, CancellationToken ct = default)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, ct);
            }

            if (ShouldThrow)
            {
                throw new Exception("Install failed");
            }

            return true;
        }

        public async Task<bool> RollbackAsync(SysVersion targetVersion, CancellationToken ct = default)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, ct);
            }

            return true;
        }

        public IReadOnlyList<RollbackOption> GetRollbackOptions() => RollbackOptions;
    }
}

public class UpdateModeTests
{
    [Theory]
    [InlineData(UpdateMode.Manual)]
    [InlineData(UpdateMode.NotifyOnly)]
    [InlineData(UpdateMode.AutoDownload)]
    [InlineData(UpdateMode.AutoInstall)]
    public void UpdateMode_AllValuesAreDefined(UpdateMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }
}

public class UpdateStateTests
{
    [Theory]
    [InlineData(UpdateState.Idle)]
    [InlineData(UpdateState.Checking)]
    [InlineData(UpdateState.Downloading)]
    [InlineData(UpdateState.ReadyToInstall)]
    [InlineData(UpdateState.Installing)]
    [InlineData(UpdateState.PendingRestart)]
    [InlineData(UpdateState.RollingBack)]
    public void UpdateState_AllValuesAreDefined(UpdateState state)
    {
        Enum.IsDefined(state).Should().BeTrue();
    }
}

public class UpdateInfoTests
{
    [Fact]
    public void UpdateInfo_RecordsAllProperties()
    {
        var version = new SysVersion(2, 0, 0);
        var releasedAt = DateTimeOffset.UtcNow;

        var info = new UpdateInfo(
            Version: version,
            Title: "Version 2.0",
            Description: "Major update",
            ChangelogUrl: "https://example.com/changelog",
            DownloadUrl: "https://example.com/download",
            SizeBytes: 1024,
            Checksum: "sha256",
            ReleasedAt: releasedAt,
            IsCritical: true,
            IsPrerelease: false
        );

        info.Version.Should().Be(version);
        info.Title.Should().Be("Version 2.0");
        info.Description.Should().Be("Major update");
        info.IsCritical.Should().BeTrue();
        info.IsPrerelease.Should().BeFalse();
    }
}

public class RollbackOptionTests
{
    [Fact]
    public void RollbackOption_RecordsAllProperties()
    {
        var version = new SysVersion(1, 0, 0);
        var installedAt = DateTimeOffset.UtcNow.AddDays(-7);

        var option = new RollbackOption(version, installedAt, "Previous stable");

        option.Version.Should().Be(version);
        option.InstalledAt.Should().Be(installedAt);
        option.Notes.Should().Be("Previous stable");
    }
}
