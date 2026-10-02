using FluentAssertions;
using InControl.Inference.Interfaces;
using Xunit;

namespace InControl.Inference.Tests.Interfaces;

public class HealthCheckResultTests
{
    [Fact]
    public void Healthy_CreatesHealthyResult()
    {
        // Arrange & Act
        var result = HealthCheckResult.Healthy("All systems go", TimeSpan.FromMilliseconds(50));

        // Assert
        result.IsHealthy.Should().BeTrue();
        result.Status.Should().Be("All systems go");
        result.ResponseTime.Should().Be(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Healthy_WithDefaults_CreatesOkResult()
    {
        // Arrange & Act
        var result = HealthCheckResult.Healthy();

        // Assert
        result.IsHealthy.Should().BeTrue();
        result.Status.Should().Be("OK");
        result.ResponseTime.Should().BeNull();
    }

    [Fact]
    public void Unhealthy_CreatesUnhealthyResult()
    {
        // Arrange & Act
        var result = HealthCheckResult.Unhealthy("Connection refused");

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.Status.Should().Be("Connection refused");
    }
}
