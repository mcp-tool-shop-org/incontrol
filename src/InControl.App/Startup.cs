using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using InControl.Core.Configuration;
using InControl.Inference.Extensions;
using InControl.Services.Extensions;
using InControl.App.Audio;
using InControl.Services.Voice;
using InControl.ViewModels;

namespace InControl.App;

/// <summary>
/// Application startup and DI configuration.
/// </summary>
public static class Startup
{
    /// <summary>
    /// Configures services for dependency injection.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration options
        ConfigureOptions(services, configuration);

        // Logging
        ConfigureLogging(services, configuration);

        // Layer services (extension methods for clean registration)
        services.AddInferenceServices();
        services.AddOllamaBackend();
        services.AddApplicationServices();
        services.AddVoiceServices();
        services.AddSingleton<IAudioPlayer, AudioGraphPlayer>();

        // ViewModels
        ConfigureViewModels(services);
    }

    private static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<ChatOptions>(configuration.GetSection(ChatOptions.SectionName));
        services.Configure<InferenceOptions>(configuration.GetSection(InferenceOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<VoiceOptions>(configuration.GetSection(VoiceOptions.SectionName));
        services.Configure<LoggingOptions>(configuration.GetSection(LoggingOptions.SectionName));
    }

    private static void ConfigureViewModels(IServiceCollection services)
    {
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ConversationListViewModel>();
    }

    private static void ConfigureLogging(IServiceCollection services, IConfiguration configuration)
    {
        var loggingOptions = configuration
            .GetSection(LoggingOptions.SectionName)
            .Get<LoggingOptions>() ?? new LoggingOptions();

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(ParseLogLevel(loggingOptions.MinLevel));

        if (loggingOptions.WriteToConsole)
        {
            loggerConfig.WriteTo.Console();
        }

        if (loggingOptions.WriteToFile)
        {
            var logPath = Path.Combine(
                loggingOptions.ExpandedFilePath,
                "incontrol-.log");

            loggerConfig.WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: loggingOptions.MaxFileSizeMb * 1024 * 1024,
                retainedFileCountLimit: loggingOptions.RetainedFileCount);
        }

        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });
    }

    private static Serilog.Events.LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" => Serilog.Events.LogEventLevel.Verbose,
            "debug" => Serilog.Events.LogEventLevel.Debug,
            "information" => Serilog.Events.LogEventLevel.Information,
            "warning" => Serilog.Events.LogEventLevel.Warning,
            "error" => Serilog.Events.LogEventLevel.Error,
            "fatal" => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
    }
}
