using FluentAssertions;
using InControl.Core.State;
using InControl.Services.Health;
using Xunit;

namespace InControl.Services.Tests.Health;

public class HealthReportTests
{
    [Fact]
    public void Create_WithAllHealthy_ReportsHealthy()
    {
        var probes = new[]
        {
            HealthProbeResult.Healthy("Probe1", "Cat1"),
            HealthProbeResult.Healthy("Probe2", "Cat2")
        };

        var report = HealthReport.Create(probes, TimeSpan.FromMilliseconds(100));

        report.OverallStatus.Should().Be(HealthStatus.Healthy);
        report.IsHealthy.Should().BeTrue();
        report.Probes.Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithOneDegraded_ReportsDegraded()
    {
        var probes = new[]
        {
            HealthProbeResult.Healthy("Probe1", "Cat1"),
            HealthProbeResult.Degraded("Probe2", "Cat2", "Slow")
        };

        var report = HealthReport.Create(probes, TimeSpan.FromMilliseconds(100));

        report.OverallStatus.Should().Be(HealthStatus.Degraded);
        report.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void Create_WithOneUnhealthy_ReportsUnhealthy()
    {
        var probes = new[]
        {
            HealthProbeResult.Healthy("Probe1", "Cat1"),
            HealthProbeResult.Degraded("Probe2", "Cat2", "Slow"),
            HealthProbeResult.Unhealthy("Probe3", "Cat3", "Down")
        };

        var report = HealthReport.Create(probes, TimeSpan.FromMilliseconds(100));

        report.OverallStatus.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Degradations_ReturnsOnlyNonHealthy()
    {
        var probes = new[]
        {
            HealthProbeResult.Healthy("Probe1", "Cat1"),
            HealthProbeResult.Degraded("Probe2", "Cat2", "Slow"),
            HealthProbeResult.Unhealthy("Probe3", "Cat3", "Down")
        };

        var report = HealthReport.Create(probes, TimeSpan.FromMilliseconds(100));

        report.Degradations.Should().HaveCount(2);
        report.Degradations.Should().Contain(p => p.Name == "Probe2");
        report.Degradations.Should().Contain(p => p.Name == "Probe3");
    }

    [Fact]
    public void ByCategory_GroupsProbes()
    {
        var probes = new[]
        {
            HealthProbeResult.Healthy("Probe1", "Inference"),
            HealthProbeResult.Healthy("Probe2", "Inference"),
            HealthProbeResult.Healthy("Probe3", "Storage")
        };

        var report = HealthReport.Create(probes, TimeSpan.FromMilliseconds(100));

        report.ByCategory.Should().HaveCount(2);
        report.ByCategory["Inference"].Should().HaveCount(2);
        report.ByCategory["Storage"].Should().HaveCount(1);
    }

    [Fact]
    public void Empty_ReturnsHealthyReport()
    {
        var report = HealthReport.Empty();

        report.OverallStatus.Should().Be(HealthStatus.Healthy);
        report.IsHealthy.Should().BeTrue();
        report.Probes.Should().BeEmpty();
        report.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Create_WithEmptyProbes_ReportsHealthy()
    {
        var report = HealthReport.Create(
            Array.Empty<HealthProbeResult>(),
            TimeSpan.FromMilliseconds(10));

        report.OverallStatus.Should().Be(HealthStatus.Healthy);
        report.Probes.Should().BeEmpty();
    }

    [Fact]
    public void Duration_IsPreserved()
    {
        var duration = TimeSpan.FromMilliseconds(250);

        var report = HealthReport.Create(
            new[] { HealthProbeResult.Healthy("Test", "Cat") },
            duration);

        report.Duration.Should().Be(duration);
    }

    [Fact]
    public void HealthReport_IsSerializable()
    {
        var probes = new[]
        {
            HealthProbeResult.Healthy("Probe1", "Cat1", "OK") with
            {
                Duration = TimeSpan.FromMilliseconds(10),
                Properties = new Dictionary<string, object> { ["Key"] = "Value" }
            },
            HealthProbeResult.Degraded("Probe2", "Cat2", "Slow", "Check it")
        };
        var report = HealthReport.Create(probes, TimeSpan.FromMilliseconds(50));

        var json = StateSerializer.Serialize(report);
        var result = StateSerializer.Deserialize<HealthReport>(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.OverallStatus.Should().Be(report.OverallStatus);
        result.Value.Probes.Should().HaveCount(2);
    }
}
