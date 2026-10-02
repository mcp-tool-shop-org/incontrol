namespace InControl.Services.Health;

/// <summary>
/// Service for running health checks and aggregating results.
/// </summary>
public interface IHealthService
{
    /// <summary>
    /// Runs all registered health checks and returns an aggregated report.
    /// </summary>
    Task<HealthReport> CheckAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs health checks for a specific category.
    /// </summary>
    Task<HealthReport> CheckCategoryAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Gets the list of registered health check names.
    /// </summary>
    IReadOnlyList<string> RegisteredChecks { get; }
}
