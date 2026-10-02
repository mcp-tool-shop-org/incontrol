using System.Text.Json;

namespace InControl.Core.Plugins;

/// <summary>
/// Manages permission policies for plugins.
/// Operator consent is required for any permission elevation.
/// </summary>
public sealed class PluginPermissionPolicy
{
    private readonly Dictionary<string, PluginPolicyRecord> _policies = new();
    private readonly string _policyPath;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when a permission decision is required.
    /// </summary>
    public event EventHandler<PermissionRequestEventArgs>? PermissionRequested;

    /// <summary>
    /// Event raised when a policy is updated.
    /// </summary>
    public event EventHandler<PolicyUpdatedEventArgs>? PolicyUpdated;

    public PluginPermissionPolicy(string policyPath)
    {
        _policyPath = policyPath;
        LoadPolicies();
    }

    /// <summary>
    /// Gets the policy for a plugin.
    /// </summary>
    public PluginPolicyRecord? GetPolicy(string pluginId)
    {
        lock (_lock)
        {
            return _policies.TryGetValue(pluginId, out var policy) ? policy : null;
        }
    }

    /// <summary>
    /// Checks if a permission is granted for a plugin.
    /// </summary>
    public PermissionCheckResult CheckPermission(
        string pluginId,
        PermissionType type,
        PermissionAccess access,
        string? scope = null)
    {
        lock (_lock)
        {
            if (!_policies.TryGetValue(pluginId, out var policy))
            {
                return PermissionCheckResult.NotConfigured;
            }

            // Check if plugin is trusted
            if (policy.TrustLevel == PluginTrustLevel.Blocked)
            {
                return PermissionCheckResult.Denied("Plugin is blocked");
            }

            // Find matching permission rule
            var rule = policy.PermissionRules.FirstOrDefault(r =>
                r.Type == type &&
                r.Access >= access &&
                (scope == null || r.Scope == null || MatchesScope(scope, r.Scope)));

            if (rule == null)
            {
                return PermissionCheckResult.NotConfigured;
            }

            return rule.Decision switch
            {
                PermissionDecision.Allow => PermissionCheckResult.Allowed,
                PermissionDecision.Deny => PermissionCheckResult.Denied("Permission denied by policy"),
                PermissionDecision.AskOnce => PermissionCheckResult.RequiresConsent,
                PermissionDecision.AskAlways => PermissionCheckResult.RequiresConsent,
                _ => PermissionCheckResult.NotConfigured
            };
        }
    }

