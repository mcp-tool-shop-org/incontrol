using FluentAssertions;
using InControl.Core.Trust;
using Xunit;

namespace InControl.Core.Tests.Trust;

public class BuildInfoTests
{
    [Fact]
    public void FromEntryAssembly_ReturnsValidBuildInfo()
    {
        var info = BuildInfo.FromEntryAssembly();

        info.Version.Should().NotBeNullOrEmpty();
        info.InformationalVersion.Should().NotBeNullOrEmpty();
        info.Configuration.Should().BeOneOf("Debug", "Release");
        info.TargetFramework.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FromAssembly_ExtractsCommitHash_WhenPresent()
    {
        // The test assembly may not have a commit hash, but we can test the format
        var info = BuildInfo.FromEntryAssembly();

        // If a commit hash is present, it should be a valid format
        if (info.CommitHash is not null)
        {
            info.CommitHash.Should().NotBeNullOrEmpty();
            info.InformationalVersion.Should().Contain("+");
        }
    }

    [Fact]
    public void IsDebugBuild_ReflectsConfiguration()
    {
        var info = BuildInfo.FromEntryAssembly();

#if DEBUG
        info.Configuration.Should().Be("Debug");
        info.IsDebugBuild.Should().BeTrue();
#else
        info.Configuration.Should().Be("Release");
        info.IsDebugBuild.Should().BeFalse();
#endif
    }

    [Fact]
    public void ToString_ReturnsCompactFormat()
    {
        var info = new BuildInfo
        {
            Version = "1.0.0.0",
            InformationalVersion = "1.0.0+abc1234",
            CommitHash = "abc1234567890",
            Configuration = "Release",
            TargetFramework = ".NETCoreApp,Version=v9.0"
        };

        var result = info.ToString();

        result.Should().Contain("InControl v1.0.0.0");
        result.Should().Contain("abc1234"); // First 7 chars of hash
        result.Should().Contain("[Release]");
    }

    [Fact]
    public void ToString_OmitsHash_WhenNotPresent()
    {
        var info = new BuildInfo
        {
            Version = "1.0.0.0",
            InformationalVersion = "1.0.0",
            CommitHash = null,
            Configuration = "Debug",
            TargetFramework = ".NETCoreApp,Version=v9.0"
        };

        var result = info.ToString();

        result.Should().Be("InControl v1.0.0.0 [Debug]");
    }

    [Fact]
    public void BuildTimestamp_CanBeSet()
    {
        var timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var info = new BuildInfo
        {
            Version = "1.0.0",
            InformationalVersion = "1.0.0",
            Configuration = "Release",
            TargetFramework = "net9.0",
            BuildTimestamp = timestamp
        };

        info.BuildTimestamp.Should().Be(timestamp);
    }
}
