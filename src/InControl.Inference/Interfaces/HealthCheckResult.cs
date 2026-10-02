namespace InControl.Inference.Interfaces;

/// <summary>
/// Result of a health check on an inference backend.
/// </summary>
public sealed record HealthCheckResult
{
    /// <summary>
    /// Whether the backend is healthy and ready to serve requests.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Time taken to perform the health check.
    /// </summary>
    public TimeSpan? ResponseTime { get; init; }

    /// <summary>
    /// Backend version information.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Number of models currently loaded.
    /// </summary>
    public int? LoadedModels { get; init; }

    /// <summary>
    /// GPU memory usage in bytes (if available).
    /// </summary>
    public long? GpuMemoryUsed { get; init; }

    /// <summary>
    /// Total GPU memory in bytes (if available).
    /// </summary>
    public long? GpuMemoryTotal { get; init; }

    /// <summary>
    /// Additional backend-specific properties.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Properties { get; init; }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    public static HealthCheckResult Healthy(string status = "OK", TimeSpan? responseTime = null) => new()
    {
        IsHealthy = true,
        Status = status,
        ResponseTime = responseTime
    };

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    public static HealthCheckResult Unhealthy(string status) => new()
    {
        IsHealthy = false,
        Status = status
    };
}
