using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace InControl.Core.Policy;

/// <summary>
/// The policy engine: loads, merges, and evaluates policies deterministically.
///
/// Evaluation Order (highest precedence first):
/// 1. Organization policy (locked rules cannot be overridden)
/// 2. Team policy
/// 3. User policy
/// 4. Session policy
/// 5. Default behavior
///
/// Every evaluation produces an explainable result.
/// </summary>
public sealed class PolicyEngine
{
    private readonly object _lock = new();
    private PolicyDocument? _orgPolicy;
    private PolicyDocument? _teamPolicy;
    private PolicyDocument? _userPolicy;
    private PolicyDocument? _sessionPolicy;
    private readonly List<PolicyAuditEntry> _auditLog = [];
    private static readonly ConcurrentDictionary<string, Regex> _patternCache = new();
    private DateTimeOffset _loadedAt;

    /// <summary>
    /// Event raised when a policy evaluation is logged.
    /// </summary>
    public event EventHandler<PolicyAuditEntry>? PolicyEvaluated;

    /// <summary>
    /// Gets information about the effective policy state.
    /// </summary>
    public EffectivePolicyInfo GetEffectivePolicy()
    {
        lock (_lock)
        {
            return new EffectivePolicyInfo
            {
                HasOrgPolicy = _orgPolicy != null,
                HasTeamPolicy = _teamPolicy != null,
                HasUserPolicy = _userPolicy != null,
                IsLocked = _orgPolicy?.Locked ?? false,
                OrgPolicyPath = _orgPolicy != null ? PolicyPaths.GetOrgPolicyPath() : null,
                TeamPolicyPath = _teamPolicy != null ? PolicyPaths.GetTeamPolicyPath() : null,
                UserPolicyPath = _userPolicy != null ? PolicyPaths.GetUserPolicyPath() : null,
                LoadedAt = _loadedAt
            };
        }
    }

    /// <summary>
    /// Loads policies from standard paths.
    /// </summary>
    public async Task<PolicyLoadSummary> LoadPoliciesAsync(CancellationToken ct = default)
    {
        var errors = new List<string>();
        var loaded = new List<(PolicySource Source, string Path)>();

        // Load organization policy
        var orgResult = await PolicySerializer.LoadFromFileAsync(PolicyPaths.GetOrgPolicyPath(), ct).ConfigureAwait(false);
        if (orgResult.IsSuccess)
        {
            lock (_lock) { _orgPolicy = orgResult.Document; }
            loaded.Add((PolicySource.Organization, orgResult.SourcePath!));
        }
        else if (!orgResult.FileNotFound)
        {
            errors.Add($"Organization policy: {orgResult.Error}");
        }

        // Load team policy
        var teamResult = await PolicySerializer.LoadFromFileAsync(PolicyPaths.GetTeamPolicyPath(), ct).ConfigureAwait(false);
        if (teamResult.IsSuccess)
        {
            lock (_lock) { _teamPolicy = teamResult.Document; }
            loaded.Add((PolicySource.Team, teamResult.SourcePath!));
        }
        else if (!teamResult.FileNotFound)
        {
            errors.Add($"Team policy: {teamResult.Error}");
        }

        // Load user policy
        var userResult = await PolicySerializer.LoadFromFileAsync(PolicyPaths.GetUserPolicyPath(), ct).ConfigureAwait(false);
        if (userResult.IsSuccess)
        {
            lock (_lock) { _userPolicy = userResult.Document; }
            loaded.Add((PolicySource.User, userResult.SourcePath!));
        }
        else if (!userResult.FileNotFound)
        {
            errors.Add($"User policy: {userResult.Error}");
        }

        lock (_lock)
        {
            _loadedAt = DateTimeOffset.UtcNow;
        }

        return new PolicyLoadSummary(loaded, errors);
    }

