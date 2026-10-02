using FluentAssertions;
using Xunit;

namespace InControl.Core.Tests.Packaging;

public class PackageIdentityTests
{
    [Fact]
    public void PartnerCenterPackage_KeepsInControlApp_AndAdvancesPast_1_4_0()
    {
        var root = RepoRoot();

        var manifest = File.ReadAllText(Path.Combine(root, "src", "InControl.App", "Package.appxmanifest"));
        manifest.Should().Contain("Name=\"InControl.App\"");
        manifest.Should().Contain("Version=\"2.0.0.0\"");
        manifest.Should().Contain("${PUBLISHER}");
        manifest.Should().NotContain("InControl.Desktop");

        var template = File.ReadAllText(Path.Combine(root, "packaging", "AppxManifest.template.xml"));
        template.Should().Contain("Name=\"InControl.App\"");
        template.Should().Contain("${VERSION}");
        template.Should().Contain("${PUBLISHER}");
        template.Should().NotContain("InControl.Desktop");

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

        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release-signed.yml"));
        workflow.Should().Contain("InControl.App_");
        workflow.Should().Contain("1.4.0.0");
        workflow.Should().Contain("Name=\"InControl\\.App\"");
        workflow.Should().NotContain("InControl-Desktop");
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
