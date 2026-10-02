using InControl.Inference.Interfaces;

namespace InControl.Services.Health;

/// <summary>
/// Health check for inference backend connectivity and model availability.
/// </summary>
public sealed class InferenceHealthCheck : IHealthCheck
{
    private readonly IInferenceClient _client;

    public InferenceHealthCheck(IInferenceClient client)
    {
        _client = client;
    }

    public string Name => "Inference";
    public string Category => "Inference";

    public async Task<HealthProbeResult> CheckAsync(CancellationToken ct = default)
    {
        // Check if backend is reachable
        var isAvailable = await _client.IsAvailableAsync(ct);
        if (!isAvailable)
        {
            return HealthProbeResult.Unhealthy(
                Name,
                Category,
                $"Inference backend ({_client.BackendName}) is not reachable",
                "Ensure the inference server is running and accessible");
        }

        // Check backend health details
        var health = await _client.CheckHealthAsync(ct);
        if (!health.IsHealthy)
        {
            return HealthProbeResult.Unhealthy(
                Name,
                Category,
                health.Status ?? "Backend reported unhealthy",
                "Check the inference server logs for details");
        }

        // Check if any models are loaded
        var models = await _client.ListModelsAsync(ct);
        if (models.Count == 0)
        {
            return HealthProbeResult.Degraded(
                Name,
                Category,
                "No models are loaded in the inference backend",
                "Load at least one model to enable chat functionality");
        }

        return HealthProbeResult.Healthy(Name, Category, $"{_client.BackendName} is healthy") with
        {
            Properties = new Dictionary<string, object>
            {
                ["Backend"] = _client.BackendName,
                ["LoadedModels"] = models.Count,
                ["Version"] = health.Version ?? "unknown"
            }
        };
    }
}