    /// <summary>
    /// Sets a policy directly (useful for testing and session policies).
    /// </summary>
    public void SetPolicy(PolicySource source, PolicyDocument? document)
    {
        lock (_lock)
        {
            switch (source)
            {
                case PolicySource.Organization:
                    _orgPolicy = document;
                    break;
                case PolicySource.Team:
                    _teamPolicy = document;
                    break;
                case PolicySource.User:
                    _userPolicy = document;
                    break;
                case PolicySource.Session:
                    _sessionPolicy = document;
                    break;
            }
            _loadedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Clears all loaded policies.
    /// </summary>
    public void ClearPolicies()
    {
        lock (_lock)
        {
            _orgPolicy = null;
            _teamPolicy = null;
            _userPolicy = null;
            _sessionPolicy = null;
            _loadedAt = DateTimeOffset.UtcNow;
        }
    }

    #region Tool Evaluation

    /// <summary>
    /// Evaluates whether a tool can be used.
    /// </summary>
    public PolicyEvaluationResult EvaluateTool(string toolId, string action = "execute")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        PolicyDocument? orgPolicy, teamPolicy, userPolicy, sessionPolicy;
        lock (_lock)
        {
            orgPolicy = _orgPolicy;
            teamPolicy = _teamPolicy;
            userPolicy = _userPolicy;
            sessionPolicy = _sessionPolicy;
        }

        // Check each policy in precedence order
        var result = EvaluateToolInPolicy(toolId, orgPolicy, PolicySource.Organization)
            ?? EvaluateToolInPolicy(toolId, teamPolicy, PolicySource.Team)
            ?? EvaluateToolInPolicy(toolId, userPolicy, PolicySource.User)
            ?? EvaluateToolInPolicy(toolId, sessionPolicy, PolicySource.Session)
            ?? GetDefaultToolDecision(toolId);

        LogEvaluation(PolicyCategory.Tools, toolId, action, result);
        return result;
    }

    private PolicyEvaluationResult? EvaluateToolInPolicy(string toolId, PolicyDocument? policy, PolicySource source)
    {
        if (policy?.Tools == null) return null;

        var tools = policy.Tools;
        var sourcePath = GetPathForSource(source);

        // Check deny list first (deny takes precedence) - supports patterns
        if (tools.Deny != null)
        {
            foreach (var pattern in tools.Deny)
            {
                if (MatchesPattern(toolId, pattern))
                {
                    return PolicyEvaluationResult.Deny(
                        $"Tool '{toolId}' is blocked by {source} policy (pattern: {pattern})",
                        source,
                        sourcePath,
                        $"tools.deny.{pattern}");
                }
            }
        }

        // Check detailed rules
        if (tools.Rules != null)
        {
            foreach (var rule in tools.Rules)
            {
                if (MatchesPattern(toolId, rule.Tool) && EvaluateConditions(rule.Conditions))
                {
                    return CreateResultFromRule(rule, source, sourcePath);
                }
            }
        }

        // Check require approval list - supports patterns
        if (tools.RequireApproval != null)
        {
            foreach (var pattern in tools.RequireApproval)
            {
                if (MatchesPattern(toolId, pattern))
                {
                    return PolicyEvaluationResult.RequireApproval(
                        $"Tool '{toolId}' requires approval per {source} policy (pattern: {pattern})",
                        source,
                        sourcePath);
                }
            }
        }

        // Check allow list - supports patterns
        if (tools.Allow != null)
        {
            foreach (var pattern in tools.Allow)
            {
                if (MatchesPattern(toolId, pattern))
                {
                    return PolicyEvaluationResult.Allow(
                        $"Tool '{toolId}' is explicitly allowed by {source} policy (pattern: {pattern})",
                        source,
                        sourcePath);
                }
            }
        }

        // If this policy has a locked flag and defines a default, use it
        if (policy.Locked && source == PolicySource.Organization)
        {
            return tools.Default switch
            {
                PolicyDecision.Allow => PolicyEvaluationResult.Allow(
                    $"Tool '{toolId}' allowed by organization default policy",
                    source,
                    sourcePath),
                PolicyDecision.Deny => PolicyEvaluationResult.Deny(
                    $"Tool '{toolId}' denied by organization default policy",
                    source,
                    sourcePath),
                PolicyDecision.AllowWithApproval => PolicyEvaluationResult.RequireApproval(
                    $"Tool '{toolId}' requires approval by organization default policy",
                    source,
                    sourcePath),
                _ => null
            };
        }

        return null; // Not determined by this policy
    }

    private static PolicyEvaluationResult GetDefaultToolDecision(string toolId)
    {
        return PolicyEvaluationResult.Allow(
            $"Tool '{toolId}' allowed by default policy",
            PolicySource.Default);
    }

    #endregion

    #region Plugin Evaluation

    /// <summary>
    /// Evaluates whether a plugin can be loaded.
    /// </summary>
    public PolicyEvaluationResult EvaluatePlugin(string pluginId, string? author = null, Plugins.PluginRiskLevel? riskLevel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        PolicyDocument? orgPolicy, teamPolicy, userPolicy, sessionPolicy;
        lock (_lock)
        {
            orgPolicy = _orgPolicy;
            teamPolicy = _teamPolicy;
            userPolicy = _userPolicy;
            sessionPolicy = _sessionPolicy;
        }

        // Check if plugins are disabled at any level
        var disabledResult = CheckPluginsDisabled(orgPolicy, PolicySource.Organization)
            ?? CheckPluginsDisabled(teamPolicy, PolicySource.Team)
            ?? CheckPluginsDisabled(userPolicy, PolicySource.User);

        if (disabledResult != null)
        {
            LogEvaluation(PolicyCategory.Plugins, pluginId, "load", disabledResult);
            return disabledResult;
        }

        // Check risk level limits
        if (riskLevel.HasValue)
        {
            var riskResult = CheckPluginRiskLevel(riskLevel.Value, orgPolicy, PolicySource.Organization)
                ?? CheckPluginRiskLevel(riskLevel.Value, teamPolicy, PolicySource.Team)
                ?? CheckPluginRiskLevel(riskLevel.Value, userPolicy, PolicySource.User);

            if (riskResult != null)
            {
                LogEvaluation(PolicyCategory.Plugins, pluginId, "load", riskResult);
                return riskResult;
            }
        }

        // Check each policy in precedence order
        var result = EvaluatePluginInPolicy(pluginId, author, orgPolicy, PolicySource.Organization)
            ?? EvaluatePluginInPolicy(pluginId, author, teamPolicy, PolicySource.Team)
            ?? EvaluatePluginInPolicy(pluginId, author, userPolicy, PolicySource.User)
            ?? EvaluatePluginInPolicy(pluginId, author, sessionPolicy, PolicySource.Session)
            ?? GetDefaultPluginDecision(pluginId);

        LogEvaluation(PolicyCategory.Plugins, pluginId, "load", result);
        return result;
    }

    private PolicyEvaluationResult? CheckPluginsDisabled(PolicyDocument? policy, PolicySource source)
    {
        if (policy?.Plugins?.Enabled == false)
        {
            return PolicyEvaluationResult.Deny(
                $"Plugins are disabled by {source} policy",
                source,
                GetPathForSource(source),
                "plugins.enabled.false");
        }
        return null;
    }

    private PolicyEvaluationResult? CheckPluginRiskLevel(Plugins.PluginRiskLevel riskLevel, PolicyDocument? policy, PolicySource source)
    {
        if (policy?.Plugins?.MaxRiskLevel == null) return null;

        var maxAllowed = policy.Plugins.MaxRiskLevel switch
        {
            PluginRiskLevelPolicy.ReadOnly => Plugins.PluginRiskLevel.ReadOnly,
            PluginRiskLevelPolicy.LocalMutation => Plugins.PluginRiskLevel.LocalMutation,
            PluginRiskLevelPolicy.Network => Plugins.PluginRiskLevel.Network,
            _ => Plugins.PluginRiskLevel.Network
        };

        if (riskLevel > maxAllowed)
        {
            return PolicyEvaluationResult.Deny(
                $"Plugin risk level {riskLevel} exceeds maximum allowed {maxAllowed} per {source} policy",
                source,
                GetPathForSource(source),
                $"plugins.maxRiskLevel.{policy.Plugins.MaxRiskLevel}");
        }

        return null;
    }

    private PolicyEvaluationResult? EvaluatePluginInPolicy(string pluginId, string? author, PolicyDocument? policy, PolicySource source)
    {
        if (policy?.Plugins == null) return null;

        var plugins = policy.Plugins;
        var sourcePath = GetPathForSource(source);

        // Check deny list first
        if (plugins.Deny != null)
        {
            foreach (var pattern in plugins.Deny)
            {
                if (MatchesPattern(pluginId, pattern))
                {
                    return PolicyEvaluationResult.Deny(
                        $"Plugin '{pluginId}' is blocked by {source} policy (pattern: {pattern})",
                        source,
                        sourcePath,
                        $"plugins.deny.{pattern}");
                }
            }
        }

        // Check detailed rules
        if (plugins.Rules != null)
        {
            foreach (var rule in plugins.Rules)
            {
                if (MatchesPattern(pluginId, rule.Plugin))
                {
                    return rule.Decision switch
                    {
                        PolicyDecision.Allow => PolicyEvaluationResult.Allow(
                            rule.Reason ?? $"Plugin '{pluginId}' allowed by rule {rule.Id}",
                            source,
                            sourcePath),
                        PolicyDecision.Deny => PolicyEvaluationResult.Deny(
                            rule.Reason ?? $"Plugin '{pluginId}' denied by rule {rule.Id}",
                            source,
                            sourcePath,
                            rule.Id),
                        PolicyDecision.AllowWithApproval => PolicyEvaluationResult.RequireApproval(
                            rule.Reason ?? $"Plugin '{pluginId}' requires approval per rule {rule.Id}",
                            source,
                            sourcePath),
                        _ => null
                    };
                }
            }
        }

        // Check trusted authors
        if (author != null && plugins.TrustedAuthors?.Contains(author, StringComparer.OrdinalIgnoreCase) == true)
        {
            return PolicyEvaluationResult.Allow(
                $"Plugin '{pluginId}' allowed - author '{author}' is trusted",
                source,
                sourcePath);
        }

        // Check allow list
        if (plugins.Allow != null)
        {
            foreach (var pattern in plugins.Allow)
            {
                if (MatchesPattern(pluginId, pattern))
                {
                    return PolicyEvaluationResult.Allow(
                        $"Plugin '{pluginId}' is allowed by {source} policy (pattern: {pattern})",
                        source,
                        sourcePath);
                }
            }
        }

        return null;
    }

    private static PolicyEvaluationResult GetDefaultPluginDecision(string pluginId)
    {
        return PolicyEvaluationResult.RequireApproval(
            $"Plugin '{pluginId}' requires approval (default policy)",
            PolicySource.Default);
    }

    #endregion

    #region Memory Evaluation

    /// <summary>
    /// Evaluates memory policy settings.
    /// </summary>
    public MemoryPolicyEvaluation EvaluateMemoryPolicy()
    {
        PolicyDocument? orgPolicy, teamPolicy, userPolicy;
        lock (_lock)
        {
            orgPolicy = _orgPolicy;
            teamPolicy = _teamPolicy;
            userPolicy = _userPolicy;
        }

        // Merge memory policies in precedence order
        var enabled = GetMemoryEnabled(orgPolicy) ?? GetMemoryEnabled(teamPolicy) ?? GetMemoryEnabled(userPolicy) ?? true;
        var maxRetention = GetMemoryMaxRetention(orgPolicy) ?? GetMemoryMaxRetention(teamPolicy) ?? GetMemoryMaxRetention(userPolicy) ?? 0;
        var maxMemories = GetMemoryMaxMemories(orgPolicy) ?? GetMemoryMaxMemories(teamPolicy) ?? GetMemoryMaxMemories(userPolicy) ?? 10000;
        var encryptAtRest = GetMemoryEncrypt(orgPolicy) ?? GetMemoryEncrypt(teamPolicy) ?? GetMemoryEncrypt(userPolicy) ?? true;
        var autoFormation = GetMemoryAutoFormation(orgPolicy) ?? GetMemoryAutoFormation(teamPolicy) ?? GetMemoryAutoFormation(userPolicy) ?? true;
        var allowExport = GetMemoryAllowExport(orgPolicy) ?? GetMemoryAllowExport(teamPolicy) ?? GetMemoryAllowExport(userPolicy) ?? true;
        var allowImport = GetMemoryAllowImport(orgPolicy) ?? GetMemoryAllowImport(teamPolicy) ?? GetMemoryAllowImport(userPolicy) ?? true;

        var excludeCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MergeExcludeCategories(excludeCategories, orgPolicy);
        MergeExcludeCategories(excludeCategories, teamPolicy);
        MergeExcludeCategories(excludeCategories, userPolicy);

        return new MemoryPolicyEvaluation
        {
            Enabled = enabled,
            MaxRetentionDays = maxRetention,
            MaxMemories = maxMemories,
            EncryptAtRest = encryptAtRest,
            AutoFormation = autoFormation,
            AllowExport = allowExport,
            AllowImport = allowImport,
            ExcludeCategories = excludeCategories.ToList()
        };
    }

    private static bool? GetMemoryEnabled(PolicyDocument? doc) => doc?.Memory?.Enabled;
    private static int? GetMemoryMaxRetention(PolicyDocument? doc) => doc?.Memory?.MaxRetentionDays > 0 ? doc.Memory.MaxRetentionDays : null;
    private static int? GetMemoryMaxMemories(PolicyDocument? doc) => doc?.Memory?.MaxMemories > 0 ? doc.Memory.MaxMemories : null;
    private static bool? GetMemoryEncrypt(PolicyDocument? doc) => doc?.Memory != null ? doc.Memory.EncryptAtRest : null;
    private static bool? GetMemoryAutoFormation(PolicyDocument? doc) => doc?.Memory != null ? doc.Memory.AutoFormation : null;
    private static bool? GetMemoryAllowExport(PolicyDocument? doc) => doc?.Memory != null ? doc.Memory.AllowExport : null;
    private static bool? GetMemoryAllowImport(PolicyDocument? doc) => doc?.Memory != null ? doc.Memory.AllowImport : null;

    private static void MergeExcludeCategories(HashSet<string> set, PolicyDocument? doc)
    {
        if (doc?.Memory?.ExcludeCategories != null)
        {
            foreach (var cat in doc.Memory.ExcludeCategories)
                set.Add(cat);
        }
    }

    #endregion

    #region Connectivity Evaluation

    /// <summary>
    /// Evaluates connectivity policy settings.
    /// </summary>
    public ConnectivityPolicyEvaluation EvaluateConnectivityPolicy()
    {
        PolicyDocument? orgPolicy, teamPolicy, userPolicy;
        lock (_lock)
        {
            orgPolicy = _orgPolicy;
            teamPolicy = _teamPolicy;
            userPolicy = _userPolicy;
        }

        var allowedModes = GetConnectivityAllowedModes(orgPolicy) ?? GetConnectivityAllowedModes(teamPolicy) ?? GetConnectivityAllowedModes(userPolicy);
        var defaultMode = GetConnectivityDefaultMode(orgPolicy) ?? GetConnectivityDefaultMode(teamPolicy) ?? GetConnectivityDefaultMode(userPolicy) ?? "online";
        var allowModeChange = GetConnectivityAllowChange(orgPolicy) ?? GetConnectivityAllowChange(teamPolicy) ?? GetConnectivityAllowChange(userPolicy) ?? true;
        var allowTelemetry = GetConnectivityAllowTelemetry(orgPolicy) ?? GetConnectivityAllowTelemetry(teamPolicy) ?? GetConnectivityAllowTelemetry(userPolicy) ?? true;

        var blockedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MergeBlockedDomains(blockedDomains, orgPolicy);
        MergeBlockedDomains(blockedDomains, teamPolicy);
        MergeBlockedDomains(blockedDomains, userPolicy);

        var allowedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (GetConnectivityAllowedDomains(orgPolicy) != null) MergeAllowedDomains(allowedDomains, orgPolicy);
        else if (GetConnectivityAllowedDomains(teamPolicy) != null) MergeAllowedDomains(allowedDomains, teamPolicy);
        else if (GetConnectivityAllowedDomains(userPolicy) != null) MergeAllowedDomains(allowedDomains, userPolicy);

        return new ConnectivityPolicyEvaluation
        {
            AllowedModes = allowedModes?.ToList(),
            DefaultMode = defaultMode,
            AllowModeChange = allowModeChange,
            AllowTelemetry = allowTelemetry,
            BlockedDomains = blockedDomains.ToList(),
            AllowedDomains = allowedDomains.Count > 0 ? allowedDomains.ToList() : null
        };
    }

    private static List<string>? GetConnectivityAllowedModes(PolicyDocument? doc) => doc?.Connectivity?.AllowedModes;
    private static string? GetConnectivityDefaultMode(PolicyDocument? doc) => doc?.Connectivity?.DefaultMode;
    private static bool? GetConnectivityAllowChange(PolicyDocument? doc) => doc?.Connectivity != null ? doc.Connectivity.AllowModeChange : null;
    private static bool? GetConnectivityAllowTelemetry(PolicyDocument? doc) => doc?.Connectivity != null ? doc.Connectivity.AllowTelemetry : null;
    private static List<string>? GetConnectivityAllowedDomains(PolicyDocument? doc) => doc?.Connectivity?.AllowedDomains;

    private static void MergeBlockedDomains(HashSet<string> set, PolicyDocument? doc)
    {
        if (doc?.Connectivity?.BlockedDomains != null)
        {
            foreach (var domain in doc.Connectivity.BlockedDomains)
                set.Add(domain);
        }
    }

    private static void MergeAllowedDomains(HashSet<string> set, PolicyDocument? doc)
    {
        if (doc?.Connectivity?.AllowedDomains != null)
        {
            foreach (var domain in doc.Connectivity.AllowedDomains)
                set.Add(domain);
        }
    }

    /// <summary>
    /// Checks if a domain is allowed.
    /// </summary>
    public PolicyEvaluationResult EvaluateDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        var connectivity = EvaluateConnectivityPolicy();

        // Check blocked domains first
        if (connectivity.BlockedDomains.Any(blocked =>
            domain.Equals(blocked, StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith("." + blocked, StringComparison.OrdinalIgnoreCase)))
        {
            var result = PolicyEvaluationResult.Deny(
                $"Domain '{domain}' is blocked by policy",
                PolicySource.Organization); // Simplified - could track actual source

            LogEvaluation(PolicyCategory.Connectivity, domain, "access", result);
            return result;
        }

        // If allowed domains are specified, domain must be in the list
        if (connectivity.AllowedDomains != null && connectivity.AllowedDomains.Count > 0)
        {
            var isAllowed = connectivity.AllowedDomains.Any(allowed =>
                domain.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                domain.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                var result = PolicyEvaluationResult.Deny(
                    $"Domain '{domain}' is not in the allowed domains list",
                    PolicySource.Organization);

                LogEvaluation(PolicyCategory.Connectivity, domain, "access", result);
                return result;
            }
        }

        var allowResult = PolicyEvaluationResult.Allow(
            $"Domain '{domain}' is permitted",
            PolicySource.Default);

        LogEvaluation(PolicyCategory.Connectivity, domain, "access", allowResult);
        return allowResult;
    }

    #endregion

    #region Update Evaluation

    /// <summary>
    /// Evaluates update policy settings.
    /// </summary>
    public UpdatePolicyEvaluation EvaluateUpdatePolicy()
    {
        PolicyDocument? orgPolicy, teamPolicy, userPolicy;
        lock (_lock)
        {
            orgPolicy = _orgPolicy;
            teamPolicy = _teamPolicy;
            userPolicy = _userPolicy;
        }

        var autoUpdate = GetUpdateAuto(orgPolicy) ?? GetUpdateAuto(teamPolicy) ?? GetUpdateAuto(userPolicy) ?? true;
        var checkOnStartup = GetUpdateCheckOnStartup(orgPolicy) ?? GetUpdateCheckOnStartup(teamPolicy) ?? GetUpdateCheckOnStartup(userPolicy) ?? true;
        var deferDays = GetUpdateDeferDays(orgPolicy) ?? GetUpdateDeferDays(teamPolicy) ?? GetUpdateDeferDays(userPolicy) ?? 0;
        var requiredChannel = GetUpdateRequiredChannel(orgPolicy) ?? GetUpdateRequiredChannel(teamPolicy) ?? GetUpdateRequiredChannel(userPolicy);
        var minimumVersion = GetUpdateMinVersion(orgPolicy) ?? GetUpdateMinVersion(teamPolicy) ?? GetUpdateMinVersion(userPolicy);

        var allowedChannels = GetUpdateAllowedChannels(orgPolicy) ?? GetUpdateAllowedChannels(teamPolicy) ?? GetUpdateAllowedChannels(userPolicy);

        return new UpdatePolicyEvaluation
        {
            AutoUpdate = autoUpdate,
            CheckOnStartup = checkOnStartup,
            DeferDays = deferDays,
            RequiredChannel = requiredChannel,
            AllowedChannels = allowedChannels?.ToList(),
            MinimumVersion = minimumVersion
        };
    }

    private static bool? GetUpdateAuto(PolicyDocument? doc) => doc?.Updates != null ? doc.Updates.AutoUpdate : null;
    private static bool? GetUpdateCheckOnStartup(PolicyDocument? doc) => doc?.Updates != null ? doc.Updates.CheckOnStartup : null;
    private static int? GetUpdateDeferDays(PolicyDocument? doc) => doc?.Updates?.DeferDays > 0 ? doc.Updates.DeferDays : null;
    private static string? GetUpdateRequiredChannel(PolicyDocument? doc) => doc?.Updates?.RequiredChannel;
    private static string? GetUpdateMinVersion(PolicyDocument? doc) => doc?.Updates?.MinimumVersion;
    private static List<string>? GetUpdateAllowedChannels(PolicyDocument? doc) => doc?.Updates?.AllowedChannels;

    #endregion

    #region Helpers

    private static bool MatchesPattern(string value, string pattern)
    {
        if (pattern == "*") return true;

        if (pattern.Contains('*'))
        {
            // Convert glob pattern to regex, using a cache to avoid recompilation
            var regex = _patternCache.GetOrAdd(pattern, p =>
                new Regex(
                    "^" + Regex.Escape(p).Replace("\\*", ".*") + "$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled));
            return regex.IsMatch(value);
        }

        return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateConditions(RuleConditions? conditions)
    {
        if (conditions == null) return true;

        // Time range check
        if (!string.IsNullOrEmpty(conditions.TimeRange))
        {
            var parts = conditions.TimeRange.Split('-');
            if (parts.Length == 2 &&
                TimeOnly.TryParse(parts[0], out var start) &&
                TimeOnly.TryParse(parts[1], out var end))
            {
                var now = TimeOnly.FromDateTime(DateTime.Now);
                if (start <= end)
                {
                    if (now < start || now > end) return false;
                }
                else // Wraps midnight
                {
                    if (now < start && now > end) return false;
                }
            }
        }

        // Day of week check
        if (conditions.DaysOfWeek != null && conditions.DaysOfWeek.Count > 0)
        {
            var today = (int)DateTime.Now.DayOfWeek;
            if (!conditions.DaysOfWeek.Contains(today)) return false;
        }

        return true;
    }

    private static PolicyEvaluationResult CreateResultFromRule(ToolRule rule, PolicySource source, string? sourcePath)
    {
        return rule.Decision switch
        {
            PolicyDecision.Allow => PolicyEvaluationResult.Allow(
                rule.Reason ?? $"Allowed by rule {rule.Id}",
                source,
                sourcePath),
            PolicyDecision.Deny => PolicyEvaluationResult.Deny(
                rule.Reason ?? $"Denied by rule {rule.Id}",
                source,
                sourcePath,
                rule.Id),
            PolicyDecision.AllowWithApproval => PolicyEvaluationResult.RequireApproval(
                rule.Reason ?? $"Requires approval per rule {rule.Id}",
                source,
                sourcePath),
            PolicyDecision.AllowWithConstraints when rule.Constraints != null => PolicyEvaluationResult.AllowConstrained(
                rule.Reason ?? $"Allowed with constraints per rule {rule.Id}",
                source,
                rule.Constraints.AsReadOnly(),
                sourcePath),
            _ => PolicyEvaluationResult.Allow(
                rule.Reason ?? $"Allowed by rule {rule.Id}",
                source,
                sourcePath)
        };
    }

    private string? GetPathForSource(PolicySource source)
    {
        return source switch
        {
            PolicySource.Organization => PolicyPaths.GetOrgPolicyPath(),
            PolicySource.Team => PolicyPaths.GetTeamPolicyPath(),
            PolicySource.User => PolicyPaths.GetUserPolicyPath(),
            _ => null
        };
    }

    private void LogEvaluation(PolicyCategory category, string subject, string action, PolicyEvaluationResult result)
    {
        var entry = new PolicyAuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = category,
            Subject = subject,
            Action = action,
            Decision = result.Decision,
            Reason = result.Reason,
            Source = result.Source,
            SourcePath = result.SourcePath,
            RuleId = result.RuleId
        };

        lock (_lock)
        {
            _auditLog.Add(entry);

            // Keep audit log bounded
            if (_auditLog.Count > 10000)
            {
                _auditLog.RemoveRange(0, 1000);
            }
        }

        PolicyEvaluated?.Invoke(this, entry);
    }

    /// <summary>
    /// Gets recent audit entries.
    /// </summary>
    public IReadOnlyList<PolicyAuditEntry> GetAuditLog(int limit = 100)
    {
        lock (_lock)
        {
            return _auditLog.TakeLast(limit).Reverse().ToList();
        }
    }

    /// <summary>
    /// Clears the audit log.
    /// </summary>
    public void ClearAuditLog()
    {
        lock (_lock)
        {
            _auditLog.Clear();
        }
    }

    #endregion
}

#region Evaluation Results

/// <summary>
/// Summary of policy loading.
/// </summary>
public sealed record PolicyLoadSummary(
    IReadOnlyList<(PolicySource Source, string Path)> LoadedPolicies,
    IReadOnlyList<string> Errors)
{
    public bool HasErrors => Errors.Count > 0;
    public int PolicyCount => LoadedPolicies.Count;
}

/// <summary>
/// Evaluated memory policy settings.
/// </summary>
public sealed record MemoryPolicyEvaluation
{
    public bool Enabled { get; init; }
    public int MaxRetentionDays { get; init; }
    public int MaxMemories { get; init; }
    public bool EncryptAtRest { get; init; }
    public bool AutoFormation { get; init; }
    public bool AllowExport { get; init; }
    public bool AllowImport { get; init; }
    public IReadOnlyList<string> ExcludeCategories { get; init; } = [];
}

/// <summary>
/// Evaluated connectivity policy settings.
/// </summary>
public sealed record ConnectivityPolicyEvaluation
{
    public IReadOnlyList<string>? AllowedModes { get; init; }
    public string DefaultMode { get; init; } = "online";
    public bool AllowModeChange { get; init; }
    public bool AllowTelemetry { get; init; }
    public IReadOnlyList<string> BlockedDomains { get; init; } = [];
    public IReadOnlyList<string>? AllowedDomains { get; init; }
}

/// <summary>
/// Evaluated update policy settings.
/// </summary>
public sealed record UpdatePolicyEvaluation
{
    public bool AutoUpdate { get; init; }
    public bool CheckOnStartup { get; init; }
    public int DeferDays { get; init; }
    public string? RequiredChannel { get; init; }
    public IReadOnlyList<string>? AllowedChannels { get; init; }
    public string? MinimumVersion { get; init; }
}

#endregion

/// <summary>
/// Extension for dictionary readonly wrapper.
/// </summary>
internal static class DictionaryExtensions
{
    public static IReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this Dictionary<TKey, TValue> dict) where TKey : notnull
        => dict;
}
