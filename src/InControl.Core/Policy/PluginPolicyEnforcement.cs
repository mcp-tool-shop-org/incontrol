using InControl.Core.Plugins;

namespace InControl.Core.Policy;

/// <summary>
/// Wraps a PluginHost with policy enforcement.
/// All plugin loading and execution is evaluated against the active policy.
/// </summary>
public sealed class PolicyGovernedPluginHost
{
    private readonly PluginHost _innerHost;
    private readonly PolicyEngine _policyEngine;
    private readonly object _lock = new();
    private readonly Dictionary<string, (DateTimeOffset ApprovedAt, string ApprovedBy)> _approvedPlugins = [];

    /// <summary>
    /// Event raised when a plugin is blocked by policy.
    /// </summary>
    public event EventHandler<PluginBlockedEventArgs>? PluginBlocked;

    /// <summary>
    /// Event raised when a plugin requires approval.
    /// </summary>
    public event EventHandler<PluginApprovalRequiredEventArgs>? ApprovalRequired;

    public PolicyGovernedPluginHost(PluginHost innerHost, PolicyEngine policyEngine)
    {
        _innerHost = innerHost ?? throw new ArgumentNullException(nameof(innerHost));
        _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
    }

    /// <summary>
    /// Gets the underlying plugin host.
    /// </summary>
    public PluginHost InnerHost => _innerHost;

    /// <summary>
    /// Gets the policy engine.
    /// </summary>
    public PolicyEngine PolicyEngine => _policyEngine;

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    public IReadOnlyList<LoadedPlugin> LoadedPlugins => _innerHost.LoadedPlugins;

    /// <summary>
    /// Checks if a plugin can be loaded under current policy.
    /// </summary>
    public PluginPolicyCheck CheckPluginPolicy(
        string pluginId,
        string? author = null,
        PluginRiskLevel? riskLevel = null)
    {
        var policyResult = _policyEngine.EvaluatePlugin(pluginId, author, riskLevel);

        // Check for pre-approval
        lock (_lock)
        {
            if (_approvedPlugins.TryGetValue(pluginId, out var approval))
            {
                if (policyResult.Decision == PolicyDecision.AllowWithApproval)
                {
                    return new PluginPolicyCheck(
                        CanLoad: true,
                        Decision: PolicyDecision.Allow,
                        Reason: $"Pre-approved at {approval.ApprovedAt:g} by {approval.ApprovedBy}",
                        Source: PolicySource.Session,
                        RequiresApproval: false);
                }
            }
        }

        return policyResult.Decision switch
        {
            PolicyDecision.Allow => new PluginPolicyCheck(
                CanLoad: true,
                Decision: policyResult.Decision,
                Reason: policyResult.Reason,
                Source: policyResult.Source,
                RequiresApproval: false),

            PolicyDecision.AllowWithApproval => new PluginPolicyCheck(
                CanLoad: false,
                Decision: policyResult.Decision,
                Reason: policyResult.Reason,
                Source: policyResult.Source,
                RequiresApproval: true),

            PolicyDecision.Deny => new PluginPolicyCheck(
                CanLoad: false,
                Decision: policyResult.Decision,
                Reason: policyResult.Reason,
                Source: policyResult.Source,
                RequiresApproval: false,
                RuleId: policyResult.RuleId),

            _ => new PluginPolicyCheck(
                CanLoad: false,
                Decision: PolicyDecision.Deny,
                Reason: "Unknown policy decision",
                Source: policyResult.Source,
                RequiresApproval: false)
        };
    }

    /// <summary>
    /// Checks if a plugin manifest can be loaded under current policy.
    /// </summary>
    public PluginPolicyCheck CheckManifestPolicy(PluginManifest manifest)
    {
        return CheckPluginPolicy(manifest.Id, manifest.Author, manifest.RiskLevel);
    }

    /// <summary>
    /// Grants approval for a plugin.
    /// </summary>
    public void ApprovePlugin(string pluginId, string approvedBy = "operator")
    {
        lock (_lock)
        {
            _approvedPlugins[pluginId] = (DateTimeOffset.UtcNow, approvedBy);
        }
    }

    /// <summary>
    /// Revokes approval for a plugin.
    /// </summary>
    public void RevokeApproval(string pluginId)
    {
        lock (_lock)
        {
            _approvedPlugins.Remove(pluginId);
        }
    }

    /// <summary>
    /// Gets all approved plugins.
    /// </summary>
    public IReadOnlyList<(string PluginId, DateTimeOffset ApprovedAt, string ApprovedBy)> GetApprovedPlugins()
    {
        lock (_lock)
        {
            return _approvedPlugins
                .Select(kvp => (kvp.Key, kvp.Value.ApprovedAt, kvp.Value.ApprovedBy))
                .ToList();
        }
    }

