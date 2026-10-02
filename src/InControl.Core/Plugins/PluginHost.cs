using InControl.Core.Errors;

namespace InControl.Core.Plugins;

/// <summary>
/// Hosts plugin execution within a sandboxed environment.
/// All plugin actions flow through mediated APIs - no direct system access.
/// </summary>
public sealed class PluginHost : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<string, LoadedPlugin> _plugins = new();
    private readonly IPluginSandbox _sandbox;
    private readonly IPluginAuditLog _auditLog;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Raised when a plugin is loaded.
    /// </summary>
    public event EventHandler<PluginLoadedEventArgs>? PluginLoaded;

    /// <summary>
    /// Raised when a plugin is unloaded.
    /// </summary>
    public event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;

    /// <summary>
    /// Raised when a plugin execution fails.
    /// </summary>
    public event EventHandler<PluginErrorEventArgs>? PluginError;

    public PluginHost(IPluginSandbox sandbox, IPluginAuditLog auditLog)
    {
        _sandbox = sandbox;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    public IReadOnlyList<LoadedPlugin> LoadedPlugins
    {
        get
        {
            lock (_lock)
            {
                return _plugins.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Gets a specific loaded plugin by ID.
    /// </summary>
    public LoadedPlugin? GetPlugin(string pluginId)
    {
        lock (_lock)
        {
            return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }
    }

    /// <summary>
    /// Loads a plugin from its manifest and entry point.
    /// </summary>
    public async Task<PluginLoadResult> LoadPluginAsync(
        PluginManifest manifest,
        IPluginInstance instance,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Validate manifest first
        var validator = new ManifestValidator();
        var validation = validator.Validate(manifest);

        if (!validation.IsValid)
        {
            return PluginLoadResult.Failed(
                manifest.Id,
                $"Invalid manifest: {string.Join(", ", validation.Errors)}");
        }

        lock (_lock)
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return PluginLoadResult.Failed(
                    manifest.Id,
                    $"Plugin '{manifest.Id}' is already loaded");
            }
        }

        try
        {
            // Create sandboxed context for this plugin
            var context = _sandbox.CreateContext(manifest);

            // Initialize the plugin within the sandbox
            await instance.InitializeAsync(context, ct).ConfigureAwait(false);

            var loadedPlugin = new LoadedPlugin(
                Manifest: manifest,
                Instance: instance,
                Context: context,
                LoadedAt: DateTimeOffset.UtcNow,
                State: PluginState.Enabled);

            lock (_lock)
            {
                _plugins[manifest.Id] = loadedPlugin;
            }

            _auditLog.LogPluginLoaded(manifest.Id, manifest.Version);
            PluginLoaded?.Invoke(this, new PluginLoadedEventArgs(loadedPlugin));

            return PluginLoadResult.Succeeded(manifest.Id);
        }
        catch (Exception ex)
        {
            _auditLog.LogPluginError(manifest.Id, "Load failed", ex.Message);
            return PluginLoadResult.Failed(manifest.Id, ex.Message);
        }
    }

    /// <summary>
    /// Unloads a plugin and releases its resources.
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        LoadedPlugin? plugin;
        lock (_lock)
        {
            if (!_plugins.TryGetValue(pluginId, out plugin))
            {
                return false;
            }

            _plugins.Remove(pluginId);
        }

        try
        {
            // Dispose plugin instance
            if (plugin.Instance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (plugin.Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Dispose sandbox context
            plugin.Context.Dispose();

            _auditLog.LogPluginUnloaded(pluginId);
            PluginUnloaded?.Invoke(this, new PluginUnloadedEventArgs(pluginId));

            return true;
        }
        catch (Exception ex)
        {
            _auditLog.LogPluginError(pluginId, "Unload failed", ex.Message);
            PluginError?.Invoke(this, new PluginErrorEventArgs(pluginId, "Unload failed", ex));
            return false;
        }
    }

    /// <summary>
    /// Enables a disabled plugin.
    /// </summary>
    public bool EnablePlugin(string pluginId)
    {
        lock (_lock)
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
                return false;

            if (plugin.State == PluginState.Enabled)
                return true;

            _plugins[pluginId] = plugin with { State = PluginState.Enabled };
            _auditLog.LogPluginEnabled(pluginId);
            return true;
        }
    }

    /// <summary>
    /// Disables a plugin without unloading it.
    /// </summary>
    public bool DisablePlugin(string pluginId)
    {
        lock (_lock)
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
                return false;

            if (plugin.State == PluginState.Disabled)
                return true;

            _plugins[pluginId] = plugin with { State = PluginState.Disabled };
            _auditLog.LogPluginDisabled(pluginId);
            return true;
        }
    }

    /// <summary>
    /// Executes an action within a plugin's sandbox.
    /// </summary>
    public async Task<PluginExecutionResult> ExecuteAsync(
        string pluginId,
        string actionId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        LoadedPlugin? plugin;
        lock (_lock)
        {
            if (!_plugins.TryGetValue(pluginId, out plugin))
            {
                return PluginExecutionResult.Failed(
                    pluginId,
                    actionId,
                    "Plugin not found");
            }
        }

        if (plugin.State != PluginState.Enabled)
        {
            return PluginExecutionResult.Failed(
                pluginId,
                actionId,
                "Plugin is disabled");
        }

        var executionId = Guid.NewGuid();
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _auditLog.LogActionStarted(pluginId, actionId, executionId);

            // Execute within sandbox - all resource access goes through context
            var result = await plugin.Instance.ExecuteAsync(
                actionId,
                parameters,
                plugin.Context,
                ct).ConfigureAwait(false);

            var duration = DateTimeOffset.UtcNow - startTime;

            _auditLog.LogActionCompleted(pluginId, actionId, executionId, result.Success, duration);

            return new PluginExecutionResult(
                PluginId: pluginId,
                ActionId: actionId,
                ExecutionId: executionId,
                Success: result.Success,
                Output: result.Output,
                Error: result.Error,
                Duration: duration);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;

            _auditLog.LogActionFailed(pluginId, actionId, executionId, ex.Message, duration);
            PluginError?.Invoke(this, new PluginErrorEventArgs(pluginId, actionId, ex));

            // Mark plugin as faulted if it throws
            lock (_lock)
            {
                if (_plugins.TryGetValue(pluginId, out var currentPlugin))
                {
                    _plugins[pluginId] = currentPlugin with { State = PluginState.Faulted };
                }
            }

            return PluginExecutionResult.Failed(
                pluginId,
                actionId,
                $"Execution failed: {ex.Message}",
                duration);
        }
    }

    /// <summary>
    /// Disables all plugins immediately.
    /// </summary>
    public void DisableAllPlugins()
    {
        lock (_lock)
        {
            foreach (var pluginId in _plugins.Keys.ToList())
            {
                var plugin = _plugins[pluginId];
                if (plugin.State == PluginState.Enabled)
                {
                    _plugins[pluginId] = plugin with { State = PluginState.Disabled };
                    _auditLog.LogPluginDisabled(pluginId);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose all plugins
        lock (_lock)
        {
            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    if (plugin.Instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    plugin.Context.Dispose();
                }
                catch (Exception)
                {
                    // Best-effort disposal — plugin may be faulted
                }
            }
            _plugins.Clear();
        }
    }

    /// <summary>
    /// Asynchronously disposes all loaded plugins.
    /// Preferred over <see cref="Dispose"/> when plugins implement <see cref="IAsyncDisposable"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        List<LoadedPlugin> plugins;
        lock (_lock)
        {
            plugins = [.. _plugins.Values];
            _plugins.Clear();
        }

        foreach (var plugin in plugins)
        {
            try
            {
                if (plugin.Instance is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (plugin.Instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                plugin.Context.Dispose();
            }
            catch (Exception)
            {
                // Best-effort disposal — plugin may be faulted
            }
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a loaded plugin with its runtime state.
/// </summary>
public sealed record LoadedPlugin(
    PluginManifest Manifest,
    IPluginInstance Instance,
    IPluginContext Context,
    DateTimeOffset LoadedAt,
    PluginState State
);

/// <summary>
/// Plugin runtime states.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// Plugin is active and can execute.
    /// </summary>
    Enabled,

    /// <summary>
    /// Plugin is loaded but not executing.
    /// </summary>
    Disabled,

    /// <summary>
    /// Plugin threw an unhandled exception.
    /// </summary>
    Faulted
}

/// <summary>
/// Result of loading a plugin.
/// </summary>
public sealed record PluginLoadResult(
    string PluginId,
    bool Success,
    string? Error
)
{
    public static PluginLoadResult Succeeded(string pluginId) =>
        new(pluginId, true, null);

    public static PluginLoadResult Failed(string pluginId, string error) =>
        new(pluginId, false, error);
}

/// <summary>
/// Result of executing a plugin action.
/// </summary>
public sealed record PluginExecutionResult(
    string PluginId,
    string ActionId,
    Guid ExecutionId,
    bool Success,
    object? Output,
    string? Error,
    TimeSpan Duration
)
{
    public static PluginExecutionResult Failed(
        string pluginId,
        string actionId,
        string error,
        TimeSpan? duration = null) =>
        new(pluginId, actionId, Guid.NewGuid(), false, null, error, duration ?? TimeSpan.Zero);
}

/// <summary>
/// Event args for plugin loaded.
/// </summary>
public sealed class PluginLoadedEventArgs : EventArgs
{
    public LoadedPlugin Plugin { get; }

    public PluginLoadedEventArgs(LoadedPlugin plugin)
    {
        Plugin = plugin;
    }
}

/// <summary>
/// Event args for plugin unloaded.
/// </summary>
public sealed class PluginUnloadedEventArgs : EventArgs
{
    public string PluginId { get; }

    public PluginUnloadedEventArgs(string pluginId)
    {
        PluginId = pluginId;
    }
}

/// <summary>
/// Event args for plugin errors.
/// </summary>
public sealed class PluginErrorEventArgs : EventArgs
{
    public string PluginId { get; }
    public string Action { get; }
    public Exception Exception { get; }

    public PluginErrorEventArgs(string pluginId, string action, Exception exception)
    {
        PluginId = pluginId;
        Action = action;
        Exception = exception;
    }
}
