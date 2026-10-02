using FluentAssertions;
using InControl.Services.Health;
using Xunit;

namespace InControl.Services.Tests.Health;

public class HealthProbeResultTests
{
    [Fact]
    public void Healthy_CreatesHealthyResult()
    {
        var result = HealthProbeResult.Healthy("Test", "Category", "All good");

        result.Name.Should().Be("Test");
        result.Category.Should().Be("Category");
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("All good");
    }

    [Fact]
    public void Healthy_DefaultDescription_IsOk()
    {
        var result = HealthProbeResult.Healthy("Test", "Category");

        result.Description.Should().Be("OK");
    }

    [Fact]
    public void Degraded_CreatesDegradedResult()
    {
        var result = HealthProbeResult.Degraded(
            "Test",
            "Category",
            "Slow response",
            "Check network");

        result.Name.Should().Be("Test");
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Slow response");
        result.RecommendedAction.Should().Be("Check network");
    }

    [Fact]
    public void Unhealthy_CreatesUnhealthyResult()
    {
        var result = HealthProbeResult.Unhealthy(
            "Test",
            "Category",
            "Connection failed",
            "Restart service");

        result.Name.Should().Be("Test");
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Connection failed");
        result.RecommendedAction.Should().Be("Restart service");
    }

    [Fact]
    public void Timestamp_IsSetToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var result = HealthProbeResult.Healthy("Test", "Category");
        var after = DateTimeOffset.UtcNow;

        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Properties_CanBeSetViaWith()
    {
        var result = HealthProbeResult.Healthy("Test", "Category") with
        {
            Properties = new Dictionary<string, object>
            {
                ["Key1"] = "Value1",
                ["Key2"] = 42
            }
        };

        result.Properties.Should().ContainKey("Key1");
        result.Properties!["Key1"].Should().Be("Value1");
        result.Properties["Key2"].Should().Be(42);
    }
}
