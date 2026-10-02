namespace InControl.Core.Policy;

/// <summary>
/// Provides diagnostic and debugging information about policy evaluation.
/// Helps operators understand why decisions are being made.
/// </summary>
public sealed class PolicyDiagnostics
{
    private readonly PolicyEngine _engine;
    private readonly List<PolicyEvaluationTrace> _traceLog = [];
    private readonly object _lock = new();
    private bool _tracingEnabled;
    private int _maxTraceEntries = 1000;

    public PolicyDiagnostics(PolicyEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Gets or sets whether tracing is enabled.
    /// </summary>
    public bool TracingEnabled
    {
        get => _tracingEnabled;
        set => _tracingEnabled = value;
    }

    /// <summary>
    /// Gets or sets maximum trace entries to retain.
    /// </summary>
    public int MaxTraceEntries
    {
        get => _maxTraceEntries;
        set => _maxTraceEntries = value > 0 ? value : 1000;
    }

    #region Policy Summary

    /// <summary>
    /// Gets a comprehensive summary of all active policies.
    /// </summary>
    public PolicySummary GetPolicySummary()
    {
        var sources = new List<PolicySourceInfo>();
        var effectiveInfo = _engine.GetEffectivePolicy();

        // Add active sources
        if (effectiveInfo.HasOrgPolicy)
        {
            sources.Add(new PolicySourceInfo(
                Source: PolicySource.Organization,
                Version: "1.0", // Policy engine doesn't expose version directly
                LoadedAt: effectiveInfo.LoadedAt,
                Description: null,
                RuleCount: 0));
        }

        if (effectiveInfo.HasTeamPolicy)
        {
            sources.Add(new PolicySourceInfo(
                Source: PolicySource.Team,
                Version: "1.0",
                LoadedAt: effectiveInfo.LoadedAt,
                Description: null,
                RuleCount: 0));
        }

        if (effectiveInfo.HasUserPolicy)
        {
            sources.Add(new PolicySourceInfo(
                Source: PolicySource.User,
                Version: "1.0",
                LoadedAt: effectiveInfo.LoadedAt,
                Description: null,
                RuleCount: 0));
        }

        return new PolicySummary(
            ActiveSources: sources,
            EffectiveInfo: effectiveInfo,
            ToolRuleCount: 0, // Would need deep inspection
            PluginRuleCount: 0,
            MemoryRestrictions: null,
            ConnectivityRestrictions: null,
            UpdateRestrictions: null);
    }

    #endregion

    #region Tool Explanation

    /// <summary>
    /// Explains why a tool has its current policy status.
    /// </summary>
    public ToolPolicyExplanation ExplainToolPolicy(string toolId)
    {
        var result = _engine.EvaluateTool(toolId);
        var steps = new List<EvaluationStep>();

        // The engine returns the final result; we simulate steps based on result
        if (result.Source != PolicySource.Default)
        {
            steps.Add(new EvaluationStep(
                Source: result.Source,
                RuleName: result.Decision == PolicyDecision.Deny ? "deny" :
                          result.Decision == PolicyDecision.AllowWithApproval ? "requireApproval" :
                          result.Decision == PolicyDecision.AllowWithConstraints ? "rule" : "allow",
                Pattern: result.RuleId,
                Result: result.Decision,
                IsDecisive: true,
                Constraints: result.Constraints));
        }

        return new ToolPolicyExplanation(
            ToolId: toolId,
            FinalDecision: result.Decision,
            FinalSource: result.Source,
            Reason: result.Reason,
            EvaluationSteps: steps,
            RuleId: result.RuleId,
            Constraints: result.Constraints);
    }

    #endregion

    #region Plugin Explanation

    /// <summary>
    /// Explains why a plugin has its current policy status.
    /// </summary>
    public PluginPolicyExplanation ExplainPluginPolicy(string pluginId, string? author = null)
    {
        var result = _engine.EvaluatePlugin(pluginId, author, null);
        var steps = new List<EvaluationStep>();

        if (result.Source != PolicySource.Default)
        {
            steps.Add(new EvaluationStep(
                Source: result.Source,
                RuleName: result.Decision == PolicyDecision.Deny ? "deny" :
                          result.Decision == PolicyDecision.AllowWithApproval ? "requireApproval" :
                          author != null && result.Decision == PolicyDecision.Allow ? "trustedAuthor" : "allow",
                Pattern: result.RuleId,
                Result: result.Decision,
                IsDecisive: true));
        }

        return new PluginPolicyExplanation(
            PluginId: pluginId,
            Author: author,
            FinalDecision: result.Decision,
            FinalSource: result.Source,
            Reason: result.Reason,
            EvaluationSteps: steps,
            RuleId: result.RuleId);
    }

    #endregion

    #region Tracing

    /// <summary>
    /// Records a policy evaluation for tracing.
    /// </summary>
    public void RecordEvaluation(PolicyCategory category, string subject, PolicyEvaluationResult result)
    {
        if (!_tracingEnabled) return;

        lock (_lock)
        {
            _traceLog.Add(new PolicyEvaluationTrace(
                Timestamp: DateTimeOffset.UtcNow,
                Category: category,
                Subject: subject,
                Decision: result.Decision,
                Source: result.Source,
                Reason: result.Reason,
                RuleId: result.RuleId));

            // Trim if needed
            while (_traceLog.Count > _maxTraceEntries)
            {
                _traceLog.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Gets recent trace entries.
    /// </summary>
    public IReadOnlyList<PolicyEvaluationTrace> GetRecentTraces(int count = 100)
    {
        lock (_lock)
        {
            return _traceLog
                .TakeLast(count)
                .ToList();
        }
    }

    /// <summary>
    /// Gets traces for a specific subject.
    /// </summary>
    public IReadOnlyList<PolicyEvaluationTrace> GetTracesForSubject(string subject, int count = 50)
    {
        lock (_lock)
        {
            return _traceLog
                .Where(t => t.Subject == subject)
                .TakeLast(count)
                .ToList();
        }
    }

    /// <summary>
    /// Gets traces that resulted in deny.
    /// </summary>
    public IReadOnlyList<PolicyEvaluationTrace> GetDeniedTraces(int count = 100)
    {
        lock (_lock)
        {
            return _traceLog
                .Where(t => t.Decision == PolicyDecision.Deny)
                .TakeLast(count)
                .ToList();
        }
    }

    /// <summary>
    /// Clears the trace log.
    /// </summary>
    public void ClearTraces()
    {
        lock (_lock)
        {
            _traceLog.Clear();
        }
    }

    /// <summary>
    /// Gets trace statistics.
    /// </summary>
    public PolicyTraceStatistics GetTraceStatistics()
    {
        lock (_lock)
        {
            if (_traceLog.Count == 0)
            {
                return new PolicyTraceStatistics(
                    TotalEvaluations: 0,
                    AllowCount: 0,
                    DenyCount: 0,
                    ApprovalRequiredCount: 0,
                    ConstrainedCount: 0,
                    ByCategory: new Dictionary<PolicyCategory, int>(),
                    BySource: new Dictionary<PolicySource, int>(),
                    EarliestTrace: null,
                    LatestTrace: null);
            }

            return new PolicyTraceStatistics(
                TotalEvaluations: _traceLog.Count,
                AllowCount: _traceLog.Count(t => t.Decision == PolicyDecision.Allow),
                DenyCount: _traceLog.Count(t => t.Decision == PolicyDecision.Deny),
                ApprovalRequiredCount: _traceLog.Count(t => t.Decision == PolicyDecision.AllowWithApproval),
                ConstrainedCount: _traceLog.Count(t => t.Decision == PolicyDecision.AllowWithConstraints),
                ByCategory: _traceLog.GroupBy(t => t.Category).ToDictionary(g => g.Key, g => g.Count()),
                BySource: _traceLog.GroupBy(t => t.Source).ToDictionary(g => g.Key, g => g.Count()),
                EarliestTrace: _traceLog.Min(t => t.Timestamp),
                LatestTrace: _traceLog.Max(t => t.Timestamp));
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates current policy configuration for potential issues.
    /// </summary>
    public PolicyValidationReport ValidateCurrentPolicy()
    {
        var issues = new List<PolicyIssue>();
        var effectiveInfo = _engine.GetEffectivePolicy();

        // Suggest configuring important sections if no policies loaded
        if (!effectiveInfo.HasOrgPolicy && !effectiveInfo.HasTeamPolicy && !effectiveInfo.HasUserPolicy)
        {
            issues.Add(new PolicyIssue(
                Severity: IssueSeverity.Info,
                Category: PolicyCategory.Memory,
                Code: "MEMORY_NOT_CONFIGURED",
                Message: "Memory policy is not explicitly configured",
                Suggestion: "Consider setting retention and capacity limits"));

            issues.Add(new PolicyIssue(
                Severity: IssueSeverity.Info,
                Category: PolicyCategory.Updates,
                Code: "UPDATES_NOT_CONFIGURED",
                Message: "Update policy is not explicitly configured",
                Suggestion: "Consider setting channel restrictions for managed environments"));
        }

        return new PolicyValidationReport(
            IsValid: issues.All(i => i.Severity != IssueSeverity.Error),
            Issues: issues,
            CheckedAt: DateTimeOffset.UtcNow);
    }

    #endregion
}

#region Diagnostic Types

/// <summary>
/// Summary of all active policies.
/// </summary>
public sealed record PolicySummary(
    IReadOnlyList<PolicySourceInfo> ActiveSources,
    EffectivePolicyInfo EffectiveInfo,
    int ToolRuleCount,
    int PluginRuleCount,
    string? MemoryRestrictions,
    string? ConnectivityRestrictions,
    string? UpdateRestrictions);

/// <summary>
/// Information about a policy source.
/// </summary>
public sealed record PolicySourceInfo(
    PolicySource Source,
    string Version,
    DateTimeOffset LoadedAt,
    string? Description,
    int RuleCount);

/// <summary>
/// Explanation of tool policy evaluation.
/// </summary>
public sealed record ToolPolicyExplanation(
    string ToolId,
    PolicyDecision FinalDecision,
    PolicySource FinalSource,
    string Reason,
    IReadOnlyList<EvaluationStep> EvaluationSteps,
    string? RuleId,
    IReadOnlyDictionary<string, object>? Constraints);

/// <summary>
/// Explanation of plugin policy evaluation.
/// </summary>
public sealed record PluginPolicyExplanation(
    string PluginId,
    string? Author,
    PolicyDecision FinalDecision,
    PolicySource FinalSource,
    string Reason,
    IReadOnlyList<EvaluationStep> EvaluationSteps,
    string? RuleId);

/// <summary>
/// A single step in policy evaluation.
/// </summary>
public sealed record EvaluationStep(
    PolicySource Source,
    string RuleName,
    string? Pattern,
    PolicyDecision Result,
    bool IsDecisive,
    IReadOnlyDictionary<string, object>? Constraints = null);

/// <summary>
/// A recorded policy evaluation for tracing.
/// </summary>
public sealed record PolicyEvaluationTrace(
    DateTimeOffset Timestamp,
    PolicyCategory Category,
    string Subject,
    PolicyDecision Decision,
    PolicySource Source,
    string Reason,
    string? RuleId);

/// <summary>
/// Statistics about policy traces.
/// </summary>
public sealed record PolicyTraceStatistics(
    int TotalEvaluations,
    int AllowCount,
    int DenyCount,
    int ApprovalRequiredCount,
    int ConstrainedCount,
    IReadOnlyDictionary<PolicyCategory, int> ByCategory,
    IReadOnlyDictionary<PolicySource, int> BySource,
    DateTimeOffset? EarliestTrace,
    DateTimeOffset? LatestTrace);

/// <summary>
/// Report from policy validation.
/// </summary>
public sealed record PolicyValidationReport(
    bool IsValid,
    IReadOnlyList<PolicyIssue> Issues,
    DateTimeOffset CheckedAt);

/// <summary>
/// A policy configuration issue.
/// </summary>
public sealed record PolicyIssue(
    IssueSeverity Severity,
    PolicyCategory Category,
    string Code,
    string Message,
    string? Suggestion);

/// <summary>
/// Severity of a policy issue.
/// </summary>
public enum IssueSeverity
{
    Info,
    Warning,
    Error
}

#endregion
