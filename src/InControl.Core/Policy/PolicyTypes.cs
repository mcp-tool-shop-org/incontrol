namespace InControl.Core.Policy;

/// <summary>
/// Policy decision types returned by the policy engine.
/// Every decision includes an explanation.
/// </summary>
public enum PolicyDecision
{
    /// <summary>
    /// Action is permitted without restriction.
    /// </summary>
    Allow,

    /// <summary>
    /// Action is blocked. Explanation required.
    /// </summary>
    Deny,

    /// <summary>
    /// Action is permitted after operator confirms.
    /// </summary>
    AllowWithApproval,

    /// <summary>
    /// Action is permitted with restrictions applied.
    /// </summary>
    AllowWithConstraints
}

/// <summary>
/// Policy sources in order of precedence (highest first).
/// </summary>
public enum PolicySource
{
    /// <summary>
    /// Organization-wide policy (highest precedence).
    /// </summary>
    Organization = 1,

    /// <summary>
    /// Team or workgroup policy.
    /// </summary>
    Team = 2,

    /// <summary>
    /// Individual user policy.
    /// </summary>
    User = 3,

    /// <summary>
    /// Session-only policy (lowest precedence).
    /// </summary>
    Session = 4,

    /// <summary>
    /// Default/built-in policy.
    /// </summary>
    Default = 100
}

/// <summary>
/// Categories of policy enforcement.
/// </summary>
public enum PolicyCategory
{
    /// <summary>
    /// Tool usage policies.
    /// </summary>
    Tools,

    /// <summary>
    /// Plugin loading and execution policies.
    /// </summary>
    Plugins,

    /// <summary>
    /// Memory and retention policies.
    /// </summary>
    Memory,

    /// <summary>
    /// Connectivity mode policies.
    /// </summary>
    Connectivity,

    /// <summary>
    /// Update and release channel policies.
    /// </summary>
    Updates
}

/// <summary>
/// Result of a policy evaluation.
/// Always includes an explanation for the operator.
/// </summary>
public sealed record PolicyEvaluationResult
{
    /// <summary>
    /// The decision: Allow, Deny, AllowWithApproval, or AllowWithConstraints.
    /// </summary>
    public required PolicyDecision Decision { get; init; }

    /// <summary>
    /// Human-readable explanation of why this decision was made.
    /// Never null or empty - policy is always explainable.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The source that determined this decision.
    /// </summary>
    public required PolicySource Source { get; init; }

    /// <summary>
    /// Path to the policy file that contained the rule.
    /// Null for built-in defaults.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// The specific rule ID that matched, if applicable.
    /// </summary>
    public string? RuleId { get; init; }

    /// <summary>
    /// Constraints to apply if Decision is AllowWithConstraints.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Constraints { get; init; }

    /// <summary>
    /// When this decision was made.
    /// </summary>
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates an Allow result.
    /// </summary>
    public static PolicyEvaluationResult Allow(string reason, PolicySource source, string? sourcePath = null) =>
        new()
        {
            Decision = PolicyDecision.Allow,
            Reason = reason,
            Source = source,
            SourcePath = sourcePath
        };

    /// <summary>
    /// Creates a Deny result.
    /// </summary>
    public static PolicyEvaluationResult Deny(string reason, PolicySource source, string? sourcePath = null, string? ruleId = null) =>
        new()
        {
            Decision = PolicyDecision.Deny,
            Reason = reason,
            Source = source,
            SourcePath = sourcePath,
            RuleId = ruleId
        };

    /// <summary>
    /// Creates an AllowWithApproval result.
    /// </summary>
    public static PolicyEvaluationResult RequireApproval(string reason, PolicySource source, string? sourcePath = null) =>
        new()
        {
            Decision = PolicyDecision.AllowWithApproval,
            Reason = reason,
            Source = source,
            SourcePath = sourcePath
        };

