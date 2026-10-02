using InControl.Core.Assistant;
using InControl.Core.Connectivity;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// Tests for the plugin tool adapter system.
/// </summary>
public class PluginToolAdapterTests : IDisposable
{
    private readonly string _tempPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _connectivity;

    public PluginToolAdapterTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"plugin-adapter-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        _gateway = new FakeNetworkGateway();
        _connectivity = new ConnectivityManager(_gateway, Path.Combine(_tempPath, "connectivity.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, true); } catch { }
        }
    }

    private static PluginManifest CreateTestManifest(
        string id = "test-plugin",
        PluginRiskLevel riskLevel = PluginRiskLevel.ReadOnly,
        List<PluginCapability>? capabilities = null)
    {
        return new PluginManifest
        {
            Id = id,
            Version = "1.0.0",
            Name = "Test Plugin",
            Author = "Test",
            Description = "A test plugin",
            RiskLevel = riskLevel,
            Capabilities = capabilities ?? new List<PluginCapability>
            {
                new()
                {
                    ToolId = "test-tool",
                    Name = "Test Tool",
                    Description = "A test tool",
                    RequiresNetwork = false,
                    ModifiesState = false
                }
            }
        };
    }

    private PluginHost CreatePluginHost()
    {
        var storagePath = Path.Combine(_tempPath, "plugin-storage");
        var sandbox = new PluginSandbox(_connectivity, storagePath);
        var auditLog = new InMemoryPluginAuditLog();
        return new PluginHost(sandbox, auditLog);
    }

    #region PluginToolAdapter Tests

    [Fact]
    public void PluginToolAdapter_Id_IncludesPluginAndCapability()
    {
        // Arrange
        var manifest = CreateTestManifest("my-plugin");
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "my-plugin", capability, manifest);

