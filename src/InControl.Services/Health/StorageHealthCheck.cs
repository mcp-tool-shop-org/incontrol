using InControl.Services.Storage;

namespace InControl.Services.Health;

/// <summary>
/// Health check for storage system (file store accessibility and writability).
/// </summary>
public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly IFileStore _fileStore;

    public StorageHealthCheck(IFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    public string Name => "Storage";
    public string Category => "Storage";

    public async Task<HealthProbeResult> CheckAsync(CancellationToken ct = default)
    {
        // Check if base path exists by attempting to list files
        var listResult = await _fileStore.ListFilesAsync(".", "*", ct);
        if (listResult.IsFailure)
        {
            return HealthProbeResult.Unhealthy(
                Name,
                Category,
                $"Storage base directory is not accessible: {listResult.Error!.Message}",
                "Ensure the application data directory is accessible");
        }

        // Test write capability with a probe file
        const string probeFile = ".health-probe";
        var writeResult = await _fileStore.WriteTextAsync(probeFile, "health-check", ct);

        if (writeResult.IsFailure)
        {
            return HealthProbeResult.Unhealthy(
                Name,
                Category,
                $"Storage is not writable: {writeResult.Error!.Message}",
                "Check disk space and file permissions");
        }

        // Verify we can read it back
        var readResult = await _fileStore.ReadTextAsync(probeFile, ct);
        if (readResult.IsFailure || readResult.Value != "health-check")
        {
            // Clean up probe file
            await _fileStore.DeleteAsync(probeFile, ct);

            return HealthProbeResult.Degraded(
                Name,
                Category,
                "Storage read verification failed",
                "Check file system integrity");
        }

        // Clean up probe file
        await _fileStore.DeleteAsync(probeFile, ct);

        return HealthProbeResult.Healthy(Name, Category, "Storage is accessible and writable") with
        {
            Properties = new Dictionary<string, object>
            {
                ["BasePath"] = _fileStore.AppDataPath
            }
        };
    }
}