    /// <summary>
    /// Creates an AllowWithConstraints result.
    /// </summary>
    public static PolicyEvaluationResult AllowConstrained(
        string reason,
        PolicySource source,
        IReadOnlyDictionary<string, object> constraints,
        string? sourcePath = null) =>
        new()
        {
            Decision = PolicyDecision.AllowWithConstraints,
            Reason = reason,
            Source = source,
            SourcePath = sourcePath,
            Constraints = constraints
        };
}

/// <summary>
/// Record of a policy decision for audit purposes.
/// </summary>
public sealed record PolicyAuditEntry
{
    /// <summary>
    /// When the decision was made.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// What category of policy was evaluated.
    /// </summary>
    public required PolicyCategory Category { get; init; }

    /// <summary>
    /// The subject being evaluated (tool ID, plugin ID, etc.).
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// The action being requested.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// The decision that was made.
    /// </summary>
    public required PolicyDecision Decision { get; init; }

    /// <summary>
    /// Human-readable explanation.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The policy source that determined the decision.
    /// </summary>
    public required PolicySource Source { get; init; }

    /// <summary>
    /// Path to the policy file, if applicable.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Rule ID that matched, if applicable.
    /// </summary>
    public string? RuleId { get; init; }
}

/// <summary>
/// Information about the effective policy state.
/// Used for diagnostics and the policy viewer UI.
/// </summary>
public sealed record EffectivePolicyInfo
{
    /// <summary>
    /// Whether organization policy is active.
    /// </summary>
    public bool HasOrgPolicy { get; init; }

    /// <summary>
    /// Whether team policy is active.
    /// </summary>
    public bool HasTeamPolicy { get; init; }

    /// <summary>
    /// Whether user policy is active.
    /// </summary>
    public bool HasUserPolicy { get; init; }

    /// <summary>
    /// Whether the policy is locked (org prevents user overrides).
    /// </summary>
    public bool IsLocked { get; init; }

    /// <summary>
    /// Path to the org policy file.
    /// </summary>
    public string? OrgPolicyPath { get; init; }

    /// <summary>
    /// Path to the team policy file.
    /// </summary>
    public string? TeamPolicyPath { get; init; }

    /// <summary>
    /// Path to the user policy file.
    /// </summary>
    public string? UserPolicyPath { get; init; }

    /// <summary>
    /// When policies were last loaded.
    /// </summary>
    public DateTimeOffset LoadedAt { get; init; }

    /// <summary>
    /// Any errors encountered loading policies.
    /// </summary>
    public IReadOnlyList<string> LoadErrors { get; init; } = [];

    /// <summary>
    /// Summary for UI display.
    /// </summary>
    public string Summary => (HasOrgPolicy, IsLocked) switch
    {
        (true, true) => "Managed by Organization (locked)",
        (true, false) => "Organization policy active",
        (false, _) when HasTeamPolicy => "Team policy active",
        (false, _) when HasUserPolicy => "User policy active",
        _ => "Default policy"
    };
}

/// <summary>
/// Paths for policy files based on platform.
/// </summary>
public static class PolicyPaths
{
    /// <summary>
    /// Gets the organization policy path.
    /// </summary>
    public static string GetOrgPolicyPath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InControl", "policy.json");

        if (OperatingSystem.IsMacOS())
            return "/Library/Application Support/InControl/policy.json";

        // Linux
        return "/etc/incontrol/policy.json";
    }

    /// <summary>
    /// Gets the team policy path.
    /// </summary>
    public static string GetTeamPolicyPath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InControl", "team-policy.json");

        if (OperatingSystem.IsMacOS())
            return "/Library/Application Support/InControl/team-policy.json";

        return "/etc/incontrol/team-policy.json";
    }

    /// <summary>
    /// Gets the user policy path.
    /// </summary>
    public static string GetUserPolicyPath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InControl", "user-policy.json");

        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "InControl", "user-policy.json");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "incontrol", "user-policy.json");
    }
}