        // Assert
        Assert.Equal("plugin:my-plugin:test-tool", adapter.Id);
    }

    [Fact]
    public void PluginToolAdapter_Name_ComesFromCapability()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        // Assert
        Assert.Equal("Test Tool", adapter.Name);
    }

    [Fact]
    public void PluginToolAdapter_Description_ComesFromCapability()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        // Assert
        Assert.Equal("A test tool", adapter.Description);
    }

    [Theory]
    [InlineData(PluginRiskLevel.ReadOnly, ToolRiskLevel.Low)]
    [InlineData(PluginRiskLevel.LocalMutation, ToolRiskLevel.Medium)]
    [InlineData(PluginRiskLevel.Network, ToolRiskLevel.High)]
    [InlineData(PluginRiskLevel.SystemAdjacent, ToolRiskLevel.Critical)]
    public void PluginToolAdapter_RiskLevel_MapsCorrectly(PluginRiskLevel pluginRisk, ToolRiskLevel expectedToolRisk)
    {
        // Arrange
        var manifest = CreateTestManifest(riskLevel: pluginRisk);
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        // Assert
        Assert.Equal(expectedToolRisk, adapter.RiskLevel);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void PluginToolAdapter_IsReadOnly_BasedOnModifiesState(bool modifiesState, bool expectedIsReadOnly)
    {
        // Arrange
        var capabilities = new List<PluginCapability>
        {
            new()
            {
                ToolId = "test-tool",
                Name = "Test Tool",
                Description = "A test tool",
                ModifiesState = modifiesState
            }
        };
        var manifest = CreateTestManifest(capabilities: capabilities);
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        // Assert
        Assert.Equal(expectedIsReadOnly, adapter.IsReadOnly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PluginToolAdapter_RequiresNetwork_ComesFromCapability(bool requiresNetwork)
    {
        // Arrange
        var capabilities = new List<PluginCapability>
        {
            new()
            {
                ToolId = "test-tool",
                Name = "Test Tool",
                Description = "A test tool",
                RequiresNetwork = requiresNetwork
            }
        };
        var manifest = CreateTestManifest(capabilities: capabilities);
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        // Assert
        Assert.Equal(requiresNetwork, adapter.RequiresNetwork);
    }

    [Fact]
    public void PluginToolAdapter_Parameters_HasGenericInput()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        // Assert
        Assert.Single(adapter.Parameters);
        Assert.Equal("input", adapter.Parameters[0].Name);
    }

    [Fact]
    public void PluginToolAdapter_GetPluginInfo_ReturnsCorrectInfo()
    {
        // Arrange
        var manifest = CreateTestManifest("my-plugin");
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();
        var adapter = new PluginToolAdapter(host, "my-plugin", capability, manifest);

        // Act
        var info = adapter.GetPluginInfo();

        // Assert
        Assert.Equal("my-plugin", info.PluginId);
        Assert.Equal("Test Plugin", info.PluginName);
        Assert.Equal("Test", info.PluginAuthor);
        Assert.Equal("1.0.0", info.PluginVersion);
        Assert.Equal("test-tool", info.CapabilityId);
    }

    [Fact]
    public void PluginToolAdapter_ImplementsIAssistantTool()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var capability = manifest.Capabilities[0];
        var host = CreatePluginHost();

        // Act
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        // Assert
        Assert.IsAssignableFrom<IAssistantTool>(adapter);
    }

    #endregion

    #region PluginToolExtensions Tests

    [Theory]
    [InlineData("plugin:test:tool", true)]
    [InlineData("plugin:my-plugin:my-tool", true)]
    [InlineData("tool:something", false)]
    [InlineData("regular-tool", false)]
    [InlineData("", false)]
    public void IsPluginTool_IdentifiesCorrectly(string toolId, bool expected)
    {
        Assert.Equal(expected, toolId.IsPluginTool());
    }

    [Theory]
    [InlineData("plugin:test-plugin:tool", "test-plugin")]
    [InlineData("plugin:my-plugin:my-tool", "my-plugin")]
    [InlineData("plugin:complex-name:something:else", "complex-name")]
    [InlineData("regular-tool", null)]
    [InlineData("tool:something", null)]
    public void GetPluginIdFromToolId_ExtractsCorrectly(string toolId, string? expected)
    {
        Assert.Equal(expected, toolId.GetPluginIdFromToolId());
    }

    [Theory]
    [InlineData("plugin:test-plugin:tool", "tool")]
    [InlineData("plugin:my-plugin:my-tool", "my-tool")]
    [InlineData("regular-tool", null)]
    [InlineData("tool:something", null)]
    public void GetCapabilityIdFromToolId_ExtractsCorrectly(string toolId, string? expected)
    {
        Assert.Equal(expected, toolId.GetCapabilityIdFromToolId());
    }

    #endregion

    #region PluginToolRegistry Tests

    [Fact]
    public async Task PluginToolRegistry_GetPluginTools_ReturnsEnabledPluginTools()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        var pluginToolRegistry = new PluginToolRegistry(host, toolRegistry);

        var manifest = CreateTestManifest("my-plugin", capabilities: new List<PluginCapability>
        {
            new() { ToolId = "tool1", Name = "Tool 1", Description = "First tool" },
            new() { ToolId = "tool2", Name = "Tool 2", Description = "Second tool" }
        });

        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);
        host.EnablePlugin("my-plugin");

        // Act
        var tools = pluginToolRegistry.GetPluginTools();

        // Assert
        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Id == "plugin:my-plugin:tool1");
        Assert.Contains(tools, t => t.Id == "plugin:my-plugin:tool2");
    }

    [Fact]
    public async Task PluginToolRegistry_GetPluginTools_ExcludesDisabledPlugins()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        var pluginToolRegistry = new PluginToolRegistry(host, toolRegistry);

        var manifest = CreateTestManifest("my-plugin");
        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);
        // Disable the plugin after loading
        host.DisablePlugin("my-plugin");

        // Act
        var tools = pluginToolRegistry.GetPluginTools();

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public async Task PluginToolRegistry_GetToolsForPlugin_ReturnsSpecificPluginTools()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        var pluginToolRegistry = new PluginToolRegistry(host, toolRegistry);

        var manifest = CreateTestManifest("my-plugin", capabilities: new List<PluginCapability>
        {
            new() { ToolId = "tool1", Name = "Tool 1", Description = "First tool" },
            new() { ToolId = "tool2", Name = "Tool 2", Description = "Second tool" }
        });

        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);

        // Act
        var tools = pluginToolRegistry.GetToolsForPlugin("my-plugin");

        // Assert
        Assert.Equal(2, tools.Count);
    }

    [Fact]
    public void PluginToolRegistry_GetToolsForPlugin_ReturnsEmptyForUnknownPlugin()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        var pluginToolRegistry = new PluginToolRegistry(host, toolRegistry);

        // Act
        var tools = pluginToolRegistry.GetToolsForPlugin("unknown-plugin");

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public async Task PluginToolRegistry_RegisterPluginTools_AddsToToolRegistry()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        var pluginToolRegistry = new PluginToolRegistry(host, toolRegistry);

        var manifest = CreateTestManifest("my-plugin");
        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);

        // Act
        pluginToolRegistry.RegisterPluginTools("my-plugin");

        // Assert
        var tool = toolRegistry.GetTool("plugin:my-plugin:test-tool");
        Assert.NotNull(tool);
        Assert.IsType<PluginToolAdapter>(tool);
    }

    [Fact]
    public async Task PluginToolRegistry_UnregisterPluginTools_RemovesFromToolRegistry()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        var pluginToolRegistry = new PluginToolRegistry(host, toolRegistry);

        var manifest = CreateTestManifest("my-plugin");
        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);
        pluginToolRegistry.RegisterPluginTools("my-plugin");

        // Pre-condition
        Assert.NotNull(toolRegistry.GetTool("plugin:my-plugin:test-tool"));

        // Act
        pluginToolRegistry.UnregisterPluginTools("my-plugin");

        // Assert
        Assert.Null(toolRegistry.GetTool("plugin:my-plugin:test-tool"));
    }

    [Fact]
    public async Task PluginToolRegistry_AutoRegistersOnPluginLoad()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        _ = new PluginToolRegistry(host, toolRegistry);

        var manifest = CreateTestManifest("my-plugin");
        var instance = new TestPluginInstance(manifest.Capabilities);

        // Act
        await host.LoadPluginAsync(manifest, instance);

        // Assert
        var tool = toolRegistry.GetTool("plugin:my-plugin:test-tool");
        Assert.NotNull(tool);
    }

    [Fact]
    public async Task PluginToolRegistry_AutoUnregistersOnPluginUnload()
    {
        // Arrange
        var host = CreatePluginHost();
        var toolRegistry = new ToolRegistry();
        _ = new PluginToolRegistry(host, toolRegistry);

        var manifest = CreateTestManifest("my-plugin");
        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);

        // Pre-condition
        Assert.NotNull(toolRegistry.GetTool("plugin:my-plugin:test-tool"));

        // Act
        await host.UnloadPluginAsync("my-plugin");

        // Assert
        Assert.Null(toolRegistry.GetTool("plugin:my-plugin:test-tool"));
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task PluginToolAdapter_ExecuteAsync_CallsPluginHost()
    {
        // Arrange
        var host = CreatePluginHost();

        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);
        host.EnablePlugin("test-plugin");

        var capability = manifest.Capabilities[0];
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        var context = new ToolExecutionContext(
            new Dictionary<string, object?> { ["input"] = "test data" },
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        // Act
        var result = await adapter.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task PluginToolAdapter_ExecuteAsync_ReturnsFailureWhenPluginFails()
    {
        // Arrange
        var host = CreatePluginHost();

        var manifest = CreateTestManifest();
        var failingInstance = new FailingPluginInstance();
        await host.LoadPluginAsync(manifest, failingInstance);
        host.EnablePlugin("test-plugin");

        var capability = manifest.Capabilities[0];
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        var context = new ToolExecutionContext(
            new Dictionary<string, object?> { ["input"] = "test data" },
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        // Act
        var result = await adapter.ExecuteAsync(context);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task PluginToolAdapter_ExecuteAsync_ReturnsFailureWhenPluginDisabled()
    {
        // Arrange
        var host = CreatePluginHost();

        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance(manifest.Capabilities);
        await host.LoadPluginAsync(manifest, instance);
        // Disable the plugin after loading
        host.DisablePlugin("test-plugin");

        var capability = manifest.Capabilities[0];
        var adapter = new PluginToolAdapter(host, "test-plugin", capability, manifest);

        var context = new ToolExecutionContext(
            new Dictionary<string, object?> { ["input"] = "test data" },
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        // Act
        var result = await adapter.ExecuteAsync(context);

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region Test Helpers

    private sealed class FakeNetworkGateway : INetworkGateway
    {
        public Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new NetworkResponse(
                IsSuccess: true,
                StatusCode: 200,
                Data: "{}",
                Error: null,
                Duration: TimeSpan.FromMilliseconds(10)));
        }
    }

    private sealed class TestPluginInstance : IPluginInstance
    {
        private readonly IReadOnlyList<PluginCapability> _capabilities;

        public TestPluginInstance(IReadOnlyList<PluginCapability> capabilities)
        {
            _capabilities = capabilities;
        }

        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult(PluginActionResult.Succeeded($"Executed: {actionId}"));
        }

        public IReadOnlyList<PluginCapability> GetCapabilities() => _capabilities;
    }

    private sealed class FailingPluginInstance : IPluginInstance
    {
        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult(PluginActionResult.Failed("Intentional failure"));
        }

        public IReadOnlyList<PluginCapability> GetCapabilities()
            => new List<PluginCapability>
            {
                new() { ToolId = "test-tool", Name = "Test Tool", Description = "A test tool" }
            };
    }

    #endregion
}
