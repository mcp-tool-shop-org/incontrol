using InControl.Core.Plugins;
using InControl.Core.Plugins.Samples;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// Tests for the Plugin SDK.
/// </summary>
public class PluginSdkTests
{
    #region PluginBase Tests

    [Fact]
    public async Task PluginBase_InitializesCorrectly()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);

        await plugin.InitializeAsync(context);

        Assert.True(plugin.IsInitialized);
        Assert.Equal(manifest.Id, plugin.Manifest.Id);
    }

    [Fact]
    public async Task PluginBase_ThrowsIfAlreadyInitialized()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);

        await plugin.InitializeAsync(context);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await plugin.InitializeAsync(context));
    }

    [Fact]
    public async Task PluginBase_ExecuteFailsIfNotInitialized()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);

        var result = await plugin.ExecuteAsync("greet", new Dictionary<string, object?> { ["name"] = "Test" }, context);

        Assert.False(result.Success);
        Assert.Contains("not initialized", result.Error);
    }

    [Fact]
    public async Task PluginBase_DisposesCorrectly()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        await plugin.DisposeAsync();

        // Should throw after disposal
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await plugin.ExecuteAsync("greet", new Dictionary<string, object?> { ["name"] = "Test" }, context));
    }

    #endregion

    #region PluginManifestBuilder Tests

    [Fact]
    public void ManifestBuilder_BuildsValidManifest()
    {
        var manifest = new PluginManifestBuilder()
            .WithId("test-plugin")
            .WithName("Test Plugin")
            .WithVersion("1.0.0")
            .WithAuthor("Test Author")
            .WithDescription("A test plugin")
            .WithRiskLevel(PluginRiskLevel.ReadOnly)
            .Build();

        Assert.Equal("test-plugin", manifest.Id);
        Assert.Equal("Test Plugin", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
    }

    [Fact]
    public void ManifestBuilder_AddsCapabilities()
    {
        var manifest = new PluginManifestBuilder()
            .WithId("test-plugin")
            .WithName("Test Plugin")
            .WithVersion("1.0.0")
            .WithAuthor("Test Author")
            .WithDescription("A test plugin")
            .WithRiskLevel(PluginRiskLevel.LocalMutation)
            .AddFilePermission("/data", PermissionAccess.Write, "Write data")
            .AddCapability("action1", "Action One", "First action")
            .AddCapability("action2", "Action Two", "Second action", modifiesState: true)
            .Build();

        Assert.Equal(2, manifest.Capabilities.Count);
        Assert.Equal("action1", manifest.Capabilities[0].ToolId);
        Assert.False(manifest.Capabilities[0].ModifiesState);
        Assert.True(manifest.Capabilities[1].ModifiesState);
    }

    [Fact]
    public void ManifestBuilder_AddsPermissions()
    {
        var manifest = new PluginManifestBuilder()
            .WithId("test-plugin")
            .WithName("Test Plugin")
            .WithVersion("1.0.0")
            .WithAuthor("Test Author")
            .WithDescription("A test plugin")
            .WithRiskLevel(PluginRiskLevel.LocalMutation)
            .AddFilePermission("/data", PermissionAccess.Read, "Read data files")
            .AddMemoryPermission(PermissionAccess.Write, "Store preferences")
            .Build();

        Assert.Equal(2, manifest.Permissions.Count);
        Assert.Contains(manifest.Permissions, p => p.Type == PermissionType.File);
        Assert.Contains(manifest.Permissions, p => p.Type == PermissionType.Memory);
    }

    [Fact]
    public void ManifestBuilder_ThrowsOnInvalidManifest()
    {
        var builder = new PluginManifestBuilder()
            .WithId("") // Invalid: empty ID
            .WithName("Test Plugin")
            .WithVersion("1.0.0")
            .WithAuthor("Test Author")
            .WithDescription("A test plugin");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    #endregion

    #region CapabilityParameter Tests

    [Fact]
    public void CapabilityParameter_RequiredString()
    {
        var param = CapabilityParameter.RequiredString("name", "The name");

        Assert.Equal("name", param.Name);
        Assert.Equal("string", param.Type);
        Assert.True(param.Required);
    }

    [Fact]
    public void CapabilityParameter_OptionalWithDefault()
    {
        var param = CapabilityParameter.OptionalString("mode", "The mode", "default");

        Assert.False(param.Required);
        Assert.Equal("default", param.Default);
    }

    [Fact]
    public void CapabilityParameter_Enum()
    {
        var param = CapabilityParameter.Enum("color", "The color", ["red", "green", "blue"]);

        Assert.True(param.Required);
        Assert.NotNull(param.Enum);
        Assert.Equal(3, param.Enum.Count);
    }

    #endregion

    #region HelloWorldPlugin Tests

    [Fact]
    public async Task HelloWorldPlugin_Greet_ReturnsGreeting()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        var result = await plugin.ExecuteAsync(
            "greet",
            new Dictionary<string, object?> { ["name"] = "Alice" },
            context);

        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task HelloWorldPlugin_Greet_SupportsDifferentStyles()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        var formal = await plugin.ExecuteAsync(
            "greet",
            new Dictionary<string, object?> { ["name"] = "Bob", ["style"] = "formal" },
            context);

        var enthusiastic = await plugin.ExecuteAsync(
            "greet",
            new Dictionary<string, object?> { ["name"] = "Bob", ["style"] = "enthusiastic" },
            context);

        Assert.True(formal.Success);
        Assert.True(enthusiastic.Success);
    }

    [Fact]
    public async Task HelloWorldPlugin_Greet_RequiresName()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        var result = await plugin.ExecuteAsync(
            "greet",
            new Dictionary<string, object?>(),
            context);

        Assert.False(result.Success);
        Assert.Contains("name", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HelloWorldPlugin_Stats_ReturnsStatistics()
    {
        var manifest = HelloWorldPlugin.CreateManifest();
        var plugin = new HelloWorldPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        // Generate some greetings
        await plugin.ExecuteAsync("greet", new Dictionary<string, object?> { ["name"] = "Alice" }, context);
        await plugin.ExecuteAsync("greet", new Dictionary<string, object?> { ["name"] = "Bob" }, context);

        var result = await plugin.ExecuteAsync("stats", new Dictionary<string, object?>(), context);

        Assert.True(result.Success);
    }

    #endregion

    #region CounterPlugin Tests

    [Fact]
    public async Task CounterPlugin_Increment_IncreasesValue()
    {
        var manifest = CounterPlugin.CreateManifest();
        var plugin = new CounterPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        var result = await plugin.ExecuteAsync("increment", new Dictionary<string, object?>(), context);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CounterPlugin_IncrementByAmount()
    {
        var manifest = CounterPlugin.CreateManifest();
        var plugin = new CounterPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        await plugin.ExecuteAsync(
            "increment",
            new Dictionary<string, object?> { ["amount"] = 5 },
            context);

        var result = await plugin.ExecuteAsync("get", new Dictionary<string, object?>(), context);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CounterPlugin_Reset_SetsToZero()
    {
        var manifest = CounterPlugin.CreateManifest();
        var plugin = new CounterPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        await plugin.ExecuteAsync("increment", new Dictionary<string, object?> { ["amount"] = 10 }, context);
        var result = await plugin.ExecuteAsync("reset", new Dictionary<string, object?>(), context);

        Assert.True(result.Success);
    }

    #endregion

    #region WeatherPlugin Tests

    [Fact]
    public void WeatherPlugin_ManifestHasNetworkPermission()
    {
        var manifest = WeatherPlugin.CreateManifest();

        Assert.Equal(PluginRiskLevel.Network, manifest.RiskLevel);
        Assert.Contains(manifest.Permissions, p => p.Type == PermissionType.Network);
        Assert.NotNull(manifest.NetworkIntent);
    }

    [Fact]
    public async Task WeatherPlugin_Current_RequiresLocation()
    {
        var manifest = WeatherPlugin.CreateManifest();
        var plugin = new WeatherPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        var result = await plugin.ExecuteAsync("current", new Dictionary<string, object?>(), context);

        Assert.False(result.Success);
        Assert.Contains("location", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WeatherPlugin_Current_ReturnsWeather()
    {
        var manifest = WeatherPlugin.CreateManifest();
        var plugin = new WeatherPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        var result = await plugin.ExecuteAsync(
            "current",
            new Dictionary<string, object?> { ["location"] = "London" },
            context);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WeatherPlugin_Forecast_ReturnsForecast()
    {
        var manifest = WeatherPlugin.CreateManifest();
        var plugin = new WeatherPlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);
        await plugin.InitializeAsync(context);

        var result = await plugin.ExecuteAsync(
            "forecast",
            new Dictionary<string, object?> { ["location"] = "Paris", ["days"] = 5 },
            context);

        Assert.True(result.Success);
    }

    #endregion

    #region PluginTestHelpers Tests

    [Fact]
    public void TestHelpers_CreateTestManifest()
    {
        var manifest = PluginTestHelpers.CreateTestManifest("my-test", "2.0.0");

        Assert.Equal("my-test", manifest.Id);
        Assert.Equal("2.0.0", manifest.Version);
    }

    [Fact]
    public void TestHelpers_CreateTestContext()
    {
        var manifest = PluginTestHelpers.CreateTestManifest();
        var context = PluginTestHelpers.CreateTestContext(manifest);

        Assert.NotNull(context.Files);
        Assert.NotNull(context.Network);
        Assert.NotNull(context.Memory);
        Assert.NotNull(context.Storage);
    }

    [Fact]
    public async Task TestHelpers_TestFileAccess_Works()
    {
        var manifest = PluginTestHelpers.CreateTestManifest();
        var context = PluginTestHelpers.CreateTestContext(manifest);

        await context.Files.WriteAsync("/test.txt", "Hello World");
        var result = await context.Files.ReadAsync("/test.txt");

        Assert.True(result.Success);
        Assert.Equal("Hello World", result.Content);
    }

    [Fact]
    public async Task TestHelpers_TestStorage_Works()
    {
        var manifest = PluginTestHelpers.CreateTestManifest();
        var context = PluginTestHelpers.CreateTestContext(manifest);

        await context.Storage.SetAsync("key", "value");
        var value = await context.Storage.GetAsync("key");

        Assert.Equal("value", value);
    }

    #endregion
}