    /// <summary>
    /// Requests permission from the operator.
    /// This is an async operation that waits for operator consent.
    /// </summary>
    public async Task<PermissionRequestResult> RequestPermissionAsync(
        string pluginId,
        PermissionType type,
        PermissionAccess access,
        string? scope,
        string reason,
        CancellationToken ct = default)
    {
        var request = new PermissionRequest(
            PluginId: pluginId,
            Type: type,
            Access: access,
            Scope: scope,
            Reason: reason,
            RequestedAt: DateTimeOffset.UtcNow);

        // Raise event for UI to handle
        var args = new PermissionRequestEventArgs(request);
        PermissionRequested?.Invoke(this, args);

        // Wait for the decision (set by the UI)
        try
        {
            var result = await args.GetResultAsync(ct);

            // Record the decision if it should be remembered
            if (result.RememberDecision)
            {
                RecordPermissionDecision(pluginId, type, access, scope, result.Granted);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return new PermissionRequestResult(false, false, "Request cancelled");
        }
    }

    /// <summary>
    /// Sets the trust level for a plugin.
    /// </summary>
    public void SetTrustLevel(string pluginId, PluginTrustLevel trustLevel)
    {
        lock (_lock)
        {
            var policy = _policies.GetValueOrDefault(pluginId) ?? new PluginPolicyRecord(pluginId);
            _policies[pluginId] = policy with { TrustLevel = trustLevel, LastModified = DateTimeOffset.UtcNow };
            SavePolicies();
        }

        PolicyUpdated?.Invoke(this, new PolicyUpdatedEventArgs(pluginId, PolicyUpdateType.TrustLevel));
    }

    /// <summary>
    /// Adds a permission rule for a plugin.
    /// </summary>
    public void AddPermissionRule(string pluginId, PermissionRule rule)
    {
        lock (_lock)
        {
            var policy = _policies.GetValueOrDefault(pluginId) ?? new PluginPolicyRecord(pluginId);
            var rules = policy.PermissionRules.ToList();

            // Remove existing rule for same type/access/scope
            rules.RemoveAll(r => r.Type == rule.Type && r.Access == rule.Access && r.Scope == rule.Scope);
            rules.Add(rule);

            _policies[pluginId] = policy with
            {
                PermissionRules = rules,
                LastModified = DateTimeOffset.UtcNow
            };
            SavePolicies();
        }

        PolicyUpdated?.Invoke(this, new PolicyUpdatedEventArgs(pluginId, PolicyUpdateType.PermissionRule));
    }

    /// <summary>
    /// Removes a permission rule for a plugin.
    /// </summary>
    public void RemovePermissionRule(string pluginId, PermissionType type, PermissionAccess access, string? scope)
    {
        lock (_lock)
        {
            if (!_policies.TryGetValue(pluginId, out var policy))
                return;

            var rules = policy.PermissionRules.ToList();
            rules.RemoveAll(r => r.Type == type && r.Access == access && r.Scope == scope);

            _policies[pluginId] = policy with
            {
                PermissionRules = rules,
                LastModified = DateTimeOffset.UtcNow
            };
            SavePolicies();
        }

        PolicyUpdated?.Invoke(this, new PolicyUpdatedEventArgs(pluginId, PolicyUpdateType.PermissionRule));
    }

    /// <summary>
    /// Clears all policies for a plugin.
    /// </summary>
    public void ClearPolicy(string pluginId)
    {
        lock (_lock)
        {
            _policies.Remove(pluginId);
            SavePolicies();
        }

        PolicyUpdated?.Invoke(this, new PolicyUpdatedEventArgs(pluginId, PolicyUpdateType.Cleared));
    }

    /// <summary>
    /// Gets all plugin policies.
    /// </summary>
    public IReadOnlyList<PluginPolicyRecord> GetAllPolicies()
    {
        lock (_lock)
        {
            return _policies.Values.ToList();
        }
    }

    /// <summary>
    /// Records a permission decision from operator consent.
    /// </summary>
    private void RecordPermissionDecision(
        string pluginId,
        PermissionType type,
        PermissionAccess access,
        string? scope,
        bool granted)
    {
        var rule = new PermissionRule(
            Type: type,
            Access: access,
            Scope: scope,
            Decision: granted ? PermissionDecision.Allow : PermissionDecision.Deny);

        AddPermissionRule(pluginId, rule);
    }

    private static bool MatchesScope(string requested, string permitted)
    {
        return requested.StartsWith(permitted, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadPolicies()
    {
        lock (_lock)
        {
            _policies.Clear();

            if (!File.Exists(_policyPath))
                return;

            try
            {
                var json = File.ReadAllText(_policyPath);
                var records = JsonSerializer.Deserialize<List<PluginPolicyRecord>>(json);
                if (records != null)
                {
                    foreach (var record in records)
                    {
                        _policies[record.PluginId] = record;
                    }
                }
            }
            catch
            {
                // Policy file corrupted - start fresh
                _policies.Clear();
            }
        }
    }

    private void SavePolicies()
    {
        try
        {
            var directory = Path.GetDirectoryName(_policyPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var records = _policies.Values.ToList();
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_policyPath, json);
        }
        catch
        {
            // Best effort - don't crash if we can't save
        }
    }
}

/// <summary>
/// Policy record for a plugin.
/// </summary>
public sealed record PluginPolicyRecord(
    string PluginId,
    PluginTrustLevel TrustLevel = PluginTrustLevel.Default,
    IReadOnlyList<PermissionRule>? PermissionRules = null,
    DateTimeOffset? LastModified = null)
{
    public IReadOnlyList<PermissionRule> PermissionRules { get; init; } = PermissionRules ?? [];
}

/// <summary>
/// Trust level for a plugin.
/// </summary>
public enum PluginTrustLevel
{
    /// <summary>
    /// Default - permissions checked as declared.
    /// </summary>
    Default,

    /// <summary>
    /// Trusted - allow all declared permissions.
    /// </summary>
    Trusted,

    /// <summary>
    /// Blocked - deny all operations.
    /// </summary>
    Blocked
}

/// <summary>
/// A permission rule in a policy.
/// </summary>
public sealed record PermissionRule(
    PermissionType Type,
    PermissionAccess Access,
    string? Scope,
    PermissionDecision Decision);

/// <summary>
/// Decision for a permission.
/// </summary>
public enum PermissionDecision
{
    /// <summary>
    /// Allow the permission.
    /// </summary>
    Allow,

    /// <summary>
    /// Deny the permission.
    /// </summary>
    Deny,

    /// <summary>
    /// Ask the operator once, then remember.
    /// </summary>
    AskOnce,

    /// <summary>
    /// Always ask the operator.
    /// </summary>
    AskAlways
}

/// <summary>
/// Result of checking a permission.
/// </summary>
public sealed record PermissionCheckResult(
    bool IsAllowed,
    bool RequiresOperatorConsent,
    bool IsConfigured,
    string? DenialReason)
{
    public static PermissionCheckResult Allowed { get; } = new(true, false, true, null);
    public static PermissionCheckResult RequiresConsent { get; } = new(false, true, true, null);
    public static PermissionCheckResult NotConfigured { get; } = new(false, false, false, null);
    public static PermissionCheckResult Denied(string reason) => new(false, false, true, reason);
}

/// <summary>
/// A permission request for operator consent.
/// </summary>
public sealed record PermissionRequest(
    string PluginId,
    PermissionType Type,
    PermissionAccess Access,
    string? Scope,
    string Reason,
    DateTimeOffset RequestedAt);

/// <summary>
/// Result of a permission request.
/// </summary>
public sealed record PermissionRequestResult(
    bool Granted,
    bool RememberDecision,
    string? Reason = null);

/// <summary>
/// Event args for permission request.
/// </summary>
public sealed class PermissionRequestEventArgs : EventArgs
{
    private readonly TaskCompletionSource<PermissionRequestResult> _tcs = new();

    public PermissionRequest Request { get; }

    public PermissionRequestEventArgs(PermissionRequest request)
    {
        Request = request;
    }

    /// <summary>
    /// Sets the operator's decision.
    /// </summary>
    public void SetResult(PermissionRequestResult result)
    {
        _tcs.TrySetResult(result);
    }

    /// <summary>
    /// Waits for the operator's decision.
    /// </summary>
    internal Task<PermissionRequestResult> GetResultAsync(CancellationToken ct)
    {
        ct.Register(() => _tcs.TrySetCanceled());
        return _tcs.Task;
    }
}

/// <summary>
/// Event args for policy updates.
/// </summary>
public sealed class PolicyUpdatedEventArgs : EventArgs
{
    public string PluginId { get; }
    public PolicyUpdateType UpdateType { get; }

    public PolicyUpdatedEventArgs(string pluginId, PolicyUpdateType updateType)
    {
        PluginId = pluginId;
        UpdateType = updateType;
    }
}

/// <summary>
/// Type of policy update.
/// </summary>
public enum PolicyUpdateType
{
    TrustLevel,
    PermissionRule,
    Cleared
}
