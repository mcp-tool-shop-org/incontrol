using InControl.Core.Assistant;
using InControl.Core.Errors;

namespace InControl.Core.Policy;

/// <summary>
/// Wraps a ToolRegistry with policy enforcement.
/// All tool executions are evaluated against the active policy.
/// </summary>
public sealed class PolicyGovernedToolRegistry
{
    private readonly ToolRegistry _innerRegistry;
    private readonly PolicyEngine _policyEngine;
    private readonly object _lock = new();
    private readonly Dictionary<string, (PolicyDecision Decision, DateTimeOffset GrantedAt)> _sessionGrants = [];

    /// <summary>
    /// Event raised when a tool is blocked by policy.
    /// </summary>
    public event EventHandler<ToolBlockedEventArgs>? ToolBlocked;

    /// <summary>
    /// Event raised when approval is required for a tool.
    /// </summary>
    public event EventHandler<ToolApprovalRequiredEventArgs>? ApprovalRequired;

    public PolicyGovernedToolRegistry(ToolRegistry innerRegistry, PolicyEngine policyEngine)
    {
        _innerRegistry = innerRegistry ?? throw new ArgumentNullException(nameof(innerRegistry));
        _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
    }

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    public IReadOnlyList<IAssistantTool> Tools => _innerRegistry.Tools;

    /// <summary>
    /// Gets the policy engine.
    /// </summary>
    public PolicyEngine PolicyEngine => _policyEngine;

    /// <summary>
    /// Registers a tool.
    /// </summary>
    public void Register(IAssistantTool tool) => _innerRegistry.Register(tool);

    /// <summary>
    /// Unregisters a tool.
    /// </summary>
    public bool Unregister(string toolId) => _innerRegistry.Unregister(toolId);

    /// <summary>
    /// Gets a tool by ID.
    /// </summary>
    public IAssistantTool? GetTool(string toolId) => _innerRegistry.GetTool(toolId);

    /// <summary>
    /// Checks if a tool can be executed under current policy.
    /// </summary>
    public ToolPolicyCheck CheckToolPolicy(string toolId)
    {
        var tool = _innerRegistry.GetTool(toolId);
        if (tool == null)
        {
            return new ToolPolicyCheck(
                CanExecute: false,
                Decision: PolicyDecision.Deny,
                Reason: $"Tool not found: {toolId}",
                Source: PolicySource.Default,
                RequiresApproval: false);
        }

        var policyResult = _policyEngine.EvaluateTool(toolId, "execute");

        // Check for session-level grants
        lock (_lock)
        {
            if (_sessionGrants.TryGetValue(toolId, out var grant))
            {
                // Session grant allows execution if policy requires approval
                if (policyResult.Decision == PolicyDecision.AllowWithApproval)
                {
                    return new ToolPolicyCheck(
                        CanExecute: true,
                        Decision: PolicyDecision.Allow,
                        Reason: $"Approved for this session at {grant.GrantedAt:HH:mm:ss}",
                        Source: PolicySource.Session,
                        RequiresApproval: false);
                }
            }
        }

        return policyResult.Decision switch
        {
            PolicyDecision.Allow => new ToolPolicyCheck(
                CanExecute: true,
                Decision: policyResult.Decision,
                Reason: policyResult.Reason,
                Source: policyResult.Source,
                RequiresApproval: false),

            PolicyDecision.AllowWithApproval => new ToolPolicyCheck(
                CanExecute: false, // Cannot execute until approved
                Decision: policyResult.Decision,
                Reason: policyResult.Reason,
                Source: policyResult.Source,
                RequiresApproval: true),

            PolicyDecision.AllowWithConstraints => new ToolPolicyCheck(
                CanExecute: true,
                Decision: policyResult.Decision,
                Reason: policyResult.Reason,
                Source: policyResult.Source,
                RequiresApproval: false,
                Constraints: policyResult.Constraints),

            PolicyDecision.Deny => new ToolPolicyCheck(
                CanExecute: false,
                Decision: policyResult.Decision,
                Reason: policyResult.Reason,
                Source: policyResult.Source,
                RequiresApproval: false,
                RuleId: policyResult.RuleId),

            _ => new ToolPolicyCheck(
                CanExecute: false,
                Decision: PolicyDecision.Deny,
                Reason: "Unknown policy decision",
                Source: policyResult.Source,
                RequiresApproval: false)
        };
    }

