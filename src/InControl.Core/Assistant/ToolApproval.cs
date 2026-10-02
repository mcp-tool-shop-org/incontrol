using InControl.Core.Errors;

namespace InControl.Core.Assistant;

/// <summary>
/// Manages tool proposal and approval workflow.
/// The assistant must propose tool actions; users must approve before execution.
/// </summary>
public sealed class ToolApprovalManager
{
    private readonly ToolRegistry _registry;
    private readonly List<ToolProposal> _pendingProposals = [];
    private readonly Dictionary<string, bool> _rememberedDecisions = [];
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when a tool action is proposed.
    /// </summary>
    public event EventHandler<ToolProposalEventArgs>? ProposalCreated;

    /// <summary>
    /// Event raised when a proposal decision is made.
    /// </summary>
    public event EventHandler<ProposalDecisionEventArgs>? ProposalDecided;

    public ToolApprovalManager(ToolRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Pending proposals awaiting user decision.
    /// </summary>
    public IReadOnlyList<ToolProposal> PendingProposals
    {
        get
        {
            lock (_lock)
            {
                return _pendingProposals.ToList();
            }
        }
    }

    /// <summary>
    /// Proposes a tool action for user approval.
    /// Throws <see cref="InvalidOperationException"/> if the tool is not registered.
    /// For a non-throwing alternative, use <see cref="TryProposeAction"/>.
    /// </summary>
    public ToolProposal ProposeAction(
        string toolId,
        IReadOnlyDictionary<string, object?> parameters,
        string rationale,
        string expectedOutcome,
        string? potentialRisks = null)
    {
        var tool = _registry.GetTool(toolId);
        if (tool == null)
        {
            throw new InvalidOperationException($"Tool not found: {toolId}");
        }

        // Check if we have a remembered "always allow" decision
        lock (_lock)
        {
            if (_rememberedDecisions.TryGetValue(toolId, out var remembered) && remembered)
            {
                // Auto-approved based on remembered decision
                var autoProposal = new ToolProposal(
                    Id: Guid.NewGuid(),
                    ToolId: toolId,
                    ToolName: tool.Name,
                    Parameters: parameters,
                    Rationale: rationale,
                    ExpectedOutcome: expectedOutcome,
                    PotentialRisks: potentialRisks,
                    RiskLevel: tool.RiskLevel,
                    ProposedAt: DateTimeOffset.UtcNow,
                    Status: ProposalStatus.AutoApproved
                );

                return autoProposal;
            }
        }

        var proposal = new ToolProposal(
            Id: Guid.NewGuid(),
            ToolId: toolId,
            ToolName: tool.Name,
            Parameters: parameters,
            Rationale: rationale,
            ExpectedOutcome: expectedOutcome,
            PotentialRisks: potentialRisks,
            RiskLevel: tool.RiskLevel,
            ProposedAt: DateTimeOffset.UtcNow,
            Status: ProposalStatus.Pending
        );

        lock (_lock)
        {
            _pendingProposals.Add(proposal);
        }

        ProposalCreated?.Invoke(this, new ToolProposalEventArgs(proposal));

        return proposal;
    }

    /// <summary>
    /// Proposes a tool action for user approval.
    /// Returns a <see cref="Result{T}"/> instead of throwing when the tool is not found.
    /// </summary>
    public Result<ToolProposal> TryProposeAction(
        string toolId,
        IReadOnlyDictionary<string, object?> parameters,
        string rationale,
        string expectedOutcome,
        string? potentialRisks = null)
    {
        var tool = _registry.GetTool(toolId);
        if (tool == null)
        {
            return InControlError.Create(ErrorCode.ToolNotFound, $"Tool not found: {toolId}");
        }

        return ProposeAction(toolId, parameters, rationale, expectedOutcome, potentialRisks);
    }

    /// <summary>
    /// Approves a proposal and executes the tool.
    /// </summary>
    public async Task<ToolResult> ApproveAsync(
        Guid proposalId,
        bool rememberDecision = false,
        CancellationToken ct = default)
    {
        ToolProposal? proposal;
        lock (_lock)
        {
            proposal = _pendingProposals.FirstOrDefault(p => p.Id == proposalId);
            if (proposal == null)
            {
                return ToolResult.Failed(
                    Errors.InControlError.Create(Errors.ErrorCode.InvalidOperation, "Proposal not found"),
                    TimeSpan.Zero
                );
            }

            _pendingProposals.Remove(proposal);

            if (rememberDecision)
            {
                _rememberedDecisions[proposal.ToolId] = true;
                _registry.SetPermission(proposal.ToolId, ToolPermission.AlwaysAllow);
            }
        }

        var result = await _registry.ExecuteAsync(proposal.ToolId, proposal.Parameters, ct);

        ProposalDecided?.Invoke(this, new ProposalDecisionEventArgs(
            proposal,
            ProposalDecision.Approved,
            result,
            rememberDecision
        ));

        return result;
    }

    /// <summary>
    /// Approves a proposal with modified parameters.
    /// </summary>
    public async Task<ToolResult> ApproveWithModificationsAsync(
        Guid proposalId,
        IReadOnlyDictionary<string, object?> modifiedParameters,
        CancellationToken ct = default)
    {
        ToolProposal? proposal;
        lock (_lock)
        {
            proposal = _pendingProposals.FirstOrDefault(p => p.Id == proposalId);
            if (proposal == null)
            {
                return ToolResult.Failed(
                    Errors.InControlError.Create(Errors.ErrorCode.InvalidOperation, "Proposal not found"),
                    TimeSpan.Zero
                );
            }

            _pendingProposals.Remove(proposal);
        }

        var result = await _registry.ExecuteAsync(proposal.ToolId, modifiedParameters, ct);

        ProposalDecided?.Invoke(this, new ProposalDecisionEventArgs(
            proposal,
            ProposalDecision.ApprovedWithModifications,
            result,
            false
        ));

        return result;
    }

    /// <summary>
    /// Denies a proposal.
    /// </summary>
    public bool Deny(Guid proposalId, string? reason = null)
    {
        ToolProposal? proposal;
        lock (_lock)
        {
            proposal = _pendingProposals.FirstOrDefault(p => p.Id == proposalId);
            if (proposal == null)
                return false;

            _pendingProposals.Remove(proposal);
        }

        ProposalDecided?.Invoke(this, new ProposalDecisionEventArgs(
            proposal,
            ProposalDecision.Denied,
            null,
            false,
            reason
        ));

        return true;
    }

    /// <summary>
    /// Clears all remembered decisions.
    /// </summary>
    public void ClearRememberedDecisions()
    {
        lock (_lock)
        {
            _rememberedDecisions.Clear();
        }
    }

    /// <summary>
    /// Checks if approval is needed for a tool.
    /// </summary>
    public bool NeedsApproval(string toolId)
    {
        lock (_lock)
        {
            if (_rememberedDecisions.TryGetValue(toolId, out var remembered) && remembered)
                return false;
        }

        return _registry.RequiresApproval(toolId);
    }
}

/// <summary>
/// A proposed tool action awaiting user decision.
/// </summary>
public sealed record ToolProposal(
    Guid Id,
    string ToolId,
    string ToolName,
    IReadOnlyDictionary<string, object?> Parameters,
    string Rationale,
    string ExpectedOutcome,
    string? PotentialRisks,
    ToolRiskLevel RiskLevel,
    DateTimeOffset ProposedAt,
    ProposalStatus Status
);

/// <summary>
/// Status of a tool proposal.
/// </summary>
public enum ProposalStatus
{
    Pending,
    Approved,
    ApprovedWithModifications,
    AutoApproved,
    Denied,
    Expired
}

/// <summary>
/// User's decision on a proposal.
/// </summary>
public enum ProposalDecision
{
    Approved,
    ApprovedWithModifications,
    Denied
}

/// <summary>
/// Event args for tool proposals.
/// </summary>
public sealed class ToolProposalEventArgs : EventArgs
{
    public ToolProposal Proposal { get; }

    public ToolProposalEventArgs(ToolProposal proposal)
    {
        Proposal = proposal;
    }
}

/// <summary>
/// Event args for proposal decisions.
/// </summary>
public sealed class ProposalDecisionEventArgs : EventArgs
{
    public ToolProposal Proposal { get; }
    public ProposalDecision Decision { get; }
    public ToolResult? Result { get; }
    public bool DecisionRemembered { get; }
    public string? DenialReason { get; }

    public ProposalDecisionEventArgs(
        ToolProposal proposal,
        ProposalDecision decision,
        ToolResult? result,
        bool decisionRemembered,
        string? denialReason = null)
    {
        Proposal = proposal;
        Decision = decision;
        Result = result;
        DecisionRemembered = decisionRemembered;
        DenialReason = denialReason;
    }
}
