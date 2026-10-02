using InControl.Core.Connectivity;
using InControl.Core.Plugins;
using InControl.ViewModels.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InControl.Core.Tests.ViewModels;

/// <summary>
/// Tests for PluginManagerViewModel.
/// </summary>
public class PluginManagerViewModelTests : IDisposable
{
    private readonly string _tempPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _connectivity;
    private readonly PluginHost _host;
    private readonly InMemoryPluginAuditLog _auditLog;
    private readonly PluginManagerViewModel _viewModel;

    public PluginManagerViewModelTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"plugin-vm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        _gateway = new FakeNetworkGateway();
        _connectivity = new ConnectivityManager(_gateway, Path.Combine(_tempPath, "connectivity.json"));

        var storagePath = Path.Combine(_tempPath, "plugin-storage");
        var sandbox = new PluginSandbox(_connectivity, storagePath);
        _auditLog = new InMemoryPluginAuditLog();
        _host = new PluginHost(sandbox, _auditLog);

        var logger = NullLogger<PluginManagerViewModel>.Instance;
        _viewModel = new PluginManagerViewModel(_host, _auditLog, logger);
    }

    public void Dispose()
    {
        _host.Dispose();
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, true); } catch { }
        }
    }

    private static PluginManifest CreateTestManifest(string id = "test-plugin")
    {
        return new PluginManifest
        {
            Id = id,
            Version = "1.0.0",
            Name = $"Test Plugin ({id})",
            Author = "Test Author",
            Description = "A test plugin for testing",
            RiskLevel = PluginRiskLevel.ReadOnly,
            Capabilities = new List<PluginCapability>
            {
                new()
                {
                    ToolId = "test-tool",
                    Name = "Test Tool",
                    Description = "A test tool"
                }
            }
        };
    }

    #region Initial State Tests

    [Fact]
    public void InitialState_HasNoPlugins()
    {
        Assert.Empty(_viewModel.Plugins);
        Assert.False(_viewModel.HasPlugins);
        Assert.Equal(0, _viewModel.TotalCount);
        Assert.Equal(0, _viewModel.EnabledCount);
        Assert.Equal("No plugins installed", _viewModel.StatusSummary);
    }

    [Fact]
    public void InitialState_HasNoSelectedPlugin()
    {
        Assert.Null(_viewModel.SelectedPlugin);
    }

    #endregion

    #region Plugin Loading Tests

    [Fact]
    public async Task LoadPlugin_AppearsInPluginsList()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();

        // Act
        await _host.LoadPluginAsync(manifest, instance);

        // Assert
        Assert.Single(_viewModel.Plugins);
        Assert.True(_viewModel.HasPlugins);
        Assert.Equal(1, _viewModel.TotalCount);
    }

    [Fact]
    public async Task LoadPlugin_PluginIsEnabled()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();

        // Act
        await _host.LoadPluginAsync(manifest, instance);

        // Assert
        Assert.Equal(1, _viewModel.EnabledCount);
        var plugin = _viewModel.Plugins.First();
        Assert.True(plugin.IsEnabled);
    }

    [Fact]
    public async Task LoadMultiplePlugins_AllAppearInList()
    {
        // Arrange & Act
        for (int i = 1; i <= 3; i++)
        {
            var manifest = CreateTestManifest($"plugin-{i}");
            var instance = new TestPluginInstance();
            await _host.LoadPluginAsync(manifest, instance);
        }

        // Assert
        Assert.Equal(3, _viewModel.Plugins.Count);
        Assert.Equal(3, _viewModel.TotalCount);
        Assert.Equal(3, _viewModel.EnabledCount);
    }

    #endregion

    #region Plugin Enable/Disable Tests

    [Fact]
    public async Task DisablePlugin_UpdatesViewModel()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        var plugin = _viewModel.Plugins.First();

        // Act
        _viewModel.DisablePlugin("test-plugin");

        // Assert
        Assert.Equal(0, _viewModel.EnabledCount);
        Assert.Equal("1 plugin (disabled)", _viewModel.StatusSummary);
    }

    [Fact]
    public async Task EnablePlugin_AfterDisable_UpdatesViewModel()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        _viewModel.DisablePlugin("test-plugin");

        // Act
        _viewModel.EnablePlugin("test-plugin");

        // Assert
        Assert.Equal(1, _viewModel.EnabledCount);
        Assert.Equal("1 plugin enabled", _viewModel.StatusSummary);
    }

    [Fact]
    public async Task DisableAllPlugins_DisablesAll()
    {
        // Arrange
        for (int i = 1; i <= 3; i++)
        {
            var manifest = CreateTestManifest($"plugin-{i}");
            var instance = new TestPluginInstance();
            await _host.LoadPluginAsync(manifest, instance);
        }
        Assert.Equal(3, _viewModel.EnabledCount);

        // Act
        _viewModel.DisableAllPluginsCommand.Execute(null);

        // Assert
        Assert.Equal(0, _viewModel.EnabledCount);
        Assert.Equal(3, _viewModel.TotalCount);
    }

    #endregion

    #region Plugin Unload Tests

    [Fact]
    public async Task UnloadPlugin_RemovesFromList()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        Assert.Single(_viewModel.Plugins);

        // Act
        await _host.UnloadPluginAsync("test-plugin");

        // Assert
        Assert.Empty(_viewModel.Plugins);
        Assert.False(_viewModel.HasPlugins);
    }

    #endregion

    #region PluginItemViewModel Tests

    [Fact]
    public async Task PluginItemViewModel_DisplaysCorrectInfo()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        // Act
        var pluginVm = _viewModel.Plugins.First();

        // Assert
        Assert.Equal("test-plugin", pluginVm.PluginId);
        Assert.Equal("Test Plugin (test-plugin)", pluginVm.Name);
        Assert.Equal("1.0.0", pluginVm.Version);
        Assert.Equal("Test Author", pluginVm.Author);
        Assert.Equal("A test plugin for testing", pluginVm.Description);
        Assert.Equal(PluginRiskLevel.ReadOnly, pluginVm.RiskLevel);
        Assert.Equal("Read-Only", pluginVm.RiskLevelDisplay);
        Assert.Equal(1, pluginVm.CapabilityCount);
    }

    [Fact]
    public async Task PluginItemViewModel_ToggleEnabled_DisablesPlugin()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        var pluginVm = _viewModel.Plugins.First();
        Assert.True(pluginVm.IsEnabled);

        // Act
        pluginVm.ToggleEnabledCommand.Execute(null);

        // Assert - get the refreshed PluginItemVM from the collection
        var refreshedVm = _viewModel.Plugins.First(p => p.PluginId == "test-plugin");
        Assert.False(refreshedVm.IsEnabled);
        Assert.True(refreshedVm.IsDisabled);
    }

    [Fact]
    public async Task PluginItemViewModel_ToggleEnabled_EnablesPlugin()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        var pluginVm = _viewModel.Plugins.First();
        pluginVm.ToggleEnabledCommand.Execute(null); // Disable first

        // Get the refreshed VM after disabling
        var disabledVm = _viewModel.Plugins.First(p => p.PluginId == "test-plugin");
        Assert.True(disabledVm.IsDisabled);

        // Act
        disabledVm.ToggleEnabledCommand.Execute(null);

        // Assert - get the refreshed PluginItemVM from the collection
        var enabledVm = _viewModel.Plugins.First(p => p.PluginId == "test-plugin");
        Assert.True(enabledVm.IsEnabled);
        Assert.False(enabledVm.IsDisabled);
    }

    [Theory]
    [InlineData(PluginRiskLevel.ReadOnly, "Green", "Read-Only")]
    [InlineData(PluginRiskLevel.LocalMutation, "Orange", "Local Mutation")]
    [InlineData(PluginRiskLevel.Network, "Red", "Network Access")]
    // Note: SystemAdjacent is not allowed in Phase 8, so not tested via loading
    public async Task PluginItemViewModel_RiskLevelDisplay_IsCorrect(
        PluginRiskLevel riskLevel, string expectedColor, string expectedDisplay)
    {
        // Arrange
        var manifest = new PluginManifest
        {
            Id = "risk-test",
            Version = "1.0.0",
            Name = "Risk Test Plugin",
            Author = "Test",
            Description = "Testing risk levels",
            RiskLevel = riskLevel,
            Capabilities = new List<PluginCapability>
            {
                new() { ToolId = "test", Name = "Test", Description = "Test" }
            }
        };
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        // Act
        var pluginVm = _viewModel.Plugins.First();

        // Assert
        Assert.Equal(expectedColor, pluginVm.RiskLevelColor);
        Assert.Equal(expectedDisplay, pluginVm.RiskLevelDisplay);
    }

    #endregion

    #region Activity Tests

    [Fact]
    public async Task LoadPlugin_CreatesAuditEntry()
    {
        // Arrange
        var manifest = CreateTestManifest();
        var instance = new TestPluginInstance();

        // Act
        await _host.LoadPluginAsync(manifest, instance);

        // Assert - audit log should have the load entry
        var entries = _auditLog.GetRecentEntries(10);
        Assert.Contains(entries, e => e.PluginId == "test-plugin" && e.EventType == PluginAuditEventType.Loaded);
    }

    [Fact]
    public void ClearAuditLog_ClearsRecentActivity()
    {
        // Arrange - add some activity
        _auditLog.LogPluginLoaded("test", "1.0.0");
        _auditLog.LogPluginEnabled("test");

        // Act
        _viewModel.ClearAuditLogCommand.Execute(null);

        // Assert
        Assert.Empty(_viewModel.RecentActivity);
        Assert.False(_viewModel.HasActivity);
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
        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult(PluginActionResult.Succeeded("OK"));
        }

        public IReadOnlyList<PluginCapability> GetCapabilities()
            => new List<PluginCapability>
            {
                new() { ToolId = "test-tool", Name = "Test Tool", Description = "A test tool" }
            };
    }

    #endregion
}

