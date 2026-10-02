namespace InControl.Services.Health;

/// <summary>
/// Aggregated health report from all health checks.
/// </summary>
public sealed record HealthReport
{
    /// <summary>
    /// Overall status of the system (worst status from all checks).
    /// </summary>
    public required HealthStatus OverallStatus { get; init; }

    /// <summary>
    /// Individual probe results.
    /// </summary>
    public required IReadOnlyList<HealthProbeResult> Probes { get; init; }

    /// <summary>
    /// When this report was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Total time to run all health checks.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Probes that are not healthy.
    /// </summary>
    public IEnumerable<HealthProbeResult> Degradations =>
        Probes.Where(p => p.Status != HealthStatus.Healthy);

    /// <summary>
    /// Whether all probes are healthy.
    /// </summary>
    public bool IsHealthy => OverallStatus == HealthStatus.Healthy;

    /// <summary>
    /// Probes grouped by category.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<HealthProbeResult>> ByCategory =>
        Probes
            .GroupBy(p => p.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<HealthProbeResult>)g.ToList());

    /// <summary>
    /// Creates a health report from a collection of probe results.
    /// </summary>
    public static HealthReport Create(IEnumerable<HealthProbeResult> probes, TimeSpan duration)
    {
        var probeList = probes.ToList();
        var worstStatus = probeList.Count == 0
            ? HealthStatus.Healthy
            : probeList.Max(p => p.Status);

        return new HealthReport
        {
            OverallStatus = worstStatus,
            Probes = probeList,
            Duration = duration
        };
    }

    /// <summary>
    /// Creates an empty healthy report.
    /// </summary>
    public static HealthReport Empty() => new()
    {
        OverallStatus = HealthStatus.Healthy,
        Probes = Array.Empty<HealthProbeResult>(),
        Duration = TimeSpan.Zero
    };
}
