using FluentAssertions;
using InControl.Services.Health;
using Xunit;

namespace InControl.Services.Tests.Health;

public class AppHealthCheckTests
{
    [Fact]
    public async Task CheckAsync_ReturnsHealthy_Normally()
    {
        var check = new AppHealthCheck();

        var result = await check.CheckAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckAsync_IncludesRuntimeInfo()
    {
        var check = new AppHealthCheck();

        var result = await check.CheckAsync();

        result.Properties.Should().ContainKey("RuntimeVersion");
        result.Properties.Should().ContainKey("OSDescription");
        result.Properties.Should().ContainKey("ProcessorCount");
    }

    [Fact]
    public async Task CheckAsync_IncludesProcessInfo()
    {
        var check = new AppHealthCheck();

        var result = await check.CheckAsync();

        result.Properties.Should().ContainKey("ProcessId");
        result.Properties.Should().ContainKey("WorkingSet");
        result.Properties.Should().ContainKey("Is64BitProcess");
    }

    [Fact]
    public async Task CheckAsync_IncludesVersionInfo()
    {
        var check = new AppHealthCheck();

        var result = await check.CheckAsync();

        result.Properties.Should().ContainKey("Version");
        result.Description.Should().Contain("InControl");
    }

    [Fact]
    public void Name_IsApp()
    {
        var check = new AppHealthCheck();

        check.Name.Should().Be("App");
    }

    [Fact]
    public void Category_IsApp()
    {
        var check = new AppHealthCheck();

        check.Category.Should().Be("App");
    }

    [Fact]
    public async Task CheckAsync_IsSynchronous_ButReturnsTask()
    {
        var check = new AppHealthCheck();

        // Should complete immediately
        var task = check.CheckAsync();
        task.IsCompleted.Should().BeTrue();

        var result = await task;
        result.Should().NotBeNull();
    }
}
