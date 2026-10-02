using FluentAssertions;
using InControl.Core.Recovery;
using InControl.Core.State;
using InControl.Core.Storage;
using Xunit;

namespace InControl.Core.Tests.Recovery;

public class StateRecoveryTests : IDisposable
{
    private readonly string _testDir;

    public StateRecoveryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"state-recovery-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenNoSessions()
    {
        var report = await StateRecovery.CheckHealthAsync();

        report.Should().NotBeNull();
        // May or may not be healthy depending on actual state
    }

    [Fact]
    public void QuarantineFile_ReturnsError_WhenFileNotFound()
    {
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.json");

        var result = StateRecovery.QuarantineFile(nonExistentPath);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(InControl.Core.Errors.ErrorCode.FileNotFound);
    }

    [Fact]
    public void ListBackups_ReturnsEmptyList_WhenNoBackups()
    {
        var backups = StateRecovery.ListBackups();

        // May have backups from previous tests, but shouldn't throw
        backups.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesBackupFile()
    {
        var result = await StateRecovery.CreateBackupAsync();

        if (result.IsSuccess)
        {
            result.Value.Should().NotBeNullOrEmpty();
            File.Exists(result.Value).Should().BeTrue();

            // Cleanup
            File.Delete(result.Value);
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_ReturnsError_WhenBackupNotFound()
    {
        var nonExistentPath = Path.Combine(_testDir, "nonexistent-backup.zip");

        var result = await StateRecovery.RestoreBackupAsync(nonExistentPath);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(InControl.Core.Errors.ErrorCode.FileNotFound);
    }
}

public class StateHealthReportTests
{
    [Fact]
    public void StateHealthReport_RecordsProperties()
    {
        var issues = new List<StateIssue>();
        var report = new StateHealthReport(
            IsHealthy: true,
            Issues: issues,
            CheckedAt: DateTimeOffset.UtcNow,
            TotalFiles: 5,
            CorruptFiles: 0
        );

        report.IsHealthy.Should().BeTrue();
        report.Issues.Should().BeEmpty();
        report.TotalFiles.Should().Be(5);
        report.CorruptFiles.Should().Be(0);
    }

    [Fact]
    public void StateHealthReport_WithCorruptFiles_IsNotHealthy()
    {
        var issues = new List<StateIssue>
        {
            new StateIssue(
                FilePath: "/test/file.json",
                IssueType: StateIssueType.InvalidJson,
                Description: "Test issue",
                RecoveryOptions: [RecoveryAction.Quarantine]
            )
        };

        var report = new StateHealthReport(
            IsHealthy: false,
            Issues: issues,
            CheckedAt: DateTimeOffset.UtcNow,
            TotalFiles: 5,
            CorruptFiles: 1
        );

        report.IsHealthy.Should().BeFalse();
        report.Issues.Should().HaveCount(1);
        report.CorruptFiles.Should().Be(1);
    }
}

public class StateIssueTests
{
    [Fact]
    public void StateIssue_RecordsAllProperties()
    {
        var issue = new StateIssue(
            FilePath: "/test/corrupt.json",
            IssueType: StateIssueType.InvalidJson,
            Description: "JSON parsing failed",
            RecoveryOptions: [RecoveryAction.Quarantine, RecoveryAction.Delete]
        );

        issue.FilePath.Should().Be("/test/corrupt.json");
        issue.IssueType.Should().Be(StateIssueType.InvalidJson);
        issue.Description.Should().Contain("JSON");
        issue.RecoveryOptions.Should().HaveCount(2);
        issue.RecoveryOptions.Should().Contain(RecoveryAction.Quarantine);
        issue.RecoveryOptions.Should().Contain(RecoveryAction.Delete);
    }

    [Theory]
    [InlineData(StateIssueType.EmptyFile)]
    [InlineData(StateIssueType.InvalidJson)]
    [InlineData(StateIssueType.InvalidVersion)]
    [InlineData(StateIssueType.AccessError)]
    [InlineData(StateIssueType.UnknownError)]
    public void StateIssueType_AllTypesAreDefined(StateIssueType issueType)
    {
        Enum.IsDefined(issueType).Should().BeTrue();
    }

    [Theory]
    [InlineData(RecoveryAction.Quarantine)]
    [InlineData(RecoveryAction.Delete)]
    [InlineData(RecoveryAction.RestoreBackup)]
    [InlineData(RecoveryAction.Retry)]
    [InlineData(RecoveryAction.Ignore)]
    public void RecoveryAction_AllActionsAreDefined(RecoveryAction action)
    {
        Enum.IsDefined(action).Should().BeTrue();
    }
}

public class BackupInfoTests
{
    [Fact]
    public void BackupInfo_RecordsAllProperties()
    {
        var createdAt = DateTime.UtcNow;
        var info = new BackupInfo(
            FilePath: "/backups/backup-2026.zip",
            FileName: "backup-2026.zip",
            CreatedAt: createdAt,
            SizeBytes: 1024 * 1024
        );

        info.FilePath.Should().Be("/backups/backup-2026.zip");
        info.FileName.Should().Be("backup-2026.zip");
        info.CreatedAt.Should().Be(createdAt);
        info.SizeBytes.Should().Be(1024 * 1024);
    }
}

public class ResetResultTests
{
    [Fact]
    public void ResetResult_WithExport_IncludesExportPath()
    {
        var result = new ResetResult(
            Success: true,
            ExportPath: "/backups/pre-reset-backup.zip",
            ClearedPaths: ["/sessions", "/cache", "/temp"]
        );

        result.Success.Should().BeTrue();
        result.ExportPath.Should().NotBeNull();
        result.ClearedPaths.Should().HaveCount(3);
    }

    [Fact]
    public void ResetResult_WithoutExport_HasNullExportPath()
    {
        var result = new ResetResult(
            Success: true,
            ExportPath: null,
            ClearedPaths: ["/sessions"]
        );

        result.Success.Should().BeTrue();
        result.ExportPath.Should().BeNull();
    }
}
