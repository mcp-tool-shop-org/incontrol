using FluentAssertions;
using InControl.Core.Diagnostics;
using Xunit;

namespace InControl.Core.Tests.Diagnostics;

public class DiagnosticsInfoTests
{
    [Fact]
    public void GetReport_ReturnsCompleteReport()
    {
        var report = DiagnosticsInfo.GetReport();

        report.Should().NotBeNull();
        report.Application.Should().NotBeNull();
        report.Runtime.Should().NotBeNull();
        report.Host.Should().NotBeNull();
        report.Storage.Should().NotBeNull();
        report.CollectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetReport_ApplicationInfo_ContainsVersion()
    {
        var report = DiagnosticsInfo.GetReport();

        report.Application.Version.Should().NotBeNullOrEmpty();
        report.Application.Configuration.Should().BeOneOf("Debug", "Release");
        report.Application.ProductName.Should().Contain("InControl");
    }

    [Fact]
    public void GetReport_RuntimeInfo_ContainsFrameworkVersion()
    {
        var report = DiagnosticsInfo.GetReport();

        report.Runtime.FrameworkVersion.Should().NotBeNullOrEmpty();
        report.Runtime.FrameworkVersion.Should().Contain(".NET");
        report.Runtime.RuntimeIdentifier.Should().NotBeNullOrEmpty();
        report.Runtime.ProcessArchitecture.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetReport_SystemInfo_ContainsOSDescription()
    {
        var report = DiagnosticsInfo.GetReport();

        report.Host.OSDescription.Should().NotBeNullOrEmpty();
        report.Host.OSArchitecture.Should().NotBeNullOrEmpty();
        report.Host.ProcessorCount.Should().BeGreaterThan(0);
        report.Host.MachineName.Should().NotBeNullOrEmpty();
        report.Host.WorkingSet.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetReport_StorageInfo_ContainsPaths()
    {
        var report = DiagnosticsInfo.GetReport();

        report.Storage.AppDataRoot.Should().NotBeNullOrEmpty();
        report.Storage.SessionsPath.Should().NotBeNullOrEmpty();
        report.Storage.LogsPath.Should().NotBeNullOrEmpty();
        report.Storage.CachePath.Should().NotBeNullOrEmpty();
        report.Storage.ExportsPath.Should().NotBeNullOrEmpty();
        report.Storage.TotalStorageUsed.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetReport_ToJson_ProducesValidJson()
    {
        var report = DiagnosticsInfo.GetReport();

        var json = report.ToJson();

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("application");
        json.Should().Contain("runtime");
        json.Should().Contain("host");
        json.Should().Contain("storage");
        json.Should().Contain("collectedAt");
    }

    [Fact]
    public void GetCompactDiagnostics_ReturnsFormattedString()
    {
        var diagnostics = DiagnosticsInfo.GetCompactDiagnostics();

        diagnostics.Should().NotBeNullOrEmpty();
        diagnostics.Should().Contain("InControl");
        diagnostics.Should().Contain("Version:");
        diagnostics.Should().Contain("Runtime:");
        diagnostics.Should().Contain("Platform:");
        diagnostics.Should().Contain("Collected:");
    }

    [Fact]
    public void GetCompactDiagnostics_DoesNotContainStackTraces()
    {
        var diagnostics = DiagnosticsInfo.GetCompactDiagnostics();

        diagnostics.Should().NotContain("StackTrace");
        diagnostics.Should().NotContain("at ");
        diagnostics.Should().NotContain("Exception");
    }

    [Fact]
    public void GetCompactDiagnostics_DoesNotContainSensitiveInfo()
    {
        var diagnostics = DiagnosticsInfo.GetCompactDiagnostics().ToLowerInvariant();

        // Should not contain common sensitive patterns
        diagnostics.Should().NotContain("password");
        diagnostics.Should().NotContain("token");
        diagnostics.Should().NotContain("secret");
        diagnostics.Should().NotContain("apikey");
    }
}

public class OllamaConnectivityTests
{
    [Fact]
    public async Task CheckOllamaAsync_WithUnreachableHost_ReturnsFalse()
    {
        // Use a port that's almost certainly not running Ollama
        var result = await DiagnosticsInfo.CheckOllamaAsync("http://127.0.0.1:19999");

        result.Should().NotBeNull();
        result.Reachable.Should().BeFalse();
        result.BaseUrl.Should().Be("http://127.0.0.1:19999");
        result.Error.Should().NotBeNullOrEmpty();
        result.VersionResponse.Should().BeNull();
    }

    [Fact]
    public async Task CheckOllamaAsync_DefaultUrl_ReturnsResult()
    {
        // Should not throw regardless of whether Ollama is running
        var result = await DiagnosticsInfo.CheckOllamaAsync();

        result.Should().NotBeNull();
        result.BaseUrl.Should().Be("http://localhost:11434");
        // Either reachable or has error — both are valid
        if (result.Reachable)
        {
            result.VersionResponse.Should().NotBeNullOrEmpty();
            result.Error.Should().BeNull();
        }
        else
        {
            result.Error.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void OllamaConnectivityInfo_RecordEquality()
    {
        var a = new OllamaConnectivityInfo(true, "http://localhost:11434", """{"version":"0.5.0"}""", null);
        var b = new OllamaConnectivityInfo(true, "http://localhost:11434", """{"version":"0.5.0"}""", null);

        a.Should().Be(b);
    }
}

public class SupportBundleOptionsTests
{
    [Fact]
    public void Default_IncludesLogsHealthAndConfig()
    {
        var options = SupportBundleOptions.Default;

        options.IncludeLogs.Should().BeTrue();
        options.IncludeHealthReport.Should().BeTrue();
        options.IncludeConfig.Should().BeTrue();
        options.IncludeSessionMetadata.Should().BeFalse();
    }

    [Fact]
    public void Full_IncludesEverything()
    {
        var options = SupportBundleOptions.Full;

        options.IncludeLogs.Should().BeTrue();
        options.IncludeHealthReport.Should().BeTrue();
        options.IncludeConfig.Should().BeTrue();
        options.IncludeSessionMetadata.Should().BeTrue();
    }

    [Fact]
    public void Minimal_IncludesOnlyDiagnostics()
    {
        var options = SupportBundleOptions.Minimal;

        options.IncludeLogs.Should().BeFalse();
        options.IncludeHealthReport.Should().BeFalse();
        options.IncludeConfig.Should().BeFalse();
        options.IncludeSessionMetadata.Should().BeFalse();
    }
}

public class SupportBundleTests
{
    [Fact]
    public async Task CreateAsync_CreatesZipFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-bundle-{Guid.NewGuid()}.zip");

        try
        {
            var result = await SupportBundle.CreateAsync(tempPath, SupportBundleOptions.Minimal);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.BundlePath.Should().Be(tempPath);
            File.Exists(tempPath).Should().BeTrue();
            result.IncludedFiles.Should().Contain("diagnostics.json");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task CreateAsync_IncludesDiagnosticsJson()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-bundle-{Guid.NewGuid()}.zip");

        try
        {
            var result = await SupportBundle.CreateAsync(tempPath, SupportBundleOptions.Minimal);

            result.IncludedFiles.Should().Contain("diagnostics.json");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task CreateAsync_WithDefaultOptions_IncludesMultipleFiles()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-bundle-{Guid.NewGuid()}.zip");

        try
        {
            var result = await SupportBundle.CreateAsync(tempPath, SupportBundleOptions.Default);

            result.Success.Should().BeTrue();
            result.IncludedFiles.Should().Contain("diagnostics.json");
            result.IncludedFiles.Should().Contain("health.json");
            result.IncludedFiles.Should().Contain("config.json");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedTimestamp()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-bundle-{Guid.NewGuid()}.zip");
        var before = DateTimeOffset.UtcNow;

        try
        {
            var result = await SupportBundle.CreateAsync(tempPath, SupportBundleOptions.Minimal);

            result.CreatedAt.Should().BeOnOrAfter(before);
            result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
