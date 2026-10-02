namespace InControl.Services.Health;

/// <summary>
/// Interface for health check probes.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Gets the name of this health check.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the category of this health check (e.g., "Inference", "Storage", "App").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Performs the health check.
    /// </summary>
    Task<HealthProbeResult> CheckAsync(CancellationToken ct = default);
}

/// <summary>
/// Result of a single health check probe.
/// </summary>
public sealed record HealthProbeResult
{
    /// <summary>
    /// Name of the health check.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Category of the health check.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Status of the check.
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Time taken to perform the check.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Recommended action if degraded or unhealthy.
    /// </summary>
    public string? RecommendedAction { get; init; }

    /// <summary>
    /// Additional properties.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Properties { get; init; }

    /// <summary>
    /// When this check was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    public static HealthProbeResult Healthy(string name, string category, string? description = null) => new()
    {
        Name = name,
        Category = category,
        Status = HealthStatus.Healthy,
        Description = description ?? "OK"
    };

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    public static HealthProbeResult Degraded(
        string name,
        string category,
        string description,
        string? action = null) => new()
    {
        Name = name,
        Category = category,
        Status = HealthStatus.Degraded,
        Description = description,
        RecommendedAction = action
    };

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    public static HealthProbeResult Unhealthy(
        string name,
        string category,
        string description,
        string? action = null) => new()
    {
        Name = name,
        Category = category,
        Status = HealthStatus.Unhealthy,
        Description = description,
        RecommendedAction = action
    };
}

/// <summary>
/// Health status levels.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Everything is working correctly.
    /// </summary>
    Healthy,

    /// <summary>
    /// Working but with issues (e.g., slow response, low resources).
    /// </summary>
    Degraded,

    /// <summary>
    /// Not working correctly.
    /// </summary>
    Unhealthy
}
