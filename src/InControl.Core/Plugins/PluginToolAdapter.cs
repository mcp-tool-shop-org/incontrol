using InControl.Core.Assistant;
using InControl.Core.Errors;

namespace InControl.Core.Plugins;

// Note: Uses ToolRegistry class from InControl.Core.Assistant

/// <summary>
/// Adapts plugin capabilities to the AssistantTool interface.
/// Plugins become tools, not special cases.
/// </summary>
public sealed class PluginToolAdapter : IAssistantTool
{
    private readonly PluginHost _host;
    private readonly string _pluginId;
    private readonly PluginCapability _capability;
    private readonly PluginManifest _manifest;

    public PluginToolAdapter(
        PluginHost host,
        string pluginId,
        PluginCapability capability,
        PluginManifest manifest)
    {
        _host = host;
        _pluginId = pluginId;
        _capability = capability;
        _manifest = manifest;
    }

    /// <summary>
    /// Tool ID combines plugin ID and capability ID for uniqueness.
    /// </summary>
    public string Id => $"plugin:{_pluginId}:{_capability.ToolId}";

    /// <summary>
    /// Human-readable name from capability.
    /// </summary>
    public string Name => _capability.Name;

    /// <summary>
    /// Description from capability.
    /// </summary>
    public string Description => _capability.Description;

    /// <summary>
    /// Risk level based on plugin's declared risk.
    /// </summary>
    public ToolRiskLevel RiskLevel => _manifest.RiskLevel switch
    {
        PluginRiskLevel.ReadOnly => ToolRiskLevel.Low,
        PluginRiskLevel.LocalMutation => ToolRiskLevel.Medium,
        PluginRiskLevel.Network => ToolRiskLevel.High,
        PluginRiskLevel.SystemAdjacent => ToolRiskLevel.Critical,
        _ => ToolRiskLevel.Medium
    };

    /// <summary>
    /// Whether this tool requires network, from capability declaration.
    /// </summary>
    public bool RequiresNetwork => _capability.RequiresNetwork;

    /// <summary>
    /// Whether this tool is read-only (does not modify state).
    /// </summary>
    public bool IsReadOnly => !_capability.ModifiesState;

    /// <summary>
    /// Parameters are dynamic for plugins - uses a generic input.
    /// </summary>
    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("input", "Input data for the plugin tool", ParameterType.String, false)
    };

    /// <summary>
    /// Executes the plugin capability through the sandbox.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var result = await _host.ExecuteAsync(
                _pluginId,
                _capability.ToolId,
                context.Parameters,
                ct);

            var duration = DateTimeOffset.UtcNow - startTime;

            if (result.Success)
            {
                return new ToolResult
                {
                    Success = true,
                    Output = result.Output?.ToString(),
                    CompletedAt = DateTimeOffset.UtcNow,
                    Duration = duration
                };
            }
            else
            {
                return ToolResult.Failed(new InControlError
                {
                    Code = ErrorCode.ToolExecutionFailed,
                    Message = result.Error ?? "Plugin execution failed"
                }, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            return ToolResult.Failed(new InControlError
            {
                Code = ErrorCode.ToolExecutionFailed,
                Message = ex.Message
            }, duration);
        }
    }

    /// <summary>
    /// Gets the source plugin information for display.
    /// </summary>
    public PluginToolInfo GetPluginInfo() => new(
        PluginId: _pluginId,
        PluginName: _manifest.Name,
        PluginAuthor: _manifest.Author,
        PluginVersion: _manifest.Version,
        CapabilityId: _capability.ToolId
    );
}

/// <summary>
/// Information about the source plugin for a tool.
/// </summary>
public sealed record PluginToolInfo(
    string PluginId,
    string PluginName,
    string PluginAuthor,
    string PluginVersion,
    string CapabilityId
);

/// <summary>
/// Manages registration of plugin tools with the tool registry.
/// </summary>
public sealed class PluginToolRegistry
{
    private readonly PluginHost _host;
    private readonly ToolRegistry _toolRegistry;
    private readonly Dictionary<string, List<string>> _pluginTools = new();
    private readonly object _lock = new();