    /// <summary>
    /// Clears all approvals.
    /// </summary>
    public void ClearApprovals()
    {
        lock (_lock)
        {
            _approvedPlugins.Clear();
        }
    }

    /// <summary>
    /// Loads a plugin with policy enforcement.
    /// </summary>
    public async Task<PolicyGovernedPluginLoadResult> LoadPluginAsync(
        PluginManifest manifest,
        IPluginInstance instance,
        CancellationToken ct = default)
    {
        var policyCheck = CheckManifestPolicy(manifest);

        // If plugin requires approval but hasn't been granted
        if (policyCheck.RequiresApproval)
        {
            ApprovalRequired?.Invoke(this, new PluginApprovalRequiredEventArgs(
                manifest.Id,
                manifest.Name,
                manifest.Author,
                manifest.RiskLevel,
                policyCheck.Reason));

            return PolicyGovernedPluginLoadResult.RequiresApproval(manifest.Id, policyCheck.Reason);
        }

        // If plugin is blocked by policy
        if (!policyCheck.CanLoad)
        {
            PluginBlocked?.Invoke(this, new PluginBlockedEventArgs(
                manifest.Id,
                manifest.Name,
                policyCheck.Reason,
                policyCheck.Source));

            return PolicyGovernedPluginLoadResult.Blocked(manifest.Id, policyCheck.Reason, policyCheck.Source);
        }

        // Load the plugin
        var result = await _innerHost.LoadPluginAsync(manifest, instance, ct);

        if (result.Success)
        {
            return PolicyGovernedPluginLoadResult.Loaded(manifest.Id);
        }
        else
        {
            return PolicyGovernedPluginLoadResult.Failed(manifest.Id, result.Error ?? "Unknown error");
        }
    }

    /// <summary>
    /// Unloads a plugin.
    /// </summary>
    public Task<bool> UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        return _innerHost.UnloadPluginAsync(pluginId, ct);
    }

    /// <summary>
    /// Executes a plugin action with policy enforcement.
    /// </summary>
    public async Task<PolicyGovernedPluginExecutionResult> ExecuteAsync(
        string pluginId,
        string actionId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        // Get the loaded plugin to check its manifest
        var loadedPlugin = _innerHost.GetPlugin(pluginId);
        if (loadedPlugin == null)
        {
            return PolicyGovernedPluginExecutionResult.Failed(pluginId, actionId, "Plugin not loaded");
        }

        // Re-check policy (in case policy changed since load)
        var policyCheck = CheckManifestPolicy(loadedPlugin.Manifest);

        if (!policyCheck.CanLoad)
        {
            PluginBlocked?.Invoke(this, new PluginBlockedEventArgs(
                pluginId,
                loadedPlugin.Manifest.Name,
                policyCheck.Reason,
                policyCheck.Source));

            return PolicyGovernedPluginExecutionResult.Blocked(pluginId, actionId, policyCheck.Reason, policyCheck.Source);
        }

        // Execute the action
        var result = await _innerHost.ExecuteAsync(pluginId, actionId, parameters, ct);

        return new PolicyGovernedPluginExecutionResult(
            PluginId: pluginId,
            ActionId: actionId,
            Success: result.Success,
            WasBlocked: false,
            Output: result.Output,
            Error: result.Error,
            Duration: result.Duration);
    }

    /// <summary>
    /// Gets plugins that can be loaded under current policy.
    /// </summary>
    public IReadOnlyList<PluginWithPolicyInfo> GetLoadablePlugins(IEnumerable<PluginManifest> manifests)
    {
        var result = new List<PluginWithPolicyInfo>();

        foreach (var manifest in manifests)
        {
            var policyCheck = CheckManifestPolicy(manifest);

            // Include if not permanently denied
            if (policyCheck.Decision != PolicyDecision.Deny)
            {
                result.Add(new PluginWithPolicyInfo(manifest, policyCheck));
            }
        }

        return result;
    }

    /// <summary>
    /// Gets policy status for all provided manifests.
    /// </summary>
    public IReadOnlyList<PluginWithPolicyInfo> GetAllPluginsWithPolicy(IEnumerable<PluginManifest> manifests)
    {
        return manifests
            .Select(m => new PluginWithPolicyInfo(m, CheckManifestPolicy(m)))
            .ToList();
    }

    /// <summary>
    /// Gets policy status for all loaded plugins.
    /// </summary>
    public IReadOnlyList<LoadedPluginWithPolicyInfo> GetLoadedPluginsWithPolicy()
    {
        return _innerHost.LoadedPlugins
            .Select(lp => new LoadedPluginWithPolicyInfo(lp, CheckManifestPolicy(lp.Manifest)))
            .ToList();
    }

    /// <summary>
    /// Enables a plugin if policy allows.
    /// </summary>
    public bool EnablePlugin(string pluginId)
    {
        var loadedPlugin = _innerHost.GetPlugin(pluginId);
        if (loadedPlugin == null) return false;

        var policyCheck = CheckManifestPolicy(loadedPlugin.Manifest);
        if (!policyCheck.CanLoad)
        {
            PluginBlocked?.Invoke(this, new PluginBlockedEventArgs(
                pluginId,
                loadedPlugin.Manifest.Name,
                policyCheck.Reason,
                policyCheck.Source));
            return false;
        }

        return _innerHost.EnablePlugin(pluginId);
    }

    /// <summary>
    /// Disables a plugin.
    /// </summary>
    public bool DisablePlugin(string pluginId)
    {
        return _innerHost.DisablePlugin(pluginId);
    }
}

