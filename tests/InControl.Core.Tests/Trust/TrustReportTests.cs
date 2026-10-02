using FluentAssertions;
using InControl.Core.State;
using InControl.Core.Trust;
using Xunit;

namespace InControl.Core.Tests.Trust;

public class TrustReportTests
{
    [Fact]
    public void Generate_CreatesCompleteReport()
    {
        var security = SecurityConfig.Default();

        var report = TrustReport.Generate(security);

        report.Build.Should().NotBeNull();
        report.Runtime.Should().NotBeNull();
        report.Security.Should().NotBeNull();
        report.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TrustLevel_IsHigh_WhenAllSecurityEnabled()
    {
        var security = new SecurityConfig
        {
            PathBoundaryEnforced = true,
            InferenceIsolated = true,
            AllowedDataPaths = new[] { "/data" }
        };

        var report = new TrustReport
        {
            Build = new BuildInfo
            {
                Version = "1.0.0",
                InformationalVersion = "1.0.0",
                Configuration = "Release",
                TargetFramework = "net9.0"
            },
            Runtime = RuntimeInfo.Current(),
            Security = security
        };

        // TrustLevel is based on the BuildInfo.Configuration we provide, not the actual build mode
        // Since we're passing Configuration = "Release", it should always be High
        report.TrustLevel.Should().Be(TrustLevel.High);
    }

    [Fact]
    public void TrustLevel_IsLow_WhenMultipleSecurityDisabled()
    {
        var security = new SecurityConfig
        {
            PathBoundaryEnforced = false,
            InferenceIsolated = false,
            AllowedDataPaths = Array.Empty<string>()
        };

        var report = new TrustReport
        {
            Build = new BuildInfo
            {
                Version = "1.0.0",
                InformationalVersion = "1.0.0",
                Configuration = "Debug",
                TargetFramework = "net9.0"
            },
            Runtime = RuntimeInfo.Current(),
            Security = security
        };

        // 4 issues: debug build, no path boundary, no inference isolation, no allowed paths
        report.TrustLevel.Should().Be(TrustLevel.Low);
    }

    [Fact]
    public void TrustSummary_ReflectsTrustLevel()
    {
        var security = SecurityConfig.Default() with { AllowedDataPaths = new[] { "/data" } };
        var report = TrustReport.Generate(security);

        report.TrustSummary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToJson_SerializesReport()
    {
        var security = SecurityConfig.Default();
        var report = TrustReport.Generate(security);

        var json = report.ToJson();

        json.Should().Contain("build");
        json.Should().Contain("runtime");
        json.Should().Contain("security");
    }

    [Fact]
    public void ToJson_Compact_ProducesMinimalJson()
    {
        var security = SecurityConfig.Default();
        var report = TrustReport.Generate(security);

        var normal = report.ToJson(compact: false);
        var compact = report.ToJson(compact: true);

        compact.Length.Should().BeLessThan(normal.Length);
        compact.Should().NotContain("\n");
    }

    [Fact]
    public void TrustReport_IsSerializable()
    {
        var security = SecurityConfig.Default() with
        {
            AllowedDataPaths = new[] { "/data", "/backup" },
            SecurityNotes = new[] { "Note 1" }
        };
        var report = TrustReport.Generate(security);

        var json = StateSerializer.Serialize(report);
        var result = StateSerializer.Deserialize<TrustReport>(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Security.AllowedDataPaths.Should().HaveCount(2);
        result.Value.Security.SecurityNotes.Should().Contain("Note 1");
    }
}

public class RuntimeInfoTests
{
    [Fact]
    public void Current_ReturnsValidInfo()
    {
        var info = RuntimeInfo.Current();

        info.Framework.Should().NotBeNullOrEmpty();
        info.OperatingSystem.Should().NotBeNullOrEmpty();
        info.Architecture.Should().NotBeNullOrEmpty();
        info.MachineName.Should().NotBeNullOrEmpty();
        info.UserName.Should().NotBeNullOrEmpty();
        info.ProcessorCount.Should().BeGreaterThan(0);
        info.ProcessId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Current_IncludesProcessInfo()
    {
        var info = RuntimeInfo.Current();

        info.Is64BitProcess.Should().Be(Environment.Is64BitProcess);
        info.ProcessStartTime.Should().BeBefore(DateTimeOffset.UtcNow);
    }
}

public class SecurityConfigTests
{
    [Fact]
    public void Default_HasSecureSettings()
    {
        var config = SecurityConfig.Default();

        config.PathBoundaryEnforced.Should().BeTrue();
        config.InferenceIsolated.Should().BeTrue();
        config.TelemetryEnabled.Should().BeFalse();
    }

    [Fact]
    public void AllowedDataPaths_DefaultsToEmpty()
    {
        var config = SecurityConfig.Default();

        config.AllowedDataPaths.Should().BeEmpty();
    }

    [Fact]
    public void SecurityNotes_CanBeSet()
    {
        var config = SecurityConfig.Default() with
        {
            SecurityNotes = new[] { "Warning: Debug mode", "External access enabled" }
        };

        config.SecurityNotes.Should().HaveCount(2);
    }
}
