using FluentAssertions;
using InControl.Core.Storage;
using Xunit;

namespace InControl.Core.Tests.Storage;

/// <summary>
/// DataPaths against a temp root set through <see cref="DataPaths.Configure"/>.
/// The override is process-wide, so these run alone in the shared isolation collection.
/// </summary>
[Collection("DataPathsIsolation")]
public class DataPathsConfiguredTests : IDisposable
{
    private readonly string _root;
    private readonly string _exports;
    private readonly DataPathsConfig _config;

    public DataPathsConfiguredTests()
    {
        var unique = Guid.NewGuid().ToString("N");
        _root = Path.Combine(Path.GetTempPath(), "datapaths-" + unique, "app");
        _exports = Path.Combine(Path.GetTempPath(), "datapaths-" + unique, "exports");
        _config = new DataPathsConfig(
            AppDataRoot: _root,
            Sessions: Path.Combine(_root, "sessions"),
            Logs: Path.Combine(_root, "logs"),
            Cache: Path.Combine(_root, "cache"),
            Exports: _exports,
            Config: Path.Combine(_root, "config"),
            Temp: Path.Combine(_root, "temp"),
            Support: Path.Combine(_root, "support"));
        DataPaths.Configure(_config);
    }

    public void Dispose()
    {
        DataPaths.ResetConfiguration();
        var parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Configure_RejectsNull()
    {
        var act = () => DataPaths.Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_ReplacesEveryPath_AndResetRestoresTheDefaults()
    {
        DataPaths.Sessions.Should().Be(_config.Sessions);
        DataPaths.Logs.Should().Be(_config.Logs);
        DataPaths.Cache.Should().Be(_config.Cache);
        DataPaths.Exports.Should().Be(_config.Exports);
        DataPaths.Config.Should().Be(_config.Config);
        DataPaths.Temp.Should().Be(_config.Temp);
        DataPaths.Support.Should().Be(_config.Support);
        DataPaths.Configuration.Should().BeSameAs(_config);

        DataPaths.ResetConfiguration();

        DataPaths.AppDataRoot.Should().NotBe(_root);
        DataPaths.AppDataRoot.Should().Contain("InControl");
        DataPaths.Configure(_config);
    }

    [Fact]
    public void Provider_ReportsTheConfiguredPaths()
    {
        IDataPathsProvider provider = new DataPathsProvider();

        provider.AppDataRoot.Should().Be(_root);
        provider.Sessions.Should().Be(_config.Sessions);
        provider.Logs.Should().Be(_config.Logs);
        provider.Cache.Should().Be(_config.Cache);
        provider.Exports.Should().Be(_config.Exports);
        provider.Config.Should().Be(_config.Config);
        provider.Temp.Should().Be(_config.Temp);
        provider.Support.Should().Be(_config.Support);
        provider.Configuration.Should().BeSameAs(_config);
        provider.IsPathAllowed(Path.Combine(_root, "x")).Should().BeTrue();
        provider.IsPathAllowed(Path.Combine(Path.GetTempPath(), "elsewhere")).Should().BeFalse();
    }

    [Fact]
    public void IsPathAllowed_AcceptsTheTwoRootsAndTheirChildren_ButNotTraversalOrSiblings()
    {
        DataPaths.IsPathAllowed(_root).Should().BeTrue();
        DataPaths.IsPathAllowed(Path.Combine(_root, "sessions", "a.json")).Should().BeTrue();
        DataPaths.IsPathAllowed(Path.Combine(_exports, "out.md")).Should().BeTrue();

        DataPaths.IsPathAllowed(Path.Combine(_root, "..", "other", "a.json")).Should().BeFalse();
        DataPaths.IsPathAllowed(Path.Combine(_root, "sessions", "..", "..", "app-evil", "a")).Should().BeFalse();
        DataPaths.IsPathAllowed(_root + "-evil").Should().BeFalse();
        DataPaths.IsPathAllowed(_exports + "2").Should().BeFalse();
        DataPaths.IsPathAllowed(Path.GetPathRoot(_root)!).Should().BeFalse();
    }

    [Theory]
    [InlineData("sessions", "Session data (conversations, messages)")]
    [InlineData("logs", "Application logs")]
    [InlineData("cache", "Cached data (model info, temporary results)")]
    [InlineData("config", "User configuration and settings")]
    [InlineData("temp", "Temporary files (cleared on startup)")]
    [InlineData("support", "Support bundles and diagnostics")]
    public void GetPathPurpose_DescribesEachFolder(string folder, string expected)
    {
        DataPaths.GetPathPurpose(Path.Combine(_root, folder, "file.dat")).Should().Be(expected);
        DataPaths.GetPathPurpose(Path.Combine(_root, folder)).Should().Be(expected);
    }

    [Fact]
    public void GetPathPurpose_ExportsAndTheRestOfTheRoot()
    {
        DataPaths.GetPathPurpose(Path.Combine(_exports, "a.md")).Should().Be("Exported sessions and data");
        DataPaths.GetPathPurpose(Path.Combine(_root, "unlisted", "a")).Should().Be("Application data");
        DataPaths.GetPathPurpose(Path.Combine(_root, "settings.json")).Should().Be("Application data");
        DataPaths.GetPathPurpose(Path.Combine(Path.GetTempPath(), "somewhere-else")).Should().Be("Unknown (outside allowed boundaries)");
    }

    [Fact]
    public void EnsureDirectoriesExist_CreatesEveryFolder_AndIsRepeatable()
    {
        DataPaths.EnsureDirectoriesExist();
        DataPaths.EnsureDirectoriesExist();

        foreach (var folder in new[]
        {
            _config.Sessions, _config.Logs, _config.Cache, _config.Exports,
            _config.Config, _config.Temp, _config.Support
        })
        {
            Directory.Exists(folder).Should().BeTrue(folder);
        }
    }

    [Fact]
    public void ClearTemp_RemovesFilesAndSubfolders_ButLeavesTheFolderAndOtherData()
    {
        DataPaths.EnsureDirectoriesExist();
        File.WriteAllText(Path.Combine(_config.Temp, "a.tmp"), "a");
        Directory.CreateDirectory(Path.Combine(_config.Temp, "nested", "deeper"));
        File.WriteAllText(Path.Combine(_config.Temp, "nested", "deeper", "b.tmp"), "b");
        File.WriteAllText(Path.Combine(_config.Sessions, "keep.json"), "{}");

        DataPaths.ClearTemp();

        Directory.Exists(_config.Temp).Should().BeTrue();
        Directory.GetFileSystemEntries(_config.Temp).Should().BeEmpty();
        File.Exists(Path.Combine(_config.Sessions, "keep.json")).Should().BeTrue();
    }

    [Fact]
    public void ClearTemp_WithNoTempFolder_DoesNothing()
    {
        var act = DataPaths.ClearTemp;

        act.Should().NotThrow();
        Directory.Exists(_config.Temp).Should().BeFalse();
    }

    [Fact]
    public void ClearTemp_SkipsAFileThatIsInUse_AndStillClearsTheRest()
    {
        DataPaths.EnsureDirectoriesExist();
        var busy = Path.Combine(_config.Temp, "busy.tmp");
        var free = Path.Combine(_config.Temp, "free.tmp");
        File.WriteAllText(busy, "x");
        File.WriteAllText(free, "x");
        using var held = new FileStream(busy, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var act = DataPaths.ClearTemp;

        act.Should().NotThrow();
        File.Exists(busy).Should().BeTrue();
        File.Exists(free).Should().BeFalse();
    }

    [Fact]
    public void GetStorageStats_SumsFileSizesPerFolder_IncludingSubfolders()
    {
        DataPaths.EnsureDirectoriesExist();
        File.WriteAllBytes(Path.Combine(_config.Sessions, "a.json"), new byte[100]);
        Directory.CreateDirectory(Path.Combine(_config.Sessions, "sub"));
        File.WriteAllBytes(Path.Combine(_config.Sessions, "sub", "b.json"), new byte[50]);
        File.WriteAllBytes(Path.Combine(_config.Logs, "app.log"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_config.Cache, "c"), new byte[1]);
        File.WriteAllBytes(Path.Combine(_config.Exports, "e"), new byte[2]);
        File.WriteAllBytes(Path.Combine(_config.Config, "s"), new byte[3]);
        File.WriteAllBytes(Path.Combine(_config.Temp, "t"), new byte[4]);
        File.WriteAllBytes(Path.Combine(_config.Support, "u"), new byte[5]);

        var stats = DataPaths.GetStorageStats();

        stats.SessionsSize.Should().Be(150);
        stats.LogsSize.Should().Be(10);
        stats.CacheSize.Should().Be(1);
        stats.ExportsSize.Should().Be(2);
        stats.ConfigSize.Should().Be(3);
        stats.TempSize.Should().Be(4);
        stats.SupportSize.Should().Be(5);
        stats.TotalSize.Should().Be(175);
        stats.TotalFormatted.Should().Be("175.0 B");
    }

    [Fact]
    public void GetStorageStats_MissingFolders_CountAsZero()
    {
        var stats = DataPaths.GetStorageStats();

        stats.TotalSize.Should().Be(0);
        stats.TotalFormatted.Should().Be("0.0 B");
    }

    [Theory]
    [InlineData(0L, "0.0 B")]
    [InlineData(1023L, "1023.0 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1.0 MB")]
    [InlineData(1073741824L, "1.0 GB")]
    [InlineData(1099511627776L, "1.0 TB")]
    [InlineData(1125899906842624L, "1024.0 TB")]
    public void FormatBytes_PicksTheLargestFittingUnit(long bytes, string expected)
    {
        StorageStats.FormatBytes(bytes).Should().Be(expected);
    }
}
