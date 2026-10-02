using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using InControl.Core.Compute;
using InControl.Core.Configuration;
using InControl.Core.Storage;
using InControl.Services.Chat;
using InControl.Services.Compute;
using InControl.Services.Interfaces;
using InControl.Services.Storage;
using InControl.Services.Voice;

namespace InControl.Services.Extensions;

/// <summary>
/// Extension methods for registering application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds application services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileStore, FileStore>();
        services.AddSingleton<IConversationStorage, JsonConversationStorage>();
        services.AddSingleton<IProjectLibrary, JsonProjectLibrary>();
        services.AddSingleton<ISessionMemory, JsonSessionMemory>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<ITcpProbe, LoopbackTcpProbe>();
        services.AddSingleton<IOllamaReadyProbe, HttpOllamaReadyProbe>();
        services.AddSingleton<IRunPodPods, RunPodPodClient>();
        services.AddSingleton<ISshSessionFactory, OpenSshSessionFactory>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            var local = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "http://127.0.0.1:11434"
                : options.BaseUrl;
            return new ComputeSession(
                local,
                sp.GetRequiredService<ISshSessionFactory>(),
                sp.GetRequiredService<ITcpProbe>(),
                Path.Combine(DataPaths.AppDataRoot, "ssh"),
                ready: sp.GetRequiredService<IOllamaReadyProbe>());
        });
        services.AddSingleton<IOllamaEndpoint>(sp => sp.GetRequiredService<ComputeSession>());
        return services;
    }

    /// <summary>
    /// Adds voice synthesis services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVoiceServices(this IServiceCollection services)
    {
        services.AddSingleton<KokoroVoiceService>();
        services.AddSingleton<WindowsVoiceService>();
        services.AddSingleton<IVoiceService, FallbackVoiceService>();
        return services;
    }
}
