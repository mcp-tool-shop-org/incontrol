namespace InControl.Core.Plugins.Samples;

/// <summary>
/// Sample plugin demonstrating the plugin SDK.
/// This plugin provides a simple greeting tool.
/// </summary>
public sealed class HelloWorldPlugin : PluginBase
{
    private int _greetingCount;

    /// <summary>
    /// Creates the manifest for this plugin.
    /// </summary>
    public static PluginManifest CreateManifest() =>
        new PluginManifestBuilder()
            .WithId("com.incontrol.samples.hello-world")
            .WithName("Hello World Plugin")
            .WithVersion("1.0.0")
            .WithAuthor("InControl Team")
            .WithDescription("A sample plugin demonstrating the plugin SDK. Provides greeting tools.")
            .WithLicense("MIT")
            .WithRiskLevel(PluginRiskLevel.ReadOnly)
            .AddCapability(
                toolId: "greet",
                name: "Greet",
                description: "Returns a personalized greeting message",
                modifiesState: false,
                parameters: [
                    CapabilityParameter.RequiredString("name", "The name to greet"),
                    CapabilityParameter.OptionalString("style", "Greeting style: formal, casual, or enthusiastic", "casual")
                ])
            .AddCapability(
                toolId: "stats",
                name: "Greeting Statistics",
                description: "Returns statistics about greetings generated",
                modifiesState: false)
            .Build();

    /// <inheritdoc />
    protected override Task OnInitializeAsync(CancellationToken ct)
    {
        _greetingCount = 0;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task<PluginActionResult> OnExecuteAsync(
        string actionId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
    {
        return actionId switch
        {
            "greet" => ExecuteGreetAsync(parameters),
            "stats" => ExecuteStatsAsync(),
            _ => Task.FromResult(PluginActionResult.Failed($"Unknown action: {actionId}"))
        };
    }

    private Task<PluginActionResult> ExecuteGreetAsync(IReadOnlyDictionary<string, object?> parameters)
    {
        // Get required parameter
        if (!parameters.TryGetValue("name", out var nameObj) || nameObj is not string name || string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(PluginActionResult.Failed("Missing required parameter: name"));
        }

        // Get optional parameter with default
        var style = "casual";
        if (parameters.TryGetValue("style", out var styleObj) && styleObj is string styleStr)
        {
            style = styleStr;
        }

        // Generate greeting based on style
        var greeting = style.ToLowerInvariant() switch
        {
            "formal" => $"Good day, {name}. It is a pleasure to make your acquaintance.",
            "enthusiastic" => $"Hey {name}! So AWESOME to meet you! 🎉",
            _ => $"Hello, {name}! Nice to meet you."
        };

        _greetingCount++;

        return Task.FromResult(PluginActionResult.Succeeded(new
        {
            greeting,
            style,
            timestamp = DateTimeOffset.UtcNow
        }));
    }

    private Task<PluginActionResult> ExecuteStatsAsync()
    {
        return Task.FromResult(PluginActionResult.Succeeded(new
        {
            totalGreetings = _greetingCount,
            pluginId = Manifest.Id,
            version = Manifest.Version
        }));
    }
}

/// <summary>
/// Sample plugin demonstrating storage and state persistence.
/// </summary>
public sealed class CounterPlugin : PluginBase
{
    private const string CounterKey = "counter_value";

    /// <summary>
    /// Creates the manifest for this plugin.
    /// </summary>
    public static PluginManifest CreateManifest() =>
        new PluginManifestBuilder()
            .WithId("com.incontrol.samples.counter")
            .WithName("Counter Plugin")
            .WithVersion("1.0.0")
            .WithAuthor("InControl Team")
            .WithDescription("A sample plugin demonstrating storage. Maintains a persistent counter.")
            .WithLicense("MIT")
            .WithRiskLevel(PluginRiskLevel.LocalMutation)
            .AddFilePermission("/storage", PermissionAccess.Write, "Store counter state")
            .AddCapability(
                toolId: "increment",
                name: "Increment Counter",
                description: "Increments the counter and returns the new value",
                modifiesState: true,
                parameters: [
                    CapabilityParameter.OptionalNumber("amount", "Amount to increment by", 1)
                ])
            .AddCapability(
                toolId: "decrement",
                name: "Decrement Counter",
                description: "Decrements the counter and returns the new value",
                modifiesState: true,
                parameters: [
                    CapabilityParameter.OptionalNumber("amount", "Amount to decrement by", 1)
                ])
            .AddCapability(
                toolId: "get",
                name: "Get Counter",
                description: "Returns the current counter value",
                modifiesState: false)
            .AddCapability(
                toolId: "reset",
                name: "Reset Counter",
                description: "Resets the counter to zero",
                modifiesState: true)
            .Build();

    /// <inheritdoc />
    protected override async Task<PluginActionResult> OnExecuteAsync(
        string actionId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
    {
        return actionId switch
        {
            "increment" => await ModifyCounterAsync(GetAmount(parameters), ct),
            "decrement" => await ModifyCounterAsync(-GetAmount(parameters), ct),
            "get" => await GetCounterAsync(ct),
            "reset" => await ResetCounterAsync(ct),
            _ => PluginActionResult.Failed($"Unknown action: {actionId}")
        };
    }

    private static int GetAmount(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue("amount", out var amountObj))
        {
            return amountObj switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 1
            };
        }
        return 1;
    }

    private async Task<PluginActionResult> GetCounterAsync(CancellationToken ct)
    {
        var value = await LoadCounterAsync(ct);
        return PluginActionResult.Succeeded(new { value });
    }

    private async Task<PluginActionResult> ModifyCounterAsync(int delta, CancellationToken ct)
    {
        var current = await LoadCounterAsync(ct);
        var newValue = current + delta;
        await SaveCounterAsync(newValue, ct);

        return PluginActionResult.Succeeded(new
        {
            previousValue = current,
            change = delta,
            value = newValue
        });
    }

    private async Task<PluginActionResult> ResetCounterAsync(CancellationToken ct)
    {
        var previous = await LoadCounterAsync(ct);
        await SaveCounterAsync(0, ct);

        return PluginActionResult.Succeeded(new
        {
            previousValue = previous,
            value = 0
        });
    }

    private async Task<int> LoadCounterAsync(CancellationToken ct)
    {
        var stored = await Storage.GetAsync(CounterKey, ct);
        if (stored != null && int.TryParse(stored, out var value))
        {
            return value;
        }
        return 0;
    }

    private async Task SaveCounterAsync(int value, CancellationToken ct)
    {
        await Storage.SetAsync(CounterKey, value.ToString(), ct);
    }
}

/// <summary>
/// Sample plugin demonstrating network capabilities.
/// </summary>
public sealed class WeatherPlugin : PluginBase
{
    /// <summary>
    /// Creates the manifest for this plugin.
    /// </summary>
    public static PluginManifest CreateManifest() =>
        new PluginManifestBuilder()
            .WithId("com.incontrol.samples.weather")
            .WithName("Weather Plugin")
            .WithVersion("1.0.0")
            .WithAuthor("InControl Team")
            .WithDescription("A sample plugin demonstrating network access. Fetches weather data.")
            .WithLicense("MIT")
            .WithRiskLevel(PluginRiskLevel.Network)
            .AddNetworkPermission(
                "https://api.weather.example.com",
                "Fetch weather data from weather API",
                ["city name", "coordinates"])
            .AddCapability(
                toolId: "current",
                name: "Get Current Weather",
                description: "Fetches current weather for a location",
                modifiesState: false,
                parameters: [
                    CapabilityParameter.RequiredString("location", "City name or coordinates")
                ])
            .AddCapability(
                toolId: "forecast",
                name: "Get Weather Forecast",
                description: "Fetches weather forecast for a location",
                modifiesState: false,
                parameters: [
                    CapabilityParameter.RequiredString("location", "City name or coordinates"),
                    CapabilityParameter.OptionalNumber("days", "Number of days to forecast", 3)
                ])
            .Build();

