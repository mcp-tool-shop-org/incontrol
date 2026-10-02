using System.Diagnostics;

namespace InControl.Services.Health;

/// <summary>
/// Service that runs all registered health checks and aggregates results.
/// </summary>
public sealed class HealthService : IHealthService
{
    private readonly IReadOnlyList<IHealthCheck> _healthChecks;

    public HealthService(IEnumerable<IHealthCheck> healthChecks)
    {
        _healthChecks = healthChecks.ToList();
    }

    public IReadOnlyList<string> RegisteredChecks =>
        _healthChecks.Select(c => c.Name).ToList();

    public async Task<HealthReport> CheckAllAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<HealthProbeResult>();

        foreach (var check in _healthChecks)
        {
            ct.ThrowIfCancellationRequested();
            var result = await RunCheckAsync(check, ct);
            results.Add(result);
        }

        stopwatch.Stop();
        return HealthReport.Create(results, stopwatch.Elapsed);
    }

    public async Task<HealthReport> CheckCategoryAsync(string category, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<HealthProbeResult>();

        var categoryChecks = _healthChecks.Where(c =>
            c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

        foreach (var check in categoryChecks)
        {
            ct.ThrowIfCancellationRequested();
            var result = await RunCheckAsync(check, ct);
            results.Add(result);
        }

        stopwatch.Stop();
        return HealthReport.Create(results, stopwatch.Elapsed);
    }

    private static async Task<HealthProbeResult> RunCheckAsync(IHealthCheck check, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await check.CheckAsync(ct);
            stopwatch.Stop();

            // Ensure the result has the correct duration
            return result with { Duration = stopwatch.Elapsed };
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Health checks should not throw - convert exception to unhealthy result
            return HealthProbeResult.Unhealthy(
                check.Name,
                check.Category,
                $"Health check threw exception: {ex.Message}",
                "Investigate the health check implementation") with
            {
                Duration = stopwatch.Elapsed,
                Properties = new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["ExceptionMessage"] = ex.Message
                }
            };
        }
    }
}