/// <summary>
/// Tests for PluginDetailViewModel.
/// </summary>
public class PluginDetailViewModelTests : IDisposable
{
    private readonly string _tempPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _connectivity;
    private readonly PluginHost _host;
    private readonly InMemoryPluginAuditLog _auditLog;
    private readonly PluginDetailViewModel _viewModel;

    public PluginDetailViewModelTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"plugin-detail-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        _gateway = new FakeNetworkGateway();
        _connectivity = new ConnectivityManager(_gateway, Path.Combine(_tempPath, "connectivity.json"));

        var storagePath = Path.Combine(_tempPath, "plugin-storage");
        var sandbox = new PluginSandbox(_connectivity, storagePath);
        _auditLog = new InMemoryPluginAuditLog();
        _host = new PluginHost(sandbox, _auditLog);

        var logger = NullLogger<PluginDetailViewModel>.Instance;
        _viewModel = new PluginDetailViewModel(_host, _auditLog, logger);
    }

    public void Dispose()
    {
        _host.Dispose();
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, true); } catch { }
        }
    }

    [Fact]
    public async Task LoadPlugin_PopulatesManifest()
    {
        // Arrange
        var manifest = new PluginManifest
        {
            Id = "detail-test",
            Version = "2.0.0",
            Name = "Detail Test Plugin",
            Author = "Detail Author",
            Description = "Testing detail view",
            RiskLevel = PluginRiskLevel.LocalMutation,
            Capabilities = new List<PluginCapability>
            {
                new() { ToolId = "tool1", Name = "Tool 1", Description = "First" },
                new() { ToolId = "tool2", Name = "Tool 2", Description = "Second" }
            },
            Permissions = new List<PluginPermission>
            {
                new() { Type = PermissionType.File, Access = PermissionAccess.Read, Scope = "/temp" }
            }
        };
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        // Act
        _viewModel.LoadPlugin("detail-test");

        // Assert
        Assert.Equal("detail-test", _viewModel.PluginId);
        Assert.NotNull(_viewModel.Manifest);
        Assert.Equal("Detail Test Plugin", _viewModel.Manifest.Name);
        Assert.Equal(PluginState.Enabled, _viewModel.State);
    }

    [Fact]
    public async Task LoadPlugin_PopulatesCapabilities()
    {
        // Arrange
        var manifest = new PluginManifest
        {
            Id = "cap-test",
            Version = "1.0.0",
            Name = "Capability Test",
            Author = "Test",
            Description = "Testing capabilities",
            Capabilities = new List<PluginCapability>
            {
                new() { ToolId = "read-tool", Name = "Read Tool", Description = "Reads data", RequiresNetwork = false, ModifiesState = false },
                new() { ToolId = "write-tool", Name = "Write Tool", Description = "Writes data", RequiresNetwork = false, ModifiesState = false }
            }
        };
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        // Act
        _viewModel.LoadPlugin("cap-test");

        // Assert
        Assert.Equal(2, _viewModel.Capabilities.Count);
        Assert.Contains(_viewModel.Capabilities, c => c.ToolId == "read-tool" && !c.ModifiesState);
        Assert.Contains(_viewModel.Capabilities, c => c.ToolId == "write-tool");
    }

    [Fact]
    public async Task LoadPlugin_PopulatesPermissions()
    {
        // Arrange
        var manifest = new PluginManifest
        {
            Id = "perm-test",
            Version = "1.0.0",
            Name = "Permission Test",
            Author = "Test",
            Description = "Testing permissions",
            RiskLevel = PluginRiskLevel.LocalMutation, // Required for write permission
            Capabilities = new List<PluginCapability>
            {
                new() { ToolId = "test", Name = "Test", Description = "Test" }
            },
            Permissions = new List<PluginPermission>
            {
                new() { Type = PermissionType.File, Access = PermissionAccess.Read, Scope = "/data" },
                new() { Type = PermissionType.Memory, Access = PermissionAccess.Write }
            }
        };
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        // Act
        _viewModel.LoadPlugin("perm-test");

        // Assert
        Assert.Equal(2, _viewModel.Permissions.Count);
        Assert.Contains(_viewModel.Permissions, p => p.Type == PermissionType.File);
        Assert.Contains(_viewModel.Permissions, p => p.Type == PermissionType.Memory);
    }

    [Fact]
    public void LoadPlugin_UnknownPlugin_SetsError()
    {
        // Act
        _viewModel.LoadPlugin("nonexistent-plugin");

        // Assert
        Assert.True(_viewModel.HasError);
        Assert.Contains("not found", _viewModel.ErrorMessage);
    }

    [Fact]
    public async Task EnableCommand_EnablesPlugin()
    {
        // Arrange
        var manifest = new PluginManifest
        {
            Id = "enable-test",
            Version = "1.0.0",
            Name = "Enable Test",
            Author = "Test",
            Description = "Test",
            Capabilities = new List<PluginCapability>
            {
                new() { ToolId = "test", Name = "Test", Description = "Test" }
            }
        };
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        _host.DisablePlugin("enable-test");
        _viewModel.LoadPlugin("enable-test");
        Assert.Equal(PluginState.Disabled, _viewModel.State);

        // Act
        _viewModel.EnableCommand.Execute(null);

        // Assert
        Assert.Equal(PluginState.Enabled, _viewModel.State);
    }

    [Fact]
    public async Task DisableCommand_DisablesPlugin()
    {
        // Arrange
        var manifest = new PluginManifest
        {
            Id = "disable-test",
            Version = "1.0.0",
            Name = "Disable Test",
            Author = "Test",
            Description = "Test",
            Capabilities = new List<PluginCapability>
            {
                new() { ToolId = "test", Name = "Test", Description = "Test" }
            }
        };
        var instance = new TestPluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        _viewModel.LoadPlugin("disable-test");
        Assert.Equal(PluginState.Enabled, _viewModel.State);

        // Act
        _viewModel.DisableCommand.Execute(null);

        // Assert
        Assert.Equal(PluginState.Disabled, _viewModel.State);
    }

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
        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult(PluginActionResult.Succeeded("OK"));
        }

        public IReadOnlyList<PluginCapability> GetCapabilities()
            => new List<PluginCapability>
            {
                new() { ToolId = "test", Name = "Test", Description = "Test" }
            };
    }

    #endregion
}
