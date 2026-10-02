using FluentAssertions;
using InControl.Core.Connectivity;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

public class PluginHostTests : IDisposable
{
    private readonly string _storagePath;
    private readonly string _connectivityPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _connectivity;
    private readonly PluginSandbox _sandbox;
    private readonly InMemoryPluginAuditLog _auditLog;
    private readonly PluginHost _host;

    public PluginHostTests()
    {
        _storagePath = Path.Combine(Path.GetTempPath(), $"plugin-test-{Guid.NewGuid()}");
        _connectivityPath = Path.Combine(Path.GetTempPath(), $"conn-test-{Guid.NewGuid()}.json");
        Directory.CreateDirectory(_storagePath);

        _gateway = new FakeNetworkGateway();
        _connectivity = new ConnectivityManager(_gateway, _connectivityPath);
        _sandbox = new PluginSandbox(_connectivity, _storagePath);
        _auditLog = new InMemoryPluginAuditLog();
        _host = new PluginHost(_sandbox, _auditLog);
    }

    public void Dispose()
    {
        _host.Dispose();

        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, true);

        if (File.Exists(_connectivityPath))
            File.Delete(_connectivityPath);
    }

    [Fact]
    public async Task LoadPluginAsync_ValidManifest_Succeeds()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();

        var result = await _host.LoadPluginAsync(manifest, instance);

        result.Success.Should().BeTrue();
        _host.LoadedPlugins.Should().HaveCount(1);
        _host.GetPlugin(manifest.Id).Should().NotBeNull();
    }

    [Fact]
    public async Task LoadPluginAsync_InvalidManifest_Fails()
    {
        var manifest = CreateValidManifest() with { Id = "" };
        var instance = new FakePluginInstance();

        var result = await _host.LoadPluginAsync(manifest, instance);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid manifest");
        _host.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPluginAsync_DuplicateId_Fails()
    {
        var manifest = CreateValidManifest();
        var instance1 = new FakePluginInstance();
        var instance2 = new FakePluginInstance();

        await _host.LoadPluginAsync(manifest, instance1);
        var result = await _host.LoadPluginAsync(manifest, instance2);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already loaded");
    }

    [Fact]
    public async Task LoadPluginAsync_RaisesEvent()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();
        LoadedPlugin? loadedPlugin = null;
        _host.PluginLoaded += (_, e) => loadedPlugin = e.Plugin;

        await _host.LoadPluginAsync(manifest, instance);

        loadedPlugin.Should().NotBeNull();
        loadedPlugin!.Manifest.Id.Should().Be(manifest.Id);
    }

    [Fact]
    public async Task UnloadPluginAsync_LoadedPlugin_Succeeds()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        var result = await _host.UnloadPluginAsync(manifest.Id);

        result.Should().BeTrue();
        _host.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task UnloadPluginAsync_NotLoaded_ReturnsFalse()
    {
        var result = await _host.UnloadPluginAsync("not-loaded");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UnloadPluginAsync_RaisesEvent()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        string? unloadedId = null;
        _host.PluginUnloaded += (_, e) => unloadedId = e.PluginId;

        await _host.UnloadPluginAsync(manifest.Id);

        unloadedId.Should().Be(manifest.Id);
    }

    [Fact]
    public async Task EnablePlugin_DisabledPlugin_Enables()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        _host.DisablePlugin(manifest.Id);

        var result = _host.EnablePlugin(manifest.Id);

        result.Should().BeTrue();
        _host.GetPlugin(manifest.Id)!.State.Should().Be(PluginState.Enabled);
    }

    [Fact]
    public async Task DisablePlugin_EnabledPlugin_Disables()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        var result = _host.DisablePlugin(manifest.Id);

        result.Should().BeTrue();
        _host.GetPlugin(manifest.Id)!.State.Should().Be(PluginState.Disabled);
    }

    [Fact]
    public async Task ExecuteAsync_EnabledPlugin_Succeeds()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance { ResultOutput = "Hello!" };
        await _host.LoadPluginAsync(manifest, instance);

        var result = await _host.ExecuteAsync(
            manifest.Id,
            "test-action",
            new Dictionary<string, object?>());

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Hello!");
    }

    [Fact]
    public async Task ExecuteAsync_DisabledPlugin_Fails()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();
        await _host.LoadPluginAsync(manifest, instance);
        _host.DisablePlugin(manifest.Id);

        var result = await _host.ExecuteAsync(
            manifest.Id,
            "test-action",
            new Dictionary<string, object?>());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disabled");
    }

    [Fact]
    public async Task ExecuteAsync_NotLoadedPlugin_Fails()
    {
        var result = await _host.ExecuteAsync(
            "not-loaded",
            "test-action",
            new Dictionary<string, object?>());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingPlugin_MarksAsFaulted()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance { ShouldThrow = true };
        await _host.LoadPluginAsync(manifest, instance);

        var result = await _host.ExecuteAsync(
            manifest.Id,
            "test-action",
            new Dictionary<string, object?>());

        result.Success.Should().BeFalse();
        _host.GetPlugin(manifest.Id)!.State.Should().Be(PluginState.Faulted);
    }

    [Fact]
    public async Task DisableAllPlugins_DisablesAll()
    {
        var manifest1 = CreateValidManifest() with { Id = "plugin-1" };
        var manifest2 = CreateValidManifest() with { Id = "plugin-2" };
        await _host.LoadPluginAsync(manifest1, new FakePluginInstance());
        await _host.LoadPluginAsync(manifest2, new FakePluginInstance());

        _host.DisableAllPlugins();

        _host.GetPlugin("plugin-1")!.State.Should().Be(PluginState.Disabled);
        _host.GetPlugin("plugin-2")!.State.Should().Be(PluginState.Disabled);
    }

    [Fact]
    public async Task AuditLog_RecordsPluginLoad()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();

        await _host.LoadPluginAsync(manifest, instance);

        var entries = _auditLog.GetRecentEntries();
        entries.Should().ContainSingle(e =>
            e.PluginId == manifest.Id &&
            e.EventType == PluginAuditEventType.Loaded);
    }

    [Fact]
    public async Task AuditLog_RecordsActionExecution()
    {
        var manifest = CreateValidManifest();
        var instance = new FakePluginInstance();
        await _host.LoadPluginAsync(manifest, instance);

        await _host.ExecuteAsync(manifest.Id, "test-action", new Dictionary<string, object?>());

        var entries = _auditLog.GetRecentEntries();
        entries.Should().Contain(e => e.EventType == PluginAuditEventType.ActionStarted);
        entries.Should().Contain(e => e.EventType == PluginAuditEventType.ActionCompleted);
    }

    private static PluginManifest CreateValidManifest()
    {
        return new PluginManifest
        {
            Id = "com.test.sample-plugin",
            Version = "1.0.0",
            Name = "Sample Plugin",
            Author = "Test Author",
            Description = "A test plugin"
        };
    }

    private sealed class FakePluginInstance : IPluginInstance
    {
        public object? ResultOutput { get; set; }
        public bool ShouldThrow { get; set; }

        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default)
        {
            if (ShouldThrow)
                throw new InvalidOperationException("Simulated failure");

            return Task.FromResult(PluginActionResult.Succeeded(ResultOutput));
        }

        public IReadOnlyList<PluginCapability> GetCapabilities()
        {
            return [];
        }
    }

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
}

