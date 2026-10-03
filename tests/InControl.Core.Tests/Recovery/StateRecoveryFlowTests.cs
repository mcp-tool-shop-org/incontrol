using System.IO.Compression;
using FluentAssertions;
using InControl.Core.Errors;
using InControl.Core.Models;
using InControl.Core.Recovery;
using InControl.Core.State;
using InControl.Core.Storage;
using Xunit;

namespace InControl.Core.Tests.Recovery;

/// <summary>
/// Recovery flows end to end on a temp data root: health checks, quarantine, delete,
/// backup, restore and reset. The data-path override is process-wide, so these run alone.
/// </summary>
[Collection("DataPathsIsolation")]
public class StateRecoveryFlowTests : IDisposable
{
    private readonly string _root;

    public StateRecoveryFlowTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "recovery-flow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        DataPaths.Configure(new DataPathsConfig(
            AppDataRoot: _root,
            Sessions: Path.Combine(_root, "sessions"),
            Logs: Path.Combine(_root, "logs"),
            Cache: Path.Combine(_root, "cache"),
            Exports: Path.Combine(_root, "exports"),
            Config: Path.Combine(_root, "config"),
            Temp: Path.Combine(_root, "temp"),
            Support: Path.Combine(_root, "support")));
        Directory.CreateDirectory(DataPaths.Sessions);
    }

    public void Dispose()
    {
        DataPaths.ResetConfiguration();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteConversation(string title)
    {
        var conversation = Conversation.Create(title);
        var path = Path.Combine(DataPaths.Sessions, $"{conversation.Id}.json");
        File.WriteAllText(path, StateSerializer.Serialize(conversation));
        return path;
    }

    private static string WriteRaw(string name, string content)
    {
        var path = Path.Combine(DataPaths.Sessions, name);
        File.WriteAllText(path, content);
        return path;
    }

    #region Health

    [Fact]
    public async Task CheckHealth_MissingSessionsFolder_IsHealthy()
    {
        Directory.Delete(DataPaths.Sessions);

        var report = await StateRecovery.CheckHealthAsync();

        report.IsHealthy.Should().BeTrue();
        report.TotalFiles.Should().Be(0);
    }

    [Fact]
    public async Task CheckHealth_EmptyFile_IsReportedWithQuarantineAndDelete()
    {
        var path = WriteRaw("empty.json", "");

        var report = await StateRecovery.CheckHealthAsync();

        report.IsHealthy.Should().BeFalse();
        var issue = report.Issues.Should().ContainSingle().Subject;
        issue.FilePath.Should().Be(path);
        issue.IssueType.Should().Be(StateIssueType.EmptyFile);
        issue.RecoveryOptions.Should().Equal(RecoveryAction.Quarantine, RecoveryAction.Delete);
        issue.Description.Should().Contain("empty.json");
    }

    [Fact]
    public async Task CheckHealth_InvalidJson_OffersRestoreBackupToo()
    {
        WriteRaw("broken.json", "{ this is not json");

        var report = await StateRecovery.CheckHealthAsync();

        var issue = report.Issues.Should().ContainSingle().Subject;
        issue.IssueType.Should().Be(StateIssueType.InvalidJson);
        issue.RecoveryOptions.Should().Equal(RecoveryAction.Quarantine, RecoveryAction.Delete, RecoveryAction.RestoreBackup);
        issue.Description.Should().Contain("invalid JSON");
    }

    [Fact]
    public async Task CheckHealth_JsonNull_IsAnIssueNotAHealthyFile()
    {
        WriteRaw("null.json", "null");

        var report = await StateRecovery.CheckHealthAsync();

        report.Issues.Should().ContainSingle().Which.IssueType.Should().Be(StateIssueType.InvalidJson);
    }

    [Fact]
    public async Task CheckHealth_LockedFile_IsAnAccessErrorOfferingRetry()
    {
        var path = WriteConversation("locked");
        await using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var report = await StateRecovery.CheckHealthAsync();

        var issue = report.Issues.Should().ContainSingle().Subject;
        issue.IssueType.Should().Be(StateIssueType.AccessError);
        issue.RecoveryOptions.Should().Equal(RecoveryAction.Retry, RecoveryAction.Delete);
    }

    [Fact]
    public async Task CheckHealth_CountsHealthyAndCorruptFiles_AndIgnoresOtherExtensions()
    {
        WriteConversation("good one");
        WriteConversation("good two");
        WriteRaw("bad.json", "[");
        WriteRaw("notes.txt", "not a session");

        var report = await StateRecovery.CheckHealthAsync();

        report.TotalFiles.Should().Be(3);
        report.CorruptFiles.Should().Be(1);
        report.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealth_CancelledBeforeTheFirstFile_Throws()
    {
        WriteConversation("one");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await StateRecovery.CheckHealthAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Quarantine and delete

    [Fact]
    public void Quarantine_MovesTheFileUnderAQuarantineFolderWithATimestampPrefix()
    {
        var path = WriteRaw("bad.json", "[");

        var result = StateRecovery.QuarantineFile(path);

        result.IsSuccess.Should().BeTrue();
        File.Exists(path).Should().BeFalse();
        var moved = result.Value!;
        Path.GetDirectoryName(moved).Should().Be(Path.Combine(_root, "quarantine"));
        Path.GetFileName(moved).Should().EndWith("_bad.json");
        File.ReadAllText(moved).Should().Be("[");
    }

    [Fact]
    public void Quarantine_FileThatCannotBeMoved_ReportsAFailureAndKeepsTheFile()
    {
        var path = WriteConversation("in use");
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = StateRecovery.QuarantineFile(path);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task Apply_Quarantine_AndDelete_RemoveTheFileFromSessions()
    {
        var quarantined = WriteRaw("q.json", "[");
        var deleted = WriteRaw("d.json", "[");

        (await StateRecovery.ApplyRecoveryAsync(Issue(quarantined), RecoveryAction.Quarantine)).IsSuccess.Should().BeTrue();
        (await StateRecovery.ApplyRecoveryAsync(Issue(deleted), RecoveryAction.Delete)).IsSuccess.Should().BeTrue();

        File.Exists(quarantined).Should().BeFalse();
        File.Exists(deleted).Should().BeFalse();
        Directory.GetFiles(Path.Combine(_root, "quarantine")).Should().ContainSingle();
    }

    [Fact]
    public async Task Apply_Delete_OfAFileThatIsAlreadyGone_Succeeds()
    {
        var result = await StateRecovery.ApplyRecoveryAsync(
            Issue(Path.Combine(DataPaths.Sessions, "gone.json")), RecoveryAction.Delete);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Apply_Delete_OfALockedFile_ReportsFailure()
    {
        var path = WriteConversation("locked");
        await using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await StateRecovery.ApplyRecoveryAsync(Issue(path), RecoveryAction.Delete);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
    }

    [Theory]
    [InlineData(RecoveryAction.Retry)]
    [InlineData(RecoveryAction.Ignore)]
    public async Task Apply_RetryAndIgnore_LeaveTheFileAlone(RecoveryAction action)
    {
        var path = WriteRaw("keep.json", "[");

        var result = await StateRecovery.ApplyRecoveryAsync(Issue(path), action);

        result.IsSuccess.Should().BeTrue();
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task Apply_AnUnknownAction_IsRefused()
    {
        var result = await StateRecovery.ApplyRecoveryAsync(Issue("x.json"), (RecoveryAction)99);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.InvalidOperation);
    }

    [Fact]
    public async Task Apply_RestoreBackup_WithNoBackups_SaysSo()
    {
        var result = await StateRecovery.ApplyRecoveryAsync(Issue("x.json"), RecoveryAction.RestoreBackup);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileNotFound);
    }

    private static StateIssue Issue(string path) => new(
        path, StateIssueType.InvalidJson, "test", [RecoveryAction.Quarantine]);

    #endregion

    #region Backup and restore

    [Fact]
    public async Task Backup_OfAMissingSessionsFolder_IsAnEmptyArchive()
    {
        Directory.Delete(DataPaths.Sessions);

        var result = await StateRecovery.CreateBackupAsync();

        result.IsSuccess.Should().BeTrue();
        using var archive = ZipFile.OpenRead(result.Value!);
        archive.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Backup_FailureToWrite_IsReported()
    {
        // The backup folder cannot be created because a file has its name.
        File.WriteAllText(Path.Combine(_root, "backup"), "in the way");

        var result = await StateRecovery.CreateBackupAsync();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
    }

    [Fact]
    public async Task Restore_PutsTheBackedUpSessionsBack_AndKeepsACopyOfWhatItReplaced()
    {
        var original = WriteConversation("original");
        var created = await StateRecovery.CreateBackupAsync();
        var backup = Path.Combine(_root, "older-backup.zip");
        File.Copy(created.Value!, backup);
        File.Delete(created.Value!);
        File.Delete(original);
        var newer = WriteConversation("newer");

        var result = await StateRecovery.RestoreBackupAsync(backup);

        result.IsSuccess.Should().BeTrue();
        File.Exists(original).Should().BeTrue();
        File.Exists(newer).Should().BeFalse();
        var safety = StateRecovery.ListBackups();
        safety.Should().ContainSingle("the sessions that were replaced are backed up first");
        using var archive = ZipFile.OpenRead(safety[0].FilePath);
        archive.Entries.Should().ContainSingle(e => e.Name == Path.GetFileName(newer));
    }

    [Fact]
    public async Task Restore_FromACorruptArchive_FailsWithoutLosingTheCurrentSessions()
    {
        var keep = WriteConversation("must survive");
        var corrupt = Path.Combine(_root, "corrupt-backup.zip");
        File.WriteAllText(corrupt, "this is not a zip archive");

        var result = await StateRecovery.RestoreBackupAsync(corrupt);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
        File.Exists(keep).Should().BeTrue("a bad backup must not cost the sessions that are on disk");
    }

    [Fact]
    public async Task Restore_FromAnEmptyArchive_LeavesAnEmptySessionsFolder_AndNoStagingFolderBehind()
    {
        WriteConversation("replaced");
        var empty = Path.Combine(_root, "empty-backup.zip");
        using (ZipFile.Open(empty, ZipArchiveMode.Create)) { }

        var result = await StateRecovery.RestoreBackupAsync(empty);

        result.IsSuccess.Should().BeTrue();
        Directory.Exists(DataPaths.Sessions).Should().BeTrue();
        Directory.GetFiles(DataPaths.Sessions).Should().BeEmpty();
        Directory.GetDirectories(_root, "sessions.restore-*").Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_ThroughRecoveryAction_UsesTheNewestBackup()
    {
        var kept = WriteConversation("in backup");
        var created = await StateRecovery.CreateBackupAsync();
        created.IsSuccess.Should().BeTrue();
        File.Delete(kept);
        // Applying the action also writes a safety backup in the same second; give it another name.
        var staged = Path.Combine(_root, "staged.zip");
        File.Move(created.Value!, staged);
        var backupFolder = Path.Combine(_root, "backup");
        File.Copy(staged, Path.Combine(backupFolder, "backup-19990101-000000.zip"));

        var result = await StateRecovery.ApplyRecoveryAsync(Issue("x.json"), RecoveryAction.RestoreBackup);

        result.IsSuccess.Should().BeTrue();
        File.Exists(kept).Should().BeTrue();
    }

    [Fact]
    public void ListBackups_ReturnsTheNewestFirst_AndOnlyBackupZips()
    {
        var folder = Path.Combine(_root, "backup");
        Directory.CreateDirectory(folder);
        var older = Path.Combine(folder, "backup-20200101-000000.zip");
        var newer = Path.Combine(folder, "backup-20210101-000000.zip");
        File.WriteAllText(older, "a");
        File.WriteAllText(newer, "bb");
        File.WriteAllText(Path.Combine(folder, "other.zip"), "c");
        File.SetCreationTimeUtc(older, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(newer, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var backups = StateRecovery.ListBackups();

        backups.Select(b => b.FileName).Should().Equal(
            "backup-20210101-000000.zip", "backup-20200101-000000.zip");
        backups[0].SizeBytes.Should().Be(2);
        backups[0].CreatedAt.Should().Be(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    #endregion

    #region Reset

    [Fact]
    public async Task Reset_WithExport_BacksUpThenClearsSessionsCacheTempAndConfigFiles()
    {
        WriteConversation("will be backed up");
        Directory.CreateDirectory(DataPaths.Cache);
        File.WriteAllText(Path.Combine(DataPaths.Cache, "c.bin"), "x");
        Directory.CreateDirectory(DataPaths.Temp);
        File.WriteAllText(Path.Combine(DataPaths.Temp, "t.bin"), "x");
        Directory.CreateDirectory(DataPaths.Config);
        File.WriteAllText(Path.Combine(DataPaths.Config, "settings.json"), "{}");
        Directory.CreateDirectory(DataPaths.Logs);
        File.WriteAllText(Path.Combine(DataPaths.Logs, "app.log"), "log");

        var result = await StateRecovery.ResetApplicationAsync(exportFirst: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.ExportPath.Should().NotBeNull();
        File.Exists(result.Value.ExportPath!).Should().BeTrue();
        result.Value.ClearedPaths.Should().Equal(DataPaths.Sessions, DataPaths.Cache, DataPaths.Temp);
        Directory.GetFiles(DataPaths.Sessions).Should().BeEmpty();
        Directory.GetFiles(DataPaths.Cache).Should().BeEmpty();
        Directory.GetFiles(DataPaths.Temp).Should().BeEmpty();
        Directory.GetFiles(DataPaths.Config).Should().BeEmpty();
        File.Exists(Path.Combine(DataPaths.Logs, "app.log")).Should().BeTrue("logs are kept for troubleshooting");
        using var archive = ZipFile.OpenRead(result.Value.ExportPath!);
        archive.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Reset_WithoutExport_MakesNoBackup()
    {
        WriteConversation("gone");

        var result = await StateRecovery.ResetApplicationAsync(exportFirst: false);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExportPath.Should().BeNull();
        Directory.Exists(Path.Combine(_root, "backup")).Should().BeFalse();
        Directory.GetFiles(DataPaths.Sessions).Should().BeEmpty();
    }

    [Fact]
    public async Task Reset_WhenTheBackupFails_ClearsNothing()
    {
        var session = WriteConversation("must survive a failed backup");
        File.WriteAllText(Path.Combine(_root, "backup"), "in the way");

        var result = await StateRecovery.ResetApplicationAsync(exportFirst: true);

        result.IsFailure.Should().BeTrue();
        File.Exists(session).Should().BeTrue();
    }

    [Fact]
    public async Task Reset_WhenAFileCannotBeDeleted_ReportsTheFailure()
    {
        Directory.CreateDirectory(DataPaths.Config);
        var locked = Path.Combine(DataPaths.Config, "settings.json");
        File.WriteAllText(locked, "{}");
        await using var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await StateRecovery.ResetApplicationAsync(exportFirst: false);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
        result.Error.Message.Should().Contain("reset");
    }

    #endregion
}
