using FluentAssertions;
using InControl.Inference.Fakes;
using InControl.Services.Health;
using Xunit;

namespace InControl.Services.Tests.Health;

public class InferenceHealthCheckTests
{
    [Fact]
    public async Task CheckAsync_ReturnsHealthy_WhenBackendIsAvailableWithModels()
    {
        var client = new FakeInferenceClient()
            .SetAvailable(true)
            .AddModel("llama3.2");
        var check = new InferenceHealthCheck(client);

        var result = await check.CheckAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Properties.Should().ContainKey("LoadedModels");
        result.Properties!["LoadedModels"].Should().Be(1);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUnhealthy_WhenBackendIsUnavailable()
    {
        var client = new FakeInferenceClient().SetAvailable(false);
        var check = new InferenceHealthCheck(client);

        var result = await check.CheckAsync();

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not reachable");
        result.RecommendedAction.Should().Contain("running");
    }

    [Fact]
    public async Task CheckAsync_ReturnsDegraded_WhenNoModelsLoaded()
    {
        var client = new FakeInferenceClient().SetAvailable(true);
        // No models added
        var check = new InferenceHealthCheck(client);

        var result = await check.CheckAsync();

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("No models");
        result.RecommendedAction.Should().Contain("Load");
    }

    [Fact]
    public void Name_IsInference()
    {
        var client = new FakeInferenceClient();
        var check = new InferenceHealthCheck(client);

        check.Name.Should().Be("Inference");
    }

    [Fact]
    public void Category_IsInference()
    {
        var client = new FakeInferenceClient();
        var check = new InferenceHealthCheck(client);

        check.Category.Should().Be("Inference");
    }

    [Fact]
    public async Task CheckAsync_IncludesBackendName_InProperties()
    {
        var client = new FakeInferenceClient()
            .SetAvailable(true)
            .AddModel("test-model");
        var check = new InferenceHealthCheck(client);

        var result = await check.CheckAsync();

        result.Properties.Should().ContainKey("Backend");
        result.Properties!["Backend"].Should().Be("Fake");
    }
}
