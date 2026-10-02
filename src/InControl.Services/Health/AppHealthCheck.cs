using System.Reflection;
using System.Runtime.InteropServices;

namespace InControl.Services.Health;

/// <summary>
/// Health check for application runtime information.
/// </summary>
public sealed class AppHealthCheck : IHealthCheck
{
    public string Name => "App";
    public string Category => "App";

    public Task<HealthProbeResult> CheckAsync(CancellationToken ct = default)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version;

        var properties = new Dictionary<string, object>
        {
            ["Version"] = informationalVersion,
            ["RuntimeVersion"] = RuntimeInformation.FrameworkDescription,
            ["OSDescription"] = RuntimeInformation.OSDescription,
            ["OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["ProcessId"] = Environment.ProcessId,
            ["MachineName"] = Environment.MachineName,
            ["ProcessorCount"] = Environment.ProcessorCount,
            ["WorkingSet"] = Environment.WorkingSet,
            ["Is64BitProcess"] = Environment.Is64BitProcess,
            ["StartTime"] = Process.GetCurrentProcess().StartTime.ToUniversalTime()
        };

        // Check for potential issues
        var workingSetMB = Environment.WorkingSet / (1024 * 1024);
        if (workingSetMB > 2000) // More than 2GB
        {
            return Task.FromResult(HealthProbeResult.Degraded(
                Name,
                Category,
                $"High memory usage: {workingSetMB:N0} MB",
                "Consider restarting the application if memory continues to grow") with
            {
                Properties = properties
            });
        }

        return Task.FromResult(HealthProbeResult.Healthy(
            Name,
            Category,
            $"InControl v{informationalVersion}") with
        {
            Properties = properties
        });
    }
}

file static class Process
{
    public static System.Diagnostics.Process GetCurrentProcess() =>
        System.Diagnostics.Process.GetCurrentProcess();
}
