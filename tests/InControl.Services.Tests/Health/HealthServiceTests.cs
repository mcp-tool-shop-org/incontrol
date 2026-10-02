using FluentAssertions;
using InControl.Services.Health;
using Xunit;

namespace InControl.Services.Tests.Health;

public class HealthServiceTests
{
    [Fact]
    public async Task CheckAllAsync_RunsAllChecks()
    {
        var checks = new IHealthCheck[]
        {
            new FakeHealthCheck("Check1", "Cat1", HealthStatus.Healthy),
            new FakeHealthCheck("Check2", "Cat2", HealthStatus.Healthy)
        };
        var service = new HealthService(checks);

        var report = await service.CheckAllAsync();

        report.Probes.Should().HaveCount(2);
        report.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAllAsync_ReportsDuration()
    {
        var checks = new IHealthCheck[]
        {
            new FakeHealthCheck("Check1", "Cat1", HealthStatus.Healthy)
        };
        var service = new HealthService(checks);

        var report = await service.CheckAllAsync();

        report.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckAllAsync_EachProbeHasDuration()
    {
        var checks = new IHealthCheck[]
        {
            new FakeHealthCheck("Check1", "Cat1", HealthStatus.Healthy)
        };
        var service = new HealthService(checks);

        var report = await service.CheckAllAsync();

        report.Probes[0].Duration.Should().NotBeNull();
        report.Probes[0].Duration!.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckCategoryAsync_FiltersChecks()
    {
        var checks = new IHealthCheck[]
        {
            new FakeHealthCheck("Check1", "Inference", HealthStatus.Healthy),
            new FakeHealthCheck("Check2", "Storage", HealthStatus.Healthy),
            new FakeHealthCheck("Check3", "Inference", HealthStatus.Degraded)
        };
        var service = new HealthService(checks);

        var report = await service.CheckCategoryAsync("Inference");

        report.Probes.Should().HaveCount(2);
        report.Probes.Should().OnlyContain(p => p.Category == "Inference");
    }

    [Fact]
    public async Task CheckCategoryAsync_IsCaseInsensitive()
    {
        var checks = new IHealthCheck[]
        {
            new FakeHealthCheck("Check1", "Inference", HealthStatus.Healthy)
        };
        var service = new HealthService(checks);

        var report = await service.CheckCategoryAsync("INFERENCE");

        report.Probes.Should().HaveCount(1);
    }

    [Fact]
    public async Task CheckAllAsync_CatchesExceptions_ReturnsUnhealthy()
    {
        var checks = new IHealthCheck[]
        {
            new ThrowingHealthCheck("Broken", "Cat1", new InvalidOperationException("Boom!"))
        };
        var service = new HealthService(checks);

        var report = await service.CheckAllAsync();

        report.Probes.Should().HaveCount(1);
        report.Probes[0].Status.Should().Be(HealthStatus.Unhealthy);
        report.Probes[0].Description.Should().Contain("Boom!");
        report.Probes[0].Properties.Should().ContainKey("ExceptionType");
    }

    [Fact]
    public async Task CheckAllAsync_PropagatesCancellation()
    {
        var checks = new IHealthCheck[]
        {
            new FakeHealthCheck("Check1", "Cat1", HealthStatus.Healthy, TimeSpan.FromSeconds(10))
        };
        var service = new HealthService(checks);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = async () => await service.CheckAllAsync(cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void RegisteredChecks_ReturnsAllCheckNames()
    {
        var checks = new IHealthCheck[]
        {
            new FakeHealthCheck("Alpha", "Cat1", HealthStatus.Healthy),
            new FakeHealthCheck("Beta", "Cat2", HealthStatus.Healthy)
        };
        var service = new HealthService(checks);

        service.RegisteredChecks.Should().BeEquivalentTo(["Alpha", "Beta"]);
    }

    [Fact]
    public async Task CheckAllAsync_WithNoChecks_ReturnsEmptyHealthyReport()
    {
        var service = new HealthService(Array.Empty<IHealthCheck>());

        var report = await service.CheckAllAsync();

        report.IsHealthy.Should().BeTrue();
        report.Probes.Should().BeEmpty();
    }

    private sealed class FakeHealthCheck : IHealthCheck
    {
        private readonly HealthStatus _status;
        private readonly TimeSpan _delay;

        public FakeHealthCheck(string name, string category, HealthStatus status, TimeSpan? delay = null)
        {
            Name = name;
            Category = category;
            _status = status;
            _delay = delay ?? TimeSpan.FromMilliseconds(1);
        }

        public string Name { get; }
        public string Category { get; }

        public async Task<HealthProbeResult> CheckAsync(CancellationToken ct = default)
        {
            await Task.Delay(_delay, ct);
            return new HealthProbeResult
            {
                Name = Name,
                Category = Category,
                Status = _status,
                Description = _status.ToString()
            };
        }
    }

    private sealed class ThrowingHealthCheck : IHealthCheck
    {
        private readonly Exception _exception;

        public ThrowingHealthCheck(string name, string category, Exception exception)
        {
            Name = name;
            Category = category;
            _exception = exception;
        }

        public string Name { get; }
        public string Category { get; }

        public Task<HealthProbeResult> CheckAsync(CancellationToken ct = default)
        {
            throw _exception;
        }
    }
}
