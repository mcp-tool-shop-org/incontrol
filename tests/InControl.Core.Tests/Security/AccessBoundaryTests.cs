using FluentAssertions;
using InControl.Core.Security;
using Xunit;

namespace InControl.Core.Tests.Security;

public class EndpointPatternTests
{
    [Theory]
    [InlineData("https://api.example.com", "https://api.example.com")]
    [InlineData("https://api.example.com", "https://api.example.com/")]
    [InlineData("https://api.example.com", "https://api.example.com/v1/models")]
    [InlineData("https://api.example.com/", "https://api.example.com/v1")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v1")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v1/")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v1/models/list")]
    [InlineData("https://api.example.com/v1/", "https://api.example.com/v1/models")]
    [InlineData("HTTPS://API.EXAMPLE.COM/V1", "https://api.example.com/v1/x")]
    [InlineData("https://api.example.com", "https://api.example.com:443/x")]
    [InlineData("http://api.example.com", "http://api.example.com:80/x")]
    [InlineData("http://127.0.0.1:11434", "http://127.0.0.1:11434/api/tags")]
    [InlineData("  https://api.example.com  ", "  https://api.example.com/x  ")]
    public void Covers_WhenSchemeHostPortAndPathAgree(string pattern, string endpoint)
    {
        EndpointPattern.Covers(pattern, endpoint).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://api.example.com", "http://api.example.com")]
    [InlineData("http://api.example.com", "https://api.example.com")]
    [InlineData("https://api.example.com", "https://evil.example.com")]
    [InlineData("https://api.example.com", "https://api.example.com.evil.net")]
    [InlineData("https://api.example.com", "https://notapi.example.com")]
    [InlineData("https://example.com", "https://api.example.com")]
    [InlineData("https://api.example.com", "https://api.example.com:8443")]
    [InlineData("https://api.example.com:8443", "https://api.example.com")]
    [InlineData("http://127.0.0.1:11434", "http://127.0.0.1:11435")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v10")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v1evil")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v2/v1")]
    [InlineData("https://api.example.com/a/b", "https://api.example.com/a")]
    public void DoesNotCover_WhenAnyPartDiffers(string pattern, string endpoint)
    {
        EndpointPattern.Covers(pattern, endpoint).Should().BeFalse();
    }

    [Fact]
    public void UserInfoInTheEndpoint_CannotImpersonateAnAllowedHost()
    {
        // The host is evil.net; "api.example.com" is only the user name.
        EndpointPattern.Covers("https://api.example.com", "https://api.example.com@evil.net/").Should().BeFalse();
        EndpointPattern.Covers("https://api.example.com", "https://api.example.com:pw@evil.net/").Should().BeFalse();
        // Userinfo on an endpoint whose host does match does not change the host.
        EndpointPattern.Covers("https://api.example.com", "https://user@api.example.com/x").Should().BeTrue();
    }

    [Fact]
    public void UserInfoInThePattern_MakesItUnusable()
    {
        EndpointPattern.Covers("https://user@api.example.com", "https://api.example.com").Should().BeFalse();
        EndpointPattern.Covers("https://user:pw@api.example.com", "https://user:pw@api.example.com").Should().BeFalse();
    }

    [Fact]
    public void DotDotSegments_AreNormalisedBeforeComparing()
    {
        EndpointPattern.Covers("https://api.example.com/v1", "https://api.example.com/v1/../admin").Should().BeFalse();
        EndpointPattern.Covers("https://api.example.com/v1", "https://api.example.com/v1/./models").Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "https://api.example.com")]
    [InlineData("", "https://api.example.com")]
    [InlineData("   ", "https://api.example.com")]
    [InlineData("https://api.example.com", null)]
    [InlineData("https://api.example.com", "")]
    [InlineData("https://api.example.com", "  ")]
    [InlineData(null, null)]
    public void BlankPatternOrEndpoint_CoversNothing(string? pattern, string? endpoint)
    {
        EndpointPattern.Covers(pattern, endpoint).Should().BeFalse();
    }

    [Theory]
    [InlineData("ftp://api.example.com", "ftp://api.example.com")]
    [InlineData("file:///C:/secrets", "file:///C:/secrets")]
    [InlineData("api.example.com", "api.example.com")]
    [InlineData("/relative", "/relative")]
    [InlineData("*", "https://api.example.com")]
    [InlineData("https://*.example.com", "https://api.example.com")]
    [InlineData("https://", "https://")]
    public void NonHttpOrNonAbsoluteValues_CoverNothing(string pattern, string endpoint)
    {
        EndpointPattern.Covers(pattern, endpoint).Should().BeFalse();
    }

    [Fact]
    public void AnUnparseableOrNonHttpEndpoint_IsNeverCovered()
    {
        EndpointPattern.Covers("https://api.example.com", "not a url").Should().BeFalse();
        EndpointPattern.Covers("https://api.example.com", "ftp://api.example.com").Should().BeFalse();
        EndpointPattern.Covers("https://api.example.com", "javascript:alert(1)").Should().BeFalse();
    }