public class PluginSandboxTests : IDisposable
{
    private readonly string _storagePath;
    private readonly string _connectivityPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _connectivity;
    private readonly PluginSandbox _sandbox;

    public PluginSandboxTests()
    {
        _storagePath = Path.Combine(Path.GetTempPath(), $"sandbox-test-{Guid.NewGuid()}");
        _connectivityPath = Path.Combine(Path.GetTempPath(), $"conn-test-{Guid.NewGuid()}.json");
        Directory.CreateDirectory(_storagePath);

        _gateway = new FakeNetworkGateway();
        _connectivity = new ConnectivityManager(_gateway, _connectivityPath);
        _sandbox = new PluginSandbox(_connectivity, _storagePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, true);

        if (File.Exists(_connectivityPath))
            File.Delete(_connectivityPath);
    }

    [Fact]
    public void CreateContext_ReturnsContextWithManifest()
    {
        var manifest = CreateManifestWithPermissions();

        var context = _sandbox.CreateContext(manifest);

        context.Manifest.Should().Be(manifest);
    }

    [Fact]
    public void HasPermission_DeclaredPermission_ReturnsTrue()
    {
        var manifest = CreateManifestWithPermissions();
        var context = _sandbox.CreateContext(manifest);

        var hasRead = context.HasPermission(PermissionType.File, PermissionAccess.Read, "/documents/test.txt");

        hasRead.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_UndeclaredPermission_ReturnsFalse()
    {
        var manifest = CreateManifestWithPermissions();
        var context = _sandbox.CreateContext(manifest);

        var hasNetwork = context.HasPermission(PermissionType.Network, PermissionAccess.Read);

        hasNetwork.Should().BeFalse();
    }

    [Fact]
    public void FileAccess_PermittedPath_Allowed()
    {
        var manifest = CreateManifestWithPermissions();
        var context = _sandbox.CreateContext(manifest);

        var permitted = context.Files.IsPathPermitted("/documents/test.txt", PermissionAccess.Read);

        permitted.Should().BeTrue();
    }

    [Fact]
    public void FileAccess_UnpermittedPath_Denied()
    {
        var manifest = CreateManifestWithPermissions();
        var context = _sandbox.CreateContext(manifest);

        var permitted = context.Files.IsPathPermitted("/secrets/password.txt", PermissionAccess.Read);

        permitted.Should().BeFalse();
    }

    [Fact]
    public void NetworkAccess_IsAvailable_ReflectsConnectivity()
    {
        var manifest = CreateManifestWithPermissions();
        var context = _sandbox.CreateContext(manifest);

        // Default is offline
        context.Network.IsAvailable.Should().BeFalse();

        // Enable connectivity
        _connectivity.SetMode(ConnectivityMode.Connected);
        context.Network.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Storage_SetAndGet_Works()
    {
        var manifest = CreateManifestWithPermissions();
        var context = _sandbox.CreateContext(manifest);

        await context.Storage.SetAsync("test-key", "test-value");
        var value = await context.Storage.GetAsync("test-key");

        value.Should().Be("test-value");
    }

    [Fact]
    public async Task Storage_IsolatedByPlugin()
    {
        var manifest1 = CreateManifestWithPermissions() with { Id = "plugin-1" };
        var manifest2 = CreateManifestWithPermissions() with { Id = "plugin-2" };

        var context1 = _sandbox.CreateContext(manifest1);
        var context2 = _sandbox.CreateContext(manifest2);

        await context1.Storage.SetAsync("key", "value1");
        await context2.Storage.SetAsync("key", "value2");

        var value1 = await context1.Storage.GetAsync("key");
        var value2 = await context2.Storage.GetAsync("key");

        value1.Should().Be("value1");
        value2.Should().Be("value2");
    }

    private static PluginManifest CreateManifestWithPermissions()
    {
        return new PluginManifest
        {
            Id = "com.test.sandbox-plugin",
            Version = "1.0.0",
            Name = "Sandbox Test Plugin",
            Author = "Test Author",
            Description = "A test plugin for sandbox testing",
            RiskLevel = PluginRiskLevel.ReadOnly,
            Permissions = new List<PluginPermission>
            {
                new()
                {
                    Type = PermissionType.File,
                    Access = PermissionAccess.Read,
                    Scope = "/documents"
                },
                new()
                {
                    Type = PermissionType.Memory,
                    Access = PermissionAccess.Read
                }
            }
        };
    }

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
}

// PluginAuditLogTests moved to separate file: PluginAuditLogTests.cs