/// <summary>
/// Result of policy check for a plugin.
/// </summary>
public sealed record PluginPolicyCheck(
    bool CanLoad,
    PolicyDecision Decision,
    string Reason,
    PolicySource Source,
    bool RequiresApproval,
    string? RuleId = null);

/// <summary>
/// Result of policy-governed plugin loading.
/// </summary>
public sealed record PolicyGovernedPluginLoadResult
{
    public string PluginId { get; init; } = "";
    public bool Success { get; init; }
    public bool WasBlocked { get; init; }
    public bool RequiredApproval { get; init; }
    public string? BlockReason { get; init; }
    public PolicySource? BlockSource { get; init; }
    public string? Error { get; init; }

    public static PolicyGovernedPluginLoadResult Loaded(string pluginId) => new()
    {
        PluginId = pluginId,
        Success = true
    };

    public static PolicyGovernedPluginLoadResult Blocked(string pluginId, string reason, PolicySource source) => new()
    {
        PluginId = pluginId,
        Success = false,
        WasBlocked = true,
        BlockReason = reason,
        BlockSource = source
    };

    public static PolicyGovernedPluginLoadResult RequiresApproval(string pluginId, string reason) => new()
    {
        PluginId = pluginId,
        Success = false,
        RequiredApproval = true,
        BlockReason = reason
    };

    public static PolicyGovernedPluginLoadResult Failed(string pluginId, string error) => new()
    {
        PluginId = pluginId,
        Success = false,
        Error = error
    };
}

/// <summary>
/// Result of policy-governed plugin execution.
/// </summary>
public sealed record PolicyGovernedPluginExecutionResult(
    string PluginId,
    string ActionId,
    bool Success,
    bool WasBlocked,
    object? Output,
    string? Error,
    TimeSpan Duration)
{
    public PolicySource? BlockSource { get; init; }

    public static PolicyGovernedPluginExecutionResult Blocked(
        string pluginId,
        string actionId,
        string reason,
        PolicySource source) => new(
            pluginId, actionId, false, true, null, reason, TimeSpan.Zero)
    { BlockSource = source };

    public static PolicyGovernedPluginExecutionResult Failed(
        string pluginId,
        string actionId,
        string error) => new(
            pluginId, actionId, false, false, null, error, TimeSpan.Zero);
}

/// <summary>
/// A plugin manifest with its current policy information.
/// </summary>
public sealed record PluginWithPolicyInfo(PluginManifest Manifest, PluginPolicyCheck PolicyStatus);

/// <summary>
/// A loaded plugin with its current policy information.
/// </summary>
public sealed record LoadedPluginWithPolicyInfo(LoadedPlugin Plugin, PluginPolicyCheck PolicyStatus);

/// <summary>
/// Event args for when a plugin is blocked by policy.
/// </summary>
public sealed class PluginBlockedEventArgs : EventArgs
{
    public string PluginId { get; }
    public string PluginName { get; }
    public string Reason { get; }
    public PolicySource Source { get; }

    public PluginBlockedEventArgs(string pluginId, string pluginName, string reason, PolicySource source)
    {
        PluginId = pluginId;
        PluginName = pluginName;
        Reason = reason;
        Source = source;
    }
}

/// <summary>
/// Event args for when a plugin requires approval.
/// </summary>
public sealed class PluginApprovalRequiredEventArgs : EventArgs
{
    public string PluginId { get; }
    public string PluginName { get; }
    public string Author { get; }
    public PluginRiskLevel RiskLevel { get; }
    public string Reason { get; }

    public PluginApprovalRequiredEventArgs(
        string pluginId,
        string pluginName,
        string author,
        PluginRiskLevel riskLevel,
        string reason)
    {
        PluginId = pluginId;
        PluginName = pluginName;
        Author = author;
        RiskLevel = riskLevel;
        Reason = reason;
    }
}

/// <summary>
/// Extensions for easy policy integration with plugin host.
/// </summary>
public static class PluginPolicyExtensions
{
    /// <summary>
    /// Creates a policy-governed wrapper around this plugin host.
    /// </summary>
    public static PolicyGovernedPluginHost WithPolicyEnforcement(
        this PluginHost host,
        PolicyEngine policyEngine)
    {
        return new PolicyGovernedPluginHost(host, policyEngine);
    }
}