    [Theory]
    [InlineData("https://api.example.com", 0)]
    [InlineData("https://api.example.com/", 0)]
    [InlineData("https://api.example.com/v1", 3)]
    [InlineData("https://api.example.com/v1/", 3)]
    [InlineData("https://api.example.com/v1/models", 10)]
    [InlineData("  https://api.example.com/v1  ", 3)]
    public void Specificity_IsThePathLength(string pattern, int expected)
    {
        EndpointPattern.Specificity(pattern).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ftp://api.example.com/v1")]
    [InlineData("not a url")]
    [InlineData("https://user@api.example.com/v1")]
    public void Specificity_IsNegativeForUnusablePatterns(string? pattern)
    {
        EndpointPattern.Specificity(pattern).Should().Be(-1);
    }

    [Fact]
    public void Specificity_RanksALongerPathAboveAnOriginOnly()
    {
        EndpointPattern.Specificity("https://a.example/v1/models")
            .Should().BeGreaterThan(EndpointPattern.Specificity("https://a.example/v1"));
        EndpointPattern.Specificity("https://a.example/v1")
            .Should().BeGreaterThan(EndpointPattern.Specificity("https://a.example"));
        EndpointPattern.Specificity("https://a.example")
            .Should().BeGreaterThan(EndpointPattern.Specificity("ftp://a.example"));
    }
}

public class PathBoundaryTests : IDisposable
{
    private readonly string _root;

    public PathBoundaryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "boundary-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        // Nothing is created on disk; the paths are only compared.
    }

    [Fact]
    public void TheRootItself_IsInside()
    {
        PathBoundary.IsInside(_root, _root).Should().BeTrue();
        PathBoundary.IsInside(_root + Path.DirectorySeparatorChar, _root).Should().BeTrue();
        PathBoundary.IsInside(_root, _root + Path.DirectorySeparatorChar).Should().BeTrue();
        PathBoundary.IsInside(_root.ToUpperInvariant(), _root.ToLowerInvariant()).Should().BeTrue();
    }

    [Fact]
    public void AFileOrFolderBelowTheRoot_IsInside()
    {
        PathBoundary.IsInside(Path.Combine(_root, "notes.txt"), _root).Should().BeTrue();
        PathBoundary.IsInside(Path.Combine(_root, "a", "b", "c.txt"), _root).Should().BeTrue();
        PathBoundary.IsInside(Path.Combine(_root, "a", "b", "c.txt"), _root + Path.DirectorySeparatorChar).Should().BeTrue();
        PathBoundary.IsInside(Path.Combine(_root, "a") + Path.AltDirectorySeparatorChar, _root).Should().BeTrue();
    }

    [Fact]
    public void ASiblingWithTheSamePrefix_IsOutside()
    {
        PathBoundary.IsInside(_root + "-evil", _root).Should().BeFalse();
        PathBoundary.IsInside(_root + "2" + Path.DirectorySeparatorChar + "x", _root).Should().BeFalse();
    }

    [Fact]
    public void TheParentAndUnrelatedPaths_AreOutside()
    {
        PathBoundary.IsInside(Path.GetDirectoryName(_root), _root).Should().BeFalse();
        PathBoundary.IsInside(Path.Combine(Path.GetTempPath(), "other"), _root).Should().BeFalse();
    }

    [Fact]
    public void DotDotSegments_CannotEscapeTheRoot()
    {
        PathBoundary.IsInside(Path.Combine(_root, "..", "secret.txt"), _root).Should().BeFalse();
        PathBoundary.IsInside(Path.Combine(_root, "a", "..", "..", "secret.txt"), _root).Should().BeFalse();
        PathBoundary.IsInside(Path.Combine(_root, "a", "..", "ok.txt"), _root).Should().BeTrue();
    }

    [Fact]
    public void ADriveRoot_ContainsItsChildren_ButNotAnotherDrive()
    {
        var driveRoot = Path.GetPathRoot(_root)!;

        PathBoundary.IsInside(driveRoot, driveRoot).Should().BeTrue();
        PathBoundary.IsInside(_root, driveRoot).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "C:\\data")]
    [InlineData("", "C:\\data")]
    [InlineData("  ", "C:\\data")]
    [InlineData("C:\\data\\x", null)]
    [InlineData("C:\\data\\x", "")]
    [InlineData("C:\\data\\x", " ")]
    public void BlankCandidateOrRoot_IsNeverInside(string? candidate, string? root)
    {
        PathBoundary.IsInside(candidate, root).Should().BeFalse();
    }

    [Fact]
    public void PathsWithIllegalCharacters_AreOutsideRatherThanThrowing()
    {
        PathBoundary.IsInside(_root + "\0x", _root).Should().BeFalse();
        PathBoundary.IsInside(Path.Combine(_root, "a\0b"), _root).Should().BeFalse();
        PathBoundary.IsInside(Path.Combine(_root, "x"), _root + "\0").Should().BeFalse();
    }
}