    public PluginToolRegistry(PluginHost host, ToolRegistry toolRegistry)
    {
        _host = host;
        _toolRegistry = toolRegistry;

        // Subscribe to plugin lifecycle events
        _host.PluginLoaded += OnPluginLoaded;
        _host.PluginUnloaded += OnPluginUnloaded;
    }

    /// <summary>
    /// Gets all registered plugin tools.
    /// </summary>
    public IReadOnlyList<PluginToolAdapter> GetPluginTools()
    {
        lock (_lock)
        {
            var tools = new List<PluginToolAdapter>();
            foreach (var plugin in _host.LoadedPlugins)
            {
                if (plugin.State != PluginState.Enabled)
                    continue;

                foreach (var capability in plugin.Manifest.Capabilities)
                {
                    var adapter = new PluginToolAdapter(
                        _host,
                        plugin.Manifest.Id,
                        capability,
                        plugin.Manifest);
                    tools.Add(adapter);
                }
            }
            return tools;
        }
    }

    /// <summary>
    /// Gets plugin tools for a specific plugin.
    /// </summary>
    public IReadOnlyList<PluginToolAdapter> GetToolsForPlugin(string pluginId)
    {
        var plugin = _host.GetPlugin(pluginId);
        if (plugin == null)
            return [];

        return plugin.Manifest.Capabilities
            .Select(c => new PluginToolAdapter(_host, pluginId, c, plugin.Manifest))
            .ToList();
    }

    /// <summary>
    /// Registers all tools from a plugin.
    /// </summary>
    public void RegisterPluginTools(string pluginId)
    {
        var plugin = _host.GetPlugin(pluginId);
        if (plugin == null)
            return;

        var toolIds = new List<string>();

        foreach (var capability in plugin.Manifest.Capabilities)
        {
            var adapter = new PluginToolAdapter(
                _host,
                pluginId,
                capability,
                plugin.Manifest);

            _toolRegistry.Register(adapter);
            toolIds.Add(adapter.Id);
        }

        lock (_lock)
        {
            _pluginTools[pluginId] = toolIds;
        }
    }

    /// <summary>
    /// Unregisters all tools from a plugin.
    /// </summary>
    public void UnregisterPluginTools(string pluginId)
    {
        List<string>? toolIds;
        lock (_lock)
        {
            if (!_pluginTools.TryGetValue(pluginId, out toolIds))
                return;

            _pluginTools.Remove(pluginId);
        }

        foreach (var toolId in toolIds)
        {
            _toolRegistry.Unregister(toolId);
        }
    }

    private void OnPluginLoaded(object? sender, PluginLoadedEventArgs e)
    {
        RegisterPluginTools(e.Plugin.Manifest.Id);
    }

    private void OnPluginUnloaded(object? sender, PluginUnloadedEventArgs e)
    {
        UnregisterPluginTools(e.PluginId);
    }
}

/// <summary>
/// Extension to check if a tool is from a plugin.
/// </summary>
public static class PluginToolExtensions
{
    /// <summary>
    /// Checks if a tool ID indicates a plugin tool.
    /// </summary>
    public static bool IsPluginTool(this string toolId) =>
        toolId.StartsWith("plugin:", StringComparison.Ordinal);

    /// <summary>
    /// Extracts plugin ID from a plugin tool ID.
    /// </summary>
    public static string? GetPluginIdFromToolId(this string toolId)
    {
        if (!toolId.IsPluginTool())
            return null;

        var parts = toolId.Split(':');
        return parts.Length >= 2 ? parts[1] : null;
    }

    /// <summary>
    /// Extracts capability ID from a plugin tool ID.
    /// </summary>
    public static string? GetCapabilityIdFromToolId(this string toolId)
    {
        if (!toolId.IsPluginTool())
            return null;

        var parts = toolId.Split(':');
        return parts.Length >= 3 ? parts[2] : null;
    }
}
