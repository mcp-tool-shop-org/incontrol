using System.Text;

namespace InControl.Core.Policy;

/// <summary>
/// Provides human-readable views of policy configuration and status.
/// Designed for admin UX and troubleshooting.
/// </summary>
public sealed class PolicyViewer
{
    private readonly PolicyEngine _engine;
    private readonly PolicyDiagnostics _diagnostics;

    public PolicyViewer(PolicyEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _diagnostics = new PolicyDiagnostics(engine);
    }

    /// <summary>
    /// Gets the diagnostics instance for detailed analysis.
    /// </summary>
    public PolicyDiagnostics Diagnostics => _diagnostics;

    #region Status Views

    /// <summary>
    /// Gets a quick status view for the policy system.
    /// </summary>
    public PolicyStatusView GetStatusView()
    {
        var summary = _diagnostics.GetPolicySummary();
        var validation = _diagnostics.ValidateCurrentPolicy();

        return new PolicyStatusView(
            IsHealthy: validation.IsValid,
            ActiveSourceCount: summary.ActiveSources.Count,
            TotalRuleCount: summary.ToolRuleCount + summary.PluginRuleCount,
            WarningCount: validation.Issues.Count(i => i.Severity == IssueSeverity.Warning),
            ErrorCount: validation.Issues.Count(i => i.Severity == IssueSeverity.Error),
            Summary: BuildQuickSummary(summary));
    }

