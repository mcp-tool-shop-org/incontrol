using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class ToolApprovalManagerTests
{
    private readonly ToolRegistry _registry;
    private readonly ToolApprovalManager _manager;

    public ToolApprovalManagerTests()
    {
        _registry = new ToolRegistry();
        _manager = new ToolApprovalManager(_registry);

        // Register test tools
        _registry.Register(new FakeTool("read-file", ToolRiskLevel.Low));
        _registry.Register(new FakeTool("write-file", ToolRiskLevel.Medium));
        _registry.Register(new FakeTool("execute-command", ToolRiskLevel.High));
        _registry.Register(new FakeTool("system-modify", ToolRiskLevel.Critical));
    }

    [Fact]
    public void ProposeAction_CreatesProposal()
    {
        var proposal = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?> { ["path"] = "/test.txt" },
            "Need to read configuration",
            "Will return file contents");

        proposal.Should().NotBeNull();
        proposal.ToolId.Should().Be("read-file");
        proposal.Rationale.Should().Be("Need to read configuration");
        proposal.ExpectedOutcome.Should().Be("Will return file contents");
        proposal.Status.Should().Be(ProposalStatus.Pending);
    }

    [Fact]
    public void ProposeAction_AddsToPendingList()
    {
        var proposal = _manager.ProposeAction(
            "write-file",
            new Dictionary<string, object?>(),
            "Test rationale",
            "Test outcome");

        _manager.PendingProposals.Should().Contain(p => p.Id == proposal.Id);
    }

    [Fact]
    public void ProposeAction_RaisesProposalCreatedEvent()
    {
        ToolProposalEventArgs? capturedArgs = null;
        _manager.ProposalCreated += (_, args) => capturedArgs = args;

        var proposal = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Rationale",
            "Outcome");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Proposal.Id.Should().Be(proposal.Id);
    }

    [Fact]
    public void ProposeAction_IncludesRiskLevel()
    {
        var proposal = _manager.ProposeAction(
            "execute-command",
            new Dictionary<string, object?>(),
            "Need to run command",
            "Will execute the command");

        proposal.RiskLevel.Should().Be(ToolRiskLevel.High);
    }

    [Fact]
    public void ProposeAction_IncludesPotentialRisks()
    {
        var proposal = _manager.ProposeAction(
            "system-modify",
            new Dictionary<string, object?>(),
            "Need to modify system",
            "Will change settings",
            "Could affect system stability");

        proposal.PotentialRisks.Should().Be("Could affect system stability");
    }

    [Fact]
    public void ProposeAction_ThrowsWhenToolNotFound()
    {
        var act = () => _manager.ProposeAction(
            "nonexistent-tool",
            new Dictionary<string, object?>(),
            "Rationale",
            "Outcome");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tool not found*");
    }

    [Fact]
    public async Task ApproveAsync_ExecutesTool()
    {
        var proposal = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?> { ["input"] = "test" },
            "Test",
            "Test");

        var result = await _manager.ApproveAsync(proposal.Id);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("executed");
    }

    [Fact]
    public async Task ApproveAsync_RemovesFromPending()
    {
        var proposal = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        await _manager.ApproveAsync(proposal.Id);

        _manager.PendingProposals.Should().NotContain(p => p.Id == proposal.Id);
    }

    [Fact]
    public async Task ApproveAsync_RaisesProposalDecidedEvent()
    {
        ProposalDecisionEventArgs? capturedArgs = null;
        _manager.ProposalDecided += (_, args) => capturedArgs = args;

        var proposal = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        await _manager.ApproveAsync(proposal.Id);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Decision.Should().Be(ProposalDecision.Approved);
        capturedArgs.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveAsync_ReturnsError_WhenProposalNotFound()
    {
        var result = await _manager.ApproveAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveAsync_WithRememberDecision_SetsAlwaysAllow()
    {
        var proposal = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        await _manager.ApproveAsync(proposal.Id, rememberDecision: true);

        _registry.GetPermission("read-file").Should().Be(ToolPermission.AlwaysAllow);
    }

    [Fact]
    public async Task ApproveAsync_WithRememberDecision_AutoApprovesSubsequentProposals()
    {
        var proposal1 = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Test 1",
            "Test");

        await _manager.ApproveAsync(proposal1.Id, rememberDecision: true);

        var proposal2 = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Test 2",
            "Test");

        proposal2.Status.Should().Be(ProposalStatus.AutoApproved);
        _manager.PendingProposals.Should().NotContain(p => p.Id == proposal2.Id);
    }

    [Fact]
    public async Task ApproveWithModificationsAsync_ExecutesWithModifiedParameters()
    {
        var proposal = _manager.ProposeAction(
            "write-file",
            new Dictionary<string, object?> { ["path"] = "/original.txt" },
            "Test",
            "Test");

        var modifiedParams = new Dictionary<string, object?> { ["path"] = "/modified.txt" };
        var result = await _manager.ApproveWithModificationsAsync(proposal.Id, modifiedParams);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveWithModificationsAsync_RaisesCorrectDecision()
    {
        ProposalDecisionEventArgs? capturedArgs = null;
        _manager.ProposalDecided += (_, args) => capturedArgs = args;

        var proposal = _manager.ProposeAction(
            "write-file",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        await _manager.ApproveWithModificationsAsync(
            proposal.Id,
            new Dictionary<string, object?>());

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Decision.Should().Be(ProposalDecision.ApprovedWithModifications);
    }

    [Fact]
    public void Deny_RemovesProposal()
    {
        var proposal = _manager.ProposeAction(
            "execute-command",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        var result = _manager.Deny(proposal.Id);

        result.Should().BeTrue();
        _manager.PendingProposals.Should().NotContain(p => p.Id == proposal.Id);
    }

    [Fact]
    public void Deny_ReturnsFalse_WhenProposalNotFound()
    {
        var result = _manager.Deny(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void Deny_RaisesProposalDecidedEvent()
    {
        ProposalDecisionEventArgs? capturedArgs = null;
        _manager.ProposalDecided += (_, args) => capturedArgs = args;

        var proposal = _manager.ProposeAction(
            "system-modify",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        _manager.Deny(proposal.Id, "Too risky");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Decision.Should().Be(ProposalDecision.Denied);
        capturedArgs.DenialReason.Should().Be("Too risky");
    }

    [Fact]
    public async Task ClearRememberedDecisions_ResetsAutoApproval()
    {
        var proposal = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        await _manager.ApproveAsync(proposal.Id, rememberDecision: true);
        _manager.ClearRememberedDecisions();

        var proposal2 = _manager.ProposeAction(
            "read-file",
            new Dictionary<string, object?>(),
            "Test 2",
            "Test");

        proposal2.Status.Should().Be(ProposalStatus.Pending);
    }

    [Fact]
    public void NeedsApproval_ReturnsTrue_ForHighRiskTool()
    {
        var result = _manager.NeedsApproval("execute-command");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NeedsApproval_ReturnsFalse_AfterRememberDecision()
    {
        var proposal = _manager.ProposeAction(
            "execute-command",
            new Dictionary<string, object?>(),
            "Test",
            "Test");

        await _manager.ApproveAsync(proposal.Id, rememberDecision: true);

        var result = _manager.NeedsApproval("execute-command");

        result.Should().BeFalse();
    }

    [Fact]
    public void PendingProposals_IsThreadSafe()
    {
        var proposals = new List<ToolProposal>();

        Parallel.For(0, 10, i =>
        {
            var proposal = _manager.ProposeAction(
                "read-file",
                new Dictionary<string, object?> { ["index"] = i },
                $"Test {i}",
                "Test");
            lock (proposals)
            {
                proposals.Add(proposal);
            }
        });

        _manager.PendingProposals.Count.Should().Be(10);
    }

    private sealed class FakeTool : IAssistantTool
    {
        public FakeTool(string id, ToolRiskLevel riskLevel = ToolRiskLevel.Low)
        {
            Id = id;
            RiskLevel = riskLevel;
        }

        public string Id { get; }
        public string Name => $"Fake Tool: {Id}";
        public string Description => "A fake tool for testing";
        public ToolRiskLevel RiskLevel { get; }
        public bool IsReadOnly => RiskLevel == ToolRiskLevel.Low;
        public bool RequiresNetwork => false;
        public IReadOnlyList<ToolParameter> Parameters => [];

        public Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct)
        {
            return Task.FromResult(ToolResult.Succeeded($"Tool {Id} executed", TimeSpan.FromMilliseconds(10)));
        }
    }
}

public class ToolProposalTests
{
    [Fact]
    public void ToolProposal_RecordsAllProperties()
    {
        var id = Guid.NewGuid();
        var parameters = new Dictionary<string, object?> { ["key"] = "value" };
        var proposedAt = DateTimeOffset.UtcNow;

        var proposal = new ToolProposal(
            Id: id,
            ToolId: "test-tool",
            ToolName: "Test Tool",
            Parameters: parameters,
            Rationale: "Test rationale",
            ExpectedOutcome: "Test outcome",
            PotentialRisks: "Test risks",
            RiskLevel: ToolRiskLevel.Medium,
            ProposedAt: proposedAt,
            Status: ProposalStatus.Pending
        );

        proposal.Id.Should().Be(id);
        proposal.ToolId.Should().Be("test-tool");
        proposal.ToolName.Should().Be("Test Tool");
        proposal.Parameters.Should().ContainKey("key");
        proposal.Rationale.Should().Be("Test rationale");
        proposal.ExpectedOutcome.Should().Be("Test outcome");
        proposal.PotentialRisks.Should().Be("Test risks");
        proposal.RiskLevel.Should().Be(ToolRiskLevel.Medium);
        proposal.ProposedAt.Should().Be(proposedAt);
        proposal.Status.Should().Be(ProposalStatus.Pending);
    }
}

public class ProposalStatusTests
{
    [Theory]
    [InlineData(ProposalStatus.Pending)]
    [InlineData(ProposalStatus.Approved)]
    [InlineData(ProposalStatus.ApprovedWithModifications)]
    [InlineData(ProposalStatus.AutoApproved)]
    [InlineData(ProposalStatus.Denied)]
    [InlineData(ProposalStatus.Expired)]
    public void ProposalStatus_AllValuesAreDefined(ProposalStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }
}

public class ProposalDecisionTests
{
    [Theory]
    [InlineData(ProposalDecision.Approved)]
    [InlineData(ProposalDecision.ApprovedWithModifications)]
    [InlineData(ProposalDecision.Denied)]
    public void ProposalDecision_AllValuesAreDefined(ProposalDecision decision)
    {
        Enum.IsDefined(decision).Should().BeTrue();
    }
}