    /// <inheritdoc />
    protected override async Task<PluginActionResult> OnExecuteAsync(
        string actionId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
    {
        if (!Network.IsAvailable)
        {
            return PluginActionResult.Failed("Network is not available");
        }

        if (!parameters.TryGetValue("location", out var locationObj) || locationObj is not string location)
        {
            return PluginActionResult.Failed("Missing required parameter: location");
        }

        return actionId switch
        {
            "current" => await GetCurrentWeatherAsync(location, ct),
            "forecast" => await GetForecastAsync(location, GetDays(parameters), ct),
            _ => PluginActionResult.Failed($"Unknown action: {actionId}")
        };
    }

    private static int GetDays(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue("days", out var daysObj))
        {
            return daysObj switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                _ => 3
            };
        }
        return 3;
    }

    private async Task<PluginActionResult> GetCurrentWeatherAsync(string location, CancellationToken ct)
    {
        var endpoint = $"https://api.weather.example.com/current?location={Uri.EscapeDataString(location)}";
        var result = await Network.RequestAsync(endpoint, "GET", null, "Fetch current weather", ct);

        if (!result.Success)
        {
            return PluginActionResult.Failed($"Weather API error: {result.Error}");
        }

        // In a real plugin, parse the JSON response
        // For this sample, return mock data
        return PluginActionResult.Succeeded(new
        {
            location,
            temperature = 22.5,
            unit = "celsius",
            condition = "Partly cloudy",
            humidity = 65,
            fetchedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task<PluginActionResult> GetForecastAsync(string location, int days, CancellationToken ct)
    {
        var endpoint = $"https://api.weather.example.com/forecast?location={Uri.EscapeDataString(location)}&days={days}";
        var result = await Network.RequestAsync(endpoint, "GET", null, "Fetch weather forecast", ct);

        if (!result.Success)
        {
            return PluginActionResult.Failed($"Weather API error: {result.Error}");
        }

        // Mock forecast data
        var forecast = Enumerable.Range(0, days).Select(i => new
        {
            date = DateTimeOffset.UtcNow.AddDays(i).ToString("yyyy-MM-dd"),
            high = 20 + Random.Shared.Next(10),
            low = 10 + Random.Shared.Next(10),
            condition = i % 2 == 0 ? "Sunny" : "Cloudy"
        }).ToList();

        return PluginActionResult.Succeeded(new
        {
            location,
            days,
            forecast,
            fetchedAt = DateTimeOffset.UtcNow
        });
    }
}
