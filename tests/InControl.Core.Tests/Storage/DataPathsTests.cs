using FluentAssertions;
using InControl.Core.Storage;
using Xunit;

namespace InControl.Core.Tests.Storage;

public class DataPathsTests
{
    [Fact]
    public void AppDataRoot_IsUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        DataPaths.AppDataRoot.Should().StartWith(localAppData);
        DataPaths.AppDataRoot.Should().Contain("InControl");
    }

    [Fact]
    public void Sessions_IsUnderAppDataRoot()
    {
        DataPaths.Sessions.Should().StartWith(DataPaths.AppDataRoot);
        DataPaths.Sessions.Should().EndWith("sessions");
    }

    [Fact]
    public void Logs_IsUnderAppDataRoot()
    {
        DataPaths.Logs.Should().StartWith(DataPaths.AppDataRoot);
        DataPaths.Logs.Should().EndWith("logs");
    }

    [Fact]
    public void Cache_IsUnderAppDataRoot()
    {
        DataPaths.Cache.Should().StartWith(DataPaths.AppDataRoot);
        DataPaths.Cache.Should().EndWith("cache");
    }

    [Fact]
    public void Exports_IsUnderDocuments()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        DataPaths.Exports.Should().StartWith(documents);
        DataPaths.Exports.Should().Contain("InControl");
        DataPaths.Exports.Should().EndWith("exports");
    }

    [Fact]
    public void Config_IsUnderAppDataRoot()
    {
        DataPaths.Config.Should().StartWith(DataPaths.AppDataRoot);
        DataPaths.Config.Should().EndWith("config");
    }

    [Fact]
    public void Temp_IsUnderAppDataRoot()
    {
        DataPaths.Temp.Should().StartWith(DataPaths.AppDataRoot);
        DataPaths.Temp.Should().EndWith("temp");
    }

    [Fact]
    public void Support_IsUnderAppDataRoot()
    {
        DataPaths.Support.Should().StartWith(DataPaths.AppDataRoot);
        DataPaths.Support.Should().EndWith("support");
    }

    [Fact]
    public void IsPathAllowed_ReturnsTrueForAppDataPaths()
    {
        DataPaths.IsPathAllowed(DataPaths.Sessions).Should().BeTrue();
        DataPaths.IsPathAllowed(DataPaths.Logs).Should().BeTrue();
        DataPaths.IsPathAllowed(DataPaths.Cache).Should().BeTrue();
        DataPaths.IsPathAllowed(DataPaths.Config).Should().BeTrue();
    }

    [Fact]
    public void IsPathAllowed_ReturnsTrueForExportPaths()
    {
        DataPaths.IsPathAllowed(DataPaths.Exports).Should().BeTrue();
    }

    [Fact]
    public void IsPathAllowed_ReturnsFalseForSystemPaths()
    {
        DataPaths.IsPathAllowed(@"C:\Windows\System32").Should().BeFalse();
        DataPaths.IsPathAllowed(@"C:\Program Files").Should().BeFalse();
    }

    [Fact]
    public void IsPathAllowed_ReturnsFalseForNullOrEmpty()
    {
        DataPaths.IsPathAllowed(null!).Should().BeFalse();
        DataPaths.IsPathAllowed(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void GetPathPurpose_ReturnsCorrectDescriptionForSessions()
    {
        var purpose = DataPaths.GetPathPurpose(DataPaths.Sessions);

        purpose.Should().Contain("Session");
    }

    [Fact]
    public void GetPathPurpose_ReturnsCorrectDescriptionForLogs()
    {
        var purpose = DataPaths.GetPathPurpose(DataPaths.Logs);

        purpose.Should().Contain("log");
    }

    [Fact]
    public void GetPathPurpose_ReturnsCorrectDescriptionForCache()
    {
        var purpose = DataPaths.GetPathPurpose(DataPaths.Cache);

        purpose.Should().Contain("Cache");
    }

    [Fact]
    public void GetPathPurpose_ReturnsCorrectDescriptionForExports()
    {
        var purpose = DataPaths.GetPathPurpose(DataPaths.Exports);

        purpose.Should().Contain("Export");
    }

    [Fact]
    public void GetPathPurpose_ReturnsUnknownForInvalidPaths()
    {
        var purpose = DataPaths.GetPathPurpose(@"C:\Windows\System32");

        purpose.Should().Contain("Unknown");
    }

    [Fact]
    public void GetPathPurpose_ReturnsUnknownForNullOrEmpty()
    {
        DataPaths.GetPathPurpose(null!).Should().Be("Unknown");
        DataPaths.GetPathPurpose(string.Empty).Should().Be("Unknown");
    }

    [Fact]
    public void Configuration_ReturnsAllPaths()
    {
        var config = DataPaths.Configuration;

        config.Should().NotBeNull();
        config.AppDataRoot.Should().NotBeNullOrEmpty();
        config.Sessions.Should().NotBeNullOrEmpty();
        config.Logs.Should().NotBeNullOrEmpty();
        config.Cache.Should().NotBeNullOrEmpty();
        config.Exports.Should().NotBeNullOrEmpty();
        config.Config.Should().NotBeNullOrEmpty();
        config.Temp.Should().NotBeNullOrEmpty();
        config.Support.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetStorageStats_ReturnsValidStats()
    {
        var stats = DataPaths.GetStorageStats();

        stats.Should().NotBeNull();
        stats.SessionsSize.Should().BeGreaterThanOrEqualTo(0);
        stats.LogsSize.Should().BeGreaterThanOrEqualTo(0);
        stats.CacheSize.Should().BeGreaterThanOrEqualTo(0);
        stats.TotalSize.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void StorageStats_TotalFormatted_ReturnsReadableString()
    {
        var stats = DataPaths.GetStorageStats();

        stats.TotalFormatted.Should().NotBeNullOrEmpty();
        stats.TotalFormatted.Should().MatchRegex(@"^\d+(\.\d)?\s+(B|KB|MB|GB|TB)$");
    }

    [Fact]
    public void StorageStats_FormatBytes_FormatsCorrectly()
    {
        StorageStats.FormatBytes(0).Should().Be("0.0 B");
        StorageStats.FormatBytes(512).Should().Be("512.0 B");
        StorageStats.FormatBytes(1024).Should().Be("1.0 KB");
        StorageStats.FormatBytes(1024 * 1024).Should().Be("1.0 MB");
        StorageStats.FormatBytes(1024 * 1024 * 1024).Should().Be("1.0 GB");
    }

    [Fact]
    public void AllPaths_AreAbsolute()
    {
        Path.IsPathRooted(DataPaths.AppDataRoot).Should().BeTrue();
        Path.IsPathRooted(DataPaths.Sessions).Should().BeTrue();
        Path.IsPathRooted(DataPaths.Logs).Should().BeTrue();
        Path.IsPathRooted(DataPaths.Cache).Should().BeTrue();
        Path.IsPathRooted(DataPaths.Exports).Should().BeTrue();
        Path.IsPathRooted(DataPaths.Config).Should().BeTrue();
        Path.IsPathRooted(DataPaths.Temp).Should().BeTrue();
        Path.IsPathRooted(DataPaths.Support).Should().BeTrue();
    }
}