    private static string BuildQuickSummary(PolicySummary summary)
    {
        var parts = new List<string>();

        if (summary.ActiveSources.Count > 0)
        {
            parts.Add($"{summary.ActiveSources.Count} policy source(s) active");
        }

        var restrictions = new List<string>();
        if (summary.MemoryRestrictions != null) restrictions.Add("memory");
        if (summary.ConnectivityRestrictions != null) restrictions.Add("connectivity");
        if (summary.UpdateRestrictions != null) restrictions.Add("updates");

        if (restrictions.Count > 0)
        {
            parts.Add($"restrictions on: {string.Join(", ", restrictions)}");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "No active policies";
    }

    #endregion

    #region Source Views

    /// <summary>
    /// Gets all policy sources with their details.
    /// </summary>
    public IReadOnlyList<PolicySourceView> GetSourceViews()
    {
        var views = new List<PolicySourceView>();
        var effectiveInfo = _engine.GetEffectivePolicy();

        views.Add(new PolicySourceView(
            Source: PolicySource.Organization,
            IsActive: effectiveInfo.HasOrgPolicy,
            Version: effectiveInfo.HasOrgPolicy ? "1.0" : null,
            Description: null,
            Path: PolicyPaths.GetOrgPolicyPath(),
            ToolRules: null,
            PluginRules: null,
            HasMemoryRules: false,
            HasConnectivityRules: false,
            HasUpdateRules: false));

        views.Add(new PolicySourceView(
            Source: PolicySource.Team,
            IsActive: effectiveInfo.HasTeamPolicy,
            Version: effectiveInfo.HasTeamPolicy ? "1.0" : null,
            Description: null,
            Path: PolicyPaths.GetTeamPolicyPath(),
            ToolRules: null,
            PluginRules: null,
            HasMemoryRules: false,
            HasConnectivityRules: false,
            HasUpdateRules: false));

        views.Add(new PolicySourceView(
            Source: PolicySource.User,
            IsActive: effectiveInfo.HasUserPolicy,
            Version: effectiveInfo.HasUserPolicy ? "1.0" : null,
            Description: null,
            Path: PolicyPaths.GetUserPolicyPath(),
            ToolRules: null,
            PluginRules: null,
            HasMemoryRules: false,
            HasConnectivityRules: false,
            HasUpdateRules: false));

        views.Add(new PolicySourceView(
            Source: PolicySource.Session,
            IsActive: false, // Session is set programmatically
            Version: null,
            Description: null,
            Path: null,
            ToolRules: null,
            PluginRules: null,
            HasMemoryRules: false,
            HasConnectivityRules: false,
            HasUpdateRules: false));

        return views;
    }

    #endregion

    #region Tool Views

    /// <summary>
    /// Gets policy view for a specific tool.
    /// </summary>
    public ToolPolicyView GetToolView(string toolId)
    {
        var explanation = _diagnostics.ExplainToolPolicy(toolId);

        return new ToolPolicyView(
            ToolId: toolId,
            Decision: explanation.FinalDecision,
            Source: explanation.FinalSource,
            Reason: explanation.Reason,
            CanExecute: explanation.FinalDecision == PolicyDecision.Allow ||
                        explanation.FinalDecision == PolicyDecision.AllowWithConstraints,
            RequiresApproval: explanation.FinalDecision == PolicyDecision.AllowWithApproval,
            HasConstraints: explanation.Constraints != null && explanation.Constraints.Count > 0,
            ConstraintSummary: FormatConstraints(explanation.Constraints),
            EvaluationPath: FormatEvaluationPath(explanation.EvaluationSteps));
    }

    /// <summary>
    /// Gets policy views for multiple tools.
    /// </summary>
    public IReadOnlyList<ToolPolicyView> GetToolViews(IEnumerable<string> toolIds)
    {
        return toolIds.Select(GetToolView).ToList();
    }

    private static string? FormatConstraints(IReadOnlyDictionary<string, object>? constraints)
    {
        if (constraints == null || constraints.Count == 0) return null;

        return string.Join(", ", constraints.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }

    private static string FormatEvaluationPath(IReadOnlyList<EvaluationStep> steps)
    {
        if (steps.Count == 0) return "default allow";

        var sb = new StringBuilder();
        foreach (var step in steps)
        {
            if (sb.Length > 0) sb.Append(" → ");
            sb.Append($"{step.Source}.{step.RuleName}");
            if (step.Pattern != null) sb.Append($"({step.Pattern})");
            sb.Append($"={step.Result}");
        }

        return sb.ToString();
    }

    #endregion

    #region Plugin Views

    /// <summary>
    /// Gets policy view for a specific plugin.
    /// </summary>
    public PluginPolicyView GetPluginView(string pluginId, string? author = null)
    {
        var explanation = _diagnostics.ExplainPluginPolicy(pluginId, author);

        return new PluginPolicyView(
            PluginId: pluginId,
            Author: author,
            Decision: explanation.FinalDecision,
            Source: explanation.FinalSource,
            Reason: explanation.Reason,
            CanLoad: explanation.FinalDecision == PolicyDecision.Allow,
            RequiresApproval: explanation.FinalDecision == PolicyDecision.AllowWithApproval,
            IsDenied: explanation.FinalDecision == PolicyDecision.Deny,
            EvaluationPath: FormatEvaluationPath(explanation.EvaluationSteps));
    }

    #endregion

    #region Memory Views

    /// <summary>
    /// Gets policy view for memory subsystem.
    /// </summary>
    public MemoryPolicyView GetMemoryView()
    {
        var result = _engine.EvaluateMemoryPolicy();

        return new MemoryPolicyView(
            IsAllowed: result.Enabled,
            Source: PolicySource.Default, // MemoryPolicyEvaluation doesn't have Source
            MaxEntries: result.MaxMemories,
            RetentionDays: result.MaxRetentionDays,
            ExcludedCategories: result.ExcludeCategories,
            RequireExplicitConsent: false, // Not in the schema
            Summary: BuildMemorySummary(result));
    }

    private static string BuildMemorySummary(MemoryPolicyEvaluation policy)
    {
        if (!policy.Enabled) return "Memory is disabled";

        var parts = new List<string>();
        if (policy.MaxMemories > 0) parts.Add($"max {policy.MaxMemories} entries");
        if (policy.MaxRetentionDays > 0) parts.Add($"{policy.MaxRetentionDays} day retention");
        if (policy.ExcludeCategories.Count > 0) parts.Add($"{policy.ExcludeCategories.Count} excluded categories");

        return parts.Count > 0 ? string.Join(", ", parts) : "No restrictions";
    }

    #endregion

    #region Connectivity Views

    /// <summary>
    /// Gets policy view for connectivity.
    /// </summary>
    public ConnectivityPolicyView GetConnectivityView()
    {
        var result = _engine.EvaluateConnectivityPolicy();

        return new ConnectivityPolicyView(
            Source: PolicySource.Default, // ConnectivityPolicyEvaluation doesn't have Source
            AllowedModes: result.AllowedModes ?? [],
            BlockedDomainCount: result.BlockedDomains.Count,
            AllowedDomainCount: result.AllowedDomains?.Count ?? 0,
            AllowTelemetry: result.AllowTelemetry,
            AllowAnalytics: true, // Not in the schema
            Summary: BuildConnectivitySummary(result));
    }

    /// <summary>
    /// Checks if a domain is allowed under current policy.
    /// </summary>
    public DomainCheckView CheckDomain(string domain)
    {
        var result = _engine.EvaluateDomain(domain);

        return new DomainCheckView(
            Domain: domain,
            IsAllowed: result.Decision != PolicyDecision.Deny,
            Decision: result.Decision,
            Source: result.Source,
            Reason: result.Reason,
            MatchedRule: result.RuleId);
    }

    private static string BuildConnectivitySummary(ConnectivityPolicyEvaluation policy)
    {
        var parts = new List<string>();

        if (policy.AllowedModes != null && policy.AllowedModes.Count > 0)
            parts.Add($"modes: {string.Join("/", policy.AllowedModes)}");

        if (policy.BlockedDomains.Count > 0)
            parts.Add($"{policy.BlockedDomains.Count} blocked domains");

        if (!policy.AllowTelemetry) parts.Add("telemetry blocked");

        return parts.Count > 0 ? string.Join(", ", parts) : "No restrictions";
    }

    #endregion

    #region Update Views

    /// <summary>
    /// Gets policy view for updates.
    /// </summary>
    public UpdatePolicyView GetUpdateView()
    {
        var result = _engine.EvaluateUpdatePolicy();

        return new UpdatePolicyView(
            Source: PolicySource.Default, // UpdatePolicyEvaluation doesn't have Source
            AutoUpdateEnabled: result.AutoUpdate,
            CheckOnStartup: result.CheckOnStartup,
            RequiredChannel: result.RequiredChannel,
            AllowedChannels: result.AllowedChannels ?? [],
            DeferDays: result.DeferDays,
            MinimumVersion: result.MinimumVersion,
            Summary: BuildUpdateSummary(result));
    }

    private static string BuildUpdateSummary(UpdatePolicyEvaluation policy)
    {
        var parts = new List<string>();

        if (!policy.AutoUpdate) parts.Add("auto-update disabled");
        if (policy.RequiredChannel != null) parts.Add($"channel: {policy.RequiredChannel}");
        if (policy.DeferDays > 0) parts.Add($"{policy.DeferDays}d deferral");
        if (policy.MinimumVersion != null) parts.Add($"min: {policy.MinimumVersion}");

        return parts.Count > 0 ? string.Join(", ", parts) : "Default update behavior";
    }

    #endregion

    #region Export

    /// <summary>
    /// Exports current policy configuration as text.
    /// </summary>
    public string ExportAsText()
    {
        var sb = new StringBuilder();
        var summary = _diagnostics.GetPolicySummary();

        sb.AppendLine("=== Policy Configuration Report ===");
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Sources
        sb.AppendLine("## Active Policy Sources");
        foreach (var source in summary.ActiveSources)
        {
            sb.AppendLine($"  - {source.Source}: v{source.Version}");
            if (source.Description != null)
                sb.AppendLine($"    Description: {source.Description}");
        }
        if (summary.ActiveSources.Count == 0)
            sb.AppendLine("  (no policies configured)");
        sb.AppendLine();

        // Memory
        var memoryView = GetMemoryView();
        sb.AppendLine("## Memory Policy");
        sb.AppendLine($"  {memoryView.Summary}");
        sb.AppendLine();

        // Connectivity
        var connView = GetConnectivityView();
        sb.AppendLine("## Connectivity Policy");
        sb.AppendLine($"  {connView.Summary}");
        sb.AppendLine();

        // Updates
        var updateView = GetUpdateView();
        sb.AppendLine("## Update Policy");
        sb.AppendLine($"  {updateView.Summary}");
        sb.AppendLine();

        // Validation
        var validation = _diagnostics.ValidateCurrentPolicy();
        sb.AppendLine("## Validation");
        sb.AppendLine($"  Valid: {(validation.IsValid ? "Yes" : "No")}");
        if (validation.Issues.Count > 0)
        {
            sb.AppendLine("  Issues:");
            foreach (var issue in validation.Issues)
            {
                sb.AppendLine($"    [{issue.Severity}] {issue.Code}: {issue.Message}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports current effective policy info as JSON string.
    /// </summary>
    public string ExportAsJson()
    {
        var effectiveInfo = _engine.GetEffectivePolicy();
        return System.Text.Json.JsonSerializer.Serialize(effectiveInfo, PolicyJsonOptions.Default);
    }

    #endregion
}

#region View Types

/// <summary>
/// Quick status view for policy system.
/// </summary>
public sealed record PolicyStatusView(
    bool IsHealthy,
    int ActiveSourceCount,
    int TotalRuleCount,
    int WarningCount,
    int ErrorCount,
    string Summary);

/// <summary>
/// View of a policy source.
/// </summary>
public sealed record PolicySourceView(
    PolicySource Source,
    bool IsActive,
    string? Version,
    string? Description,
    string? Path,
    string? ToolRules,
    string? PluginRules,
    bool HasMemoryRules,
    bool HasConnectivityRules,
    bool HasUpdateRules);

/// <summary>
/// View of a tool's policy status.
/// </summary>
public sealed record ToolPolicyView(
    string ToolId,
    PolicyDecision Decision,
    PolicySource Source,
    string Reason,
    bool CanExecute,
    bool RequiresApproval,
    bool HasConstraints,
    string? ConstraintSummary,
    string EvaluationPath);

/// <summary>
/// View of a plugin's policy status.
/// </summary>
public sealed record PluginPolicyView(
    string PluginId,
    string? Author,
    PolicyDecision Decision,
    PolicySource Source,
    string Reason,
    bool CanLoad,
    bool RequiresApproval,
    bool IsDenied,
    string EvaluationPath);

/// <summary>
/// View of memory policy.
/// </summary>
public sealed record MemoryPolicyView(
    bool IsAllowed,
    PolicySource Source,
    int MaxEntries,
    int RetentionDays,
    IReadOnlyList<string> ExcludedCategories,
    bool RequireExplicitConsent,
    string Summary);

/// <summary>
/// View of connectivity policy.
/// </summary>
public sealed record ConnectivityPolicyView(
    PolicySource Source,
    IReadOnlyList<string> AllowedModes,
    int BlockedDomainCount,
    int AllowedDomainCount,
    bool AllowTelemetry,
    bool AllowAnalytics,
    string Summary);

/// <summary>
/// View of domain check result.
/// </summary>
public sealed record DomainCheckView(
    string Domain,
    bool IsAllowed,
    PolicyDecision Decision,
    PolicySource Source,
    string Reason,
    string? MatchedRule);

/// <summary>
/// View of update policy.
/// </summary>
public sealed record UpdatePolicyView(
    PolicySource Source,
    bool AutoUpdateEnabled,
    bool CheckOnStartup,
    string? RequiredChannel,
    IReadOnlyList<string> AllowedChannels,
    int DeferDays,
    string? MinimumVersion,
    string Summary);

#endregion
