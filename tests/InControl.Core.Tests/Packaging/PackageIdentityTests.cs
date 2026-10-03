using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace InControl.Core.Tests.Packaging;

public class PackageIdentityTests
{
    private const string Name = "mcp-tool-shop.InControl-Desktop";
    private const string Publisher = "CN=5305D976-6952-4F00-9C21-3A5DB090359F";

    // These are the Partner Center Product Identity values for Store ID 9N1FG39JWF83.
    // A package that differs in any of them is rejected on upload.
    [Theory]
    [InlineData("src/InControl.App/Package.appxmanifest")]
    [InlineData("packaging/AppxManifest.template.xml")]
    public void Manifest_MatchesTheStoreIdentity(string relativePath)
    {
        var manifest = File.ReadAllText(Path.Combine(RepoRoot(), relativePath));

        manifest.Should().Contain($"Name=\"{Name}\"");
        IdentityPublisher(manifest).Should().Be(Publisher);
        manifest.Should().Contain("<PublisherDisplayName>mcp-tool-shop</PublisherDisplayName>");
        manifest.Should().Contain("<DisplayName>InControl-Desktop</DisplayName>");
        manifest.Should().Contain("Id=\"App\"");
        manifest.Should().Contain("MinVersion=\"10.0.19041.0\"");
        manifest.Should().NotContain("Name=\"InControl.App\"");
    }

    [Fact]
    public void Versions_AdvancePast_1_4_0_AndStayOnWindows10()
    {
        var root = RepoRoot();

        File.ReadAllText(Path.Combine(root, "src", "InControl.App", "Package.appxmanifest"))
            .Should().Contain("Version=\"2.0.0.0\"");
        File.ReadAllText(Path.Combine(root, "packaging", "AppxManifest.template.xml"))
            .Should().Contain("${VERSION}");

        File.ReadAllText(Path.Combine(root, "src", "InControl.Core", "InControl.Core.csproj"))
            .Should().Contain("<Version>1.2.2</Version>");
        File.ReadAllText(Path.Combine(root, "src", "InControl.Inference", "InControl.Inference.csproj"))
            .Should().Contain("<Version>1.0.2</Version>");

        var csproj = File.ReadAllText(Path.Combine(root, "src", "InControl.App", "InControl.App.csproj"));
        csproj.Should().Contain("<Version>2.0.0</Version>");
        csproj.Should().Contain("<AssemblyVersion>2.0.0.0</AssemblyVersion>");
        csproj.Should().Contain("<FileVersion>2.0.0.0</FileVersion>");
        csproj.Should().Contain("<InformationalVersion>2.0.0</InformationalVersion>");
        csproj.Should().Contain("<AppxAutoIncrementPackageRevision>false</AppxAutoIncrementPackageRevision>");
        // Without this the build writes the SDK version, 22621, as the package MinVersion.
        csproj.Should().Contain("<TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>");
        // KokoroSharp's voices and eSpeak data only reach the package as content.
        csproj.Should().Contain(@"$(PkgKokoroSharp)\content\**\*");

        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release-signed.yml"));
        workflow.Should().Contain($"{Name}_");
        workflow.Should().Contain("1.4.0.0");
        workflow.Should().Contain(@"Name=""mcp-tool-shop\.InControl-Desktop""");
        workflow.Should().NotContain(@"InControl\.App""");
    }

    private static string IdentityPublisher(string xml)
    {
        var match = Regex.Match(
            xml,
            @"<Identity\b(?:(?!>).)*?\bPublisher\s*=\s*""([^""]*)""",
            RegexOptions.Singleline);
        match.Success.Should().BeTrue();
        return match.Groups[1].Value;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InControl.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find InControl.sln above the test output.");
    }
}
