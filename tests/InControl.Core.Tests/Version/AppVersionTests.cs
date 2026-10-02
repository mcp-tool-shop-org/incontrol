using FluentAssertions;
using InControl.Core.Version;
using Xunit;

namespace InControl.Core.Tests.Version;

public class AppVersionTests
{
    [Fact]
    public void Full_ReturnsNonEmptyString()
    {
        AppVersion.Full.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SemVer_ReturnsValidFormat()
    {
        var semVer = AppVersion.SemVer;
        semVer.Should().NotBeNullOrEmpty();
        semVer.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }

    [Fact]
    public void Major_IsNonNegative()
    {
        AppVersion.Major.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Minor_IsNonNegative()
    {
        AppVersion.Minor.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Patch_IsNonNegative()
    {
        AppVersion.Patch.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ProductName_ReturnsInControl()
    {
        AppVersion.ProductName.Should().Contain("InControl");
    }

    [Fact]
    public void Configuration_ReturnsDebugOrRelease()
    {
        AppVersion.Configuration.Should().BeOneOf("Debug", "Release");
    }

    [Fact]
    public void Info_ReturnsCompleteVersionInfo()
    {
        var info = AppVersion.Info;

        info.Should().NotBeNull();
        info.Full.Should().NotBeNullOrEmpty();
        info.SemVer.Should().NotBeNullOrEmpty();
        info.ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VersionInfo_DisplayString_FormatsCorrectly()
    {
        var info = AppVersion.Info;

        info.DisplayString.Should().Contain(info.ProductName);
        info.DisplayString.Should().Contain(info.Full);
    }

    [Fact]
    public void VersionInfo_ShortDisplay_StartsWithV()
    {
        var info = AppVersion.Info;

        info.ShortDisplay.Should().StartWith("v");
        info.ShortDisplay.Should().Contain(info.Full);
    }

    [Fact]
    public void IsPrerelease_ReflectsPrereleaseTag()
    {
        // When prerelease is not empty, IsPrerelease should be true
        if (!string.IsNullOrEmpty(AppVersion.Prerelease))
        {
            AppVersion.IsPrerelease.Should().BeTrue();
        }
        else
        {
            AppVersion.IsPrerelease.Should().BeFalse();
        }
    }

    [Fact]
    public void Version_IsCachedLazily()
    {
        // Calling Info multiple times should return the same instance
        var info1 = AppVersion.Info;
        var info2 = AppVersion.Info;

        ReferenceEquals(info1, info2).Should().BeTrue();
    }
}