    /// <summary>
    /// Grants session-level approval for a tool.
    /// </summary>
    public void GrantSessionApproval(string toolId)
    {
        lock (_lock)
        {
            _sessionGrants[toolId] = (PolicyDecision.Allow, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// Revokes session-level approval for a tool.
    /// </summary>
    public void RevokeSessionApproval(string toolId)
    {
        lock (_lock)
        {
            _sessionGrants.Remove(toolId);
        }
    }

    /// <summary>
    /// Clears all session-level approvals.
    /// </summary>
    public void ClearSessionApprovals()
    {
        lock (_lock)
        {
            _sessionGrants.Clear();
        }
    }

    /// <summary>
    /// Gets all tools that have session approvals.
    /// </summary>
    public IReadOnlyList<string> GetSessionApprovedTools()
    {
        lock (_lock)
        {
            return _sessionGrants.Keys.ToList();
        }
    }

    /// <summary>
    /// Executes a tool with policy enforcement.
    /// </summary>
    public async Task<PolicyGovernedToolResult> ExecuteAsync(
        string toolId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        var policyCheck = CheckToolPolicy(toolId);

        // If tool requires approval but hasn't been granted
        if (policyCheck.RequiresApproval)
        {
            ApprovalRequired?.Invoke(this, new ToolApprovalRequiredEventArgs(toolId, policyCheck.Reason));
            return PolicyGovernedToolResult.RequiresApproval(toolId, policyCheck.Reason);
        }

        // If tool is blocked by policy
        if (!policyCheck.CanExecute)
        {
            ToolBlocked?.Invoke(this, new ToolBlockedEventArgs(toolId, policyCheck.Reason, policyCheck.Source));
            return PolicyGovernedToolResult.Blocked(toolId, policyCheck.Reason, policyCheck.Source);
        }

        // Execute the tool
        var result = await _innerRegistry.ExecuteAsync(toolId, parameters, ct);

        return PolicyGovernedToolResult.Executed(result, policyCheck.Constraints);
    }

    /// <summary>
    /// Gets tools filtered by policy - only returns tools that can potentially be used.
    /// </summary>
    public IReadOnlyList<ToolWithPolicyInfo> GetAvailableTools()
    {
        var result = new List<ToolWithPolicyInfo>();

        foreach (var tool in _innerRegistry.Tools)
        {
            var policyCheck = CheckToolPolicy(tool.Id);

            // Include if not permanently denied
            if (policyCheck.Decision != PolicyDecision.Deny)
            {
                result.Add(new ToolWithPolicyInfo(tool, policyCheck));
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the policy status for all registered tools.
    /// </summary>
    public IReadOnlyList<ToolWithPolicyInfo> GetAllToolsWithPolicy()
    {
        var result = new List<ToolWithPolicyInfo>();

        foreach (var tool in _innerRegistry.Tools)
        {
            var policyCheck = CheckToolPolicy(tool.Id);
            result.Add(new ToolWithPolicyInfo(tool, policyCheck));
        }

        return result;
    }
}

/// <summary>
/// Result of policy check for a tool.
/// </summary>
public sealed record ToolPolicyCheck(
    bool CanExecute,
    PolicyDecision Decision,
    string Reason,
    PolicySource Source,
    bool RequiresApproval,
    IReadOnlyDictionary<string, object>? Constraints = null,
    string? RuleId = null);

/// <summary>
/// Result of policy-governed tool execution.
/// </summary>
public sealed record PolicyGovernedToolResult
{
    public bool Success { get; init; }
    public bool WasBlocked { get; init; }
    public bool RequiredApproval { get; init; }
    public string? BlockReason { get; init; }
    public PolicySource? BlockSource { get; init; }
    public ToolResult? ToolResult { get; init; }
    public IReadOnlyDictionary<string, object>? AppliedConstraints { get; init; }

    public static PolicyGovernedToolResult Executed(ToolResult result, IReadOnlyDictionary<string, object>? constraints = null) => new()
    {
        Success = result.Success,
        ToolResult = result,
        AppliedConstraints = constraints
    };

    public static PolicyGovernedToolResult Blocked(string toolId, string reason, PolicySource source) => new()
    {
        Success = false,
        WasBlocked = true,
        BlockReason = reason,
        BlockSource = source
    };

    public static PolicyGovernedToolResult RequiresApproval(string toolId, string reason) => new()
    {
        Success = false,
        RequiredApproval = true,
        BlockReason = reason
    };
}

/// <summary>
/// A tool with its current policy information.
/// </summary>
public sealed record ToolWithPolicyInfo(IAssistantTool Tool, ToolPolicyCheck PolicyStatus);

/// <summary>
/// Event args for when a tool is blocked by policy.
/// </summary>
public sealed class ToolBlockedEventArgs : EventArgs
{
    public string ToolId { get; }
    public string Reason { get; }
    public PolicySource Source { get; }

    public ToolBlockedEventArgs(string toolId, string reason, PolicySource source)
    {
        ToolId = toolId;
        Reason = reason;
        Source = source;
    }
}

/// <summary>
/// Event args for when a tool requires approval.
/// </summary>
public sealed class ToolApprovalRequiredEventArgs : EventArgs
{
    public string ToolId { get; }
    public string Reason { get; }

    public ToolApprovalRequiredEventArgs(string toolId, string reason)
    {
        ToolId = toolId;
        Reason = reason;
    }
}

/// <summary>
/// Extensions for easy policy integration with tool registry.
/// </summary>
public static class ToolPolicyExtensions
{
    /// <summary>
    /// Creates a policy-governed wrapper around this registry.
    /// </summary>
    public static PolicyGovernedToolRegistry WithPolicyEnforcement(
        this ToolRegistry registry,
        PolicyEngine policyEngine)
    {
        return new PolicyGovernedToolRegistry(registry, policyEngine);
    }
}
