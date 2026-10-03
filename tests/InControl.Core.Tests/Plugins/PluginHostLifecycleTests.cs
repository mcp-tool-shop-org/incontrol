using FluentAssertions;
using InControl.Core.Connectivity;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// Plugin host lifecycle edges: enable and disable, faulting, unloading, and disposal.
/// </summary>
public class PluginHostLifecycleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "host-lifecycle-" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryPluginAuditLog _audit = new();
    private readonly PluginHost _host;

    public PluginHostLifecycleTests()
    {
        var connectivity = new ConnectivityManager(new NullGateway(), Path.Combine(_root, "connectivity.json"));
        _host = new PluginHost(new PluginSandbox(connectivity, Path.Combine(_root, "storage")), _audit);
    }

    public void Dispose()
    {
        _host.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PluginManifest Manifest(string id = "com.test.lifecycle") => new()
    {
        Id = id,
        Version = "1.0.0",
        Name = "Lifecycle",
        Author = "Tests",
        Description = "Lifecycle test plugin",
        Capabilities = [new PluginCapability { ToolId = "t", Name = "T", Description = "Does a thing" }]
    };

    private static readonly IReadOnlyDictionary<string, object?> NoParameters = new Dictionary<string, object?>();

    [Fact]
    public async Task UnknownPlugin_CannotBeEnabledDisabledOrUnloaded()
    {
        _host.EnablePlugin("com.test.none").Should().BeFalse();
        _host.DisablePlugin("com.test.none").Should().BeFalse();
        (await _host.UnloadPluginAsync("com.test.none")).Should().BeFalse();
        (await _host.ExecuteAsync("com.test.none", "a", NoParameters)).Error.Should().Be("Plugin not found");
    }

    [Fact]
    public async Task EnableAndDisable_AreIdempotent_AndOnlyTheFirstChangeIsAudited()
    {
        await _host.LoadPluginAsync(Manifest(), new Instance());

        _host.DisablePlugin("com.test.lifecycle").Should().BeTrue();
        _host.DisablePlugin("com.test.lifecycle").Should().BeTrue();
        _host.EnablePlugin("com.test.lifecycle").Should().BeTrue();
        _host.EnablePlugin("com.test.lifecycle").Should().BeTrue();

        _audit.GetEntriesByType(PluginAuditEventType.Disabled).Should().ContainSingle();
        _audit.GetEntriesByType(PluginAuditEventType.Enabled).Should().ContainSingle();
    }

    [Fact]
    public async Task DisabledPlugin_RefusesToExecute_UntilEnabledAgain()
    {
        var instance = new Instance();
        await _host.LoadPluginAsync(Manifest(), instance);
        _host.DisablePlugin("com.test.lifecycle");

        var refused = await _host.ExecuteAsync("com.test.lifecycle", "run", NoParameters);
        _host.EnablePlugin("com.test.lifecycle");
        var allowed = await _host.ExecuteAsync("com.test.lifecycle", "run", NoParameters);

        refused.Success.Should().BeFalse();
        refused.Error.Should().Be("Plugin is disabled");
        instance.Executions.Should().Be(1);
        allowed.Success.Should().BeTrue();
    }

    [Fact]
    public async Task APluginThatThrows_IsFaulted_ReportedAndStopsRunning_UntilReEnabled()
    {
        var instance = new Instance { Throw = new InvalidOperationException("boom") };
        await _host.LoadPluginAsync(Manifest(), instance);
        PluginErrorEventArgs? raised = null;
        _host.PluginError += (_, e) => raised = e;

        var failed = await _host.ExecuteAsync("com.test.lifecycle", "run", NoParameters);
        var second = await _host.ExecuteAsync("com.test.lifecycle", "run", NoParameters);

        failed.Success.Should().BeFalse();
        failed.Error.Should().Be("Execution failed: boom");
        raised!.PluginId.Should().Be("com.test.lifecycle");
        raised.Action.Should().Be("run");
        raised.Exception.Message.Should().Be("boom");
        _host.GetPlugin("com.test.lifecycle")!.State.Should().Be(PluginState.Faulted);
        second.Error.Should().Be("Plugin is disabled");
        instance.Executions.Should().Be(1);
        _audit.GetEntriesByType(PluginAuditEventType.ActionFailed).Should().ContainSingle();

        instance.Throw = null;
        _host.EnablePlugin("com.test.lifecycle").Should().BeTrue();
        (await _host.ExecuteAsync("com.test.lifecycle", "run", NoParameters)).Success.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeFailing_LeavesNothingLoaded_AndIsAudited()
    {
        var result = await _host.LoadPluginAsync(Manifest(), new Instance { InitializeThrows = new IOException("no init") });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("no init");
        _host.LoadedPlugins.Should().BeEmpty();
        _audit.GetEntriesByType(PluginAuditEventType.Error).Should().ContainSingle();
    }

    [Fact]
    public async Task DisableAll_DisablesEnabledPlugins_AndAuditsEachOnce()
    {
        await _host.LoadPluginAsync(Manifest("com.test.one"), new Instance());
        await _host.LoadPluginAsync(Manifest("com.test.two"), new Instance());
        _host.DisablePlugin("com.test.two");

        _host.DisableAllPlugins();
        _host.DisableAllPlugins();

        _host.LoadedPlugins.Should().OnlyContain(p => p.State == PluginState.Disabled);
        _audit.GetEntriesByType(PluginAuditEventType.Disabled).Should().HaveCount(2);
    }

    [Fact]
    public async Task Unload_DisposesTheInstance_AndRaisesTheEvent()
    {
        var instance = new Instance();
        await _host.LoadPluginAsync(Manifest(), instance);
        string? unloaded = null;
        _host.PluginUnloaded += (_, e) => unloaded = e.PluginId;

        (await _host.UnloadPluginAsync("com.test.lifecycle")).Should().BeTrue();

        instance.Disposed.Should().BeTrue();
        unloaded.Should().Be("com.test.lifecycle");
        _host.GetPlugin("com.test.lifecycle").Should().BeNull();
    }

    [Fact]
    public async Task Unload_WhenDisposalThrows_ReturnsFalse_AndReportsTheError()
    {
        await _host.LoadPluginAsync(Manifest(), new Instance { DisposeThrows = true });
        PluginErrorEventArgs? raised = null;
        _host.PluginError += (_, e) => raised = e;

        (await _host.UnloadPluginAsync("com.test.lifecycle")).Should().BeFalse();

        raised!.Action.Should().Be("Unload failed");
        _audit.GetEntriesByType(PluginAuditEventType.Error).Should().ContainSingle();
    }

    [Fact]
    public async Task Unload_UsesAsyncDisposalWhenThePluginOffersIt()
    {
        var instance = new AsyncInstance();
        await _host.LoadPluginAsync(Manifest(), instance);

        (await _host.UnloadPluginAsync("com.test.lifecycle")).Should().BeTrue();

        instance.AsyncDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_DisposesEveryPlugin_ToleratingOneThatThrows_AndBlocksFurtherUse()
    {
        var good = new Instance();
        var bad = new Instance { DisposeThrows = true };
        await _host.LoadPluginAsync(Manifest("com.test.good"), good);
        await _host.LoadPluginAsync(Manifest("com.test.bad"), bad);

        _host.Dispose();
        _host.Dispose();

        good.Disposed.Should().BeTrue();
        _host.LoadedPlugins.Should().BeEmpty();
        var act = async () => await _host.LoadPluginAsync(Manifest("com.test.late"), new Instance());
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_DisposesSyncAndAsyncPlugins_ToleratingFailures()
    {
        var sync = new Instance();
        var asyncInstance = new AsyncInstance();
        var bad = new Instance { DisposeThrows = true };
        await _host.LoadPluginAsync(Manifest("com.test.sync"), sync);
        await _host.LoadPluginAsync(Manifest("com.test.async"), asyncInstance);
        await _host.LoadPluginAsync(Manifest("com.test.bad"), bad);

        await _host.DisposeAsync();
        await _host.DisposeAsync();

        sync.Disposed.Should().BeTrue();
        asyncInstance.AsyncDisposed.Should().BeTrue();
        _host.LoadedPlugins.Should().BeEmpty();
    }

    private sealed class NullGateway : INetworkGateway
    {
        public Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default) =>
            Task.FromResult(new NetworkResponse(true, 200, "", null, TimeSpan.Zero));
    }

    private class Instance : IPluginInstance, IDisposable
    {
        public Exception? Throw { get; set; }
        public Exception? InitializeThrows { get; set; }
        public bool DisposeThrows { get; set; }
        public bool Disposed { get; private set; }
        public int Executions { get; private set; }

        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
        {
            if (InitializeThrows is not null)
            {
                throw InitializeThrows;
            }

            return Task.CompletedTask;
        }

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default)
        {
            Executions++;
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.FromResult(PluginActionResult.Succeeded("ok"));
        }

        public IReadOnlyList<PluginCapability> GetCapabilities() => [];

        public void Dispose()
        {
            Disposed = true;
            if (DisposeThrows)
            {
                throw new InvalidOperationException("dispose failed");
            }
        }
    }

    private sealed class AsyncInstance : IPluginInstance, IAsyncDisposable
    {
        public bool AsyncDisposed { get; private set; }

        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default) => Task.FromResult(PluginActionResult.Succeeded(null));

        public IReadOnlyList<PluginCapability> GetCapabilities() => [];

        public ValueTask DisposeAsync()
        {
            AsyncDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
