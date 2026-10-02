using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class AssistantStateTests
{
    [Theory]
    [InlineData(AssistantState.Idle)]
    [InlineData(AssistantState.Listening)]
    [InlineData(AssistantState.Reasoning)]
    [InlineData(AssistantState.Proposing)]
    [InlineData(AssistantState.AwaitingApproval)]
    [InlineData(AssistantState.Acting)]
    [InlineData(AssistantState.Blocked)]
    public void AssistantState_AllValuesAreDefined(AssistantState state)
    {
        Enum.IsDefined(state).Should().BeTrue();
    }

    [Fact]
    public void GetStateDescription_ReturnsReadableStrings()
    {
        AssistantStateMachine.GetStateDescription(AssistantState.Idle).Should().Be("Ready");
        AssistantStateMachine.GetStateDescription(AssistantState.Listening).Should().Be("Listening...");
        AssistantStateMachine.GetStateDescription(AssistantState.Reasoning).Should().Be("Thinking...");
        AssistantStateMachine.GetStateDescription(AssistantState.Proposing).Should().Be("Suggesting action...");
        AssistantStateMachine.GetStateDescription(AssistantState.AwaitingApproval).Should().Be("Waiting for approval");
        AssistantStateMachine.GetStateDescription(AssistantState.Acting).Should().Be("Working...");
        AssistantStateMachine.GetStateDescription(AssistantState.Blocked).Should().Be("Blocked");
    }
}

public class AssistantStateMachineTests
{
    [Fact]
    public void InitialState_IsIdle()
    {
        var machine = new AssistantStateMachine();

        machine.CurrentState.Should().Be(AssistantState.Idle);
    }

    [Fact]
    public void TryTransition_FromIdleToListening_Succeeds()
    {
        var machine = new AssistantStateMachine();

        var result = machine.TryTransition(AssistantState.Listening, "User started typing");

        result.Should().BeTrue();
        machine.CurrentState.Should().Be(AssistantState.Listening);
    }

    [Fact]
    public void TryTransition_FromIdleToActing_Fails()
    {
        var machine = new AssistantStateMachine();

        var result = machine.TryTransition(AssistantState.Acting, "Invalid direct jump");

        result.Should().BeFalse();
        machine.CurrentState.Should().Be(AssistantState.Idle);
    }

    [Fact]
    public void TryTransition_SameState_Succeeds()
    {
        var machine = new AssistantStateMachine();

        var result = machine.TryTransition(AssistantState.Idle, "No-op");

        result.Should().BeTrue();
        machine.CurrentState.Should().Be(AssistantState.Idle);
    }

    [Fact]
    public void TryTransition_RecordsHistory()
    {
        var machine = new AssistantStateMachine();

        machine.TryTransition(AssistantState.Listening, "Step 1");
        machine.TryTransition(AssistantState.Reasoning, "Step 2");

        machine.History.Should().HaveCount(2);
        machine.History[0].From.Should().Be(AssistantState.Idle);
        machine.History[0].To.Should().Be(AssistantState.Listening);
        machine.History[1].From.Should().Be(AssistantState.Listening);
        machine.History[1].To.Should().Be(AssistantState.Reasoning);
    }

    [Fact]
    public void ForceTransition_BypassesValidation()
    {
        var machine = new AssistantStateMachine();

        machine.ForceTransition(AssistantState.Acting, "Emergency recovery");

        machine.CurrentState.Should().Be(AssistantState.Acting);
        machine.History.Should().Contain(t => t.Reason!.Contains("[FORCED]"));
    }

    [Fact]
    public void Reset_ReturnsToIdle()
    {
        var machine = new AssistantStateMachine();
        machine.TryTransition(AssistantState.Listening);
        machine.TryTransition(AssistantState.Reasoning);

        machine.Reset();

        machine.CurrentState.Should().Be(AssistantState.Idle);
    }

    [Fact]
    public void Reset_WhenAlreadyIdle_DoesNotAddHistory()
    {
        var machine = new AssistantStateMachine();
        var initialHistoryCount = machine.History.Count;

        machine.Reset();

        machine.History.Should().HaveCount(initialHistoryCount);
    }

    [Fact]
    public async Task StateChanged_EventFires()
    {
        var machine = new AssistantStateMachine();
        StateChangedEventArgs? capturedArgs = null;
        machine.StateChanged += (_, args) => capturedArgs = args;

        machine.TryTransition(AssistantState.Listening, "Test");

        // Wait for async event
        await Task.Delay(100);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.PreviousState.Should().Be(AssistantState.Idle);
        capturedArgs.NewState.Should().Be(AssistantState.Listening);
    }
}

public class StateTransitionValidationTests
{
    // Valid transitions from Idle
    [Fact]
    public void Idle_CanTransitionToListening()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Idle, AssistantState.Listening)
            .Should().BeTrue();
    }

    [Fact]
    public void Idle_CanTransitionToBlocked()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Idle, AssistantState.Blocked)
            .Should().BeTrue();
    }

    [Fact]
    public void Idle_CannotTransitionToActing()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Idle, AssistantState.Acting)
            .Should().BeFalse();
    }

    [Fact]
    public void Idle_CannotTransitionToAwaitingApproval()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Idle, AssistantState.AwaitingApproval)
            .Should().BeFalse();
    }

    // Valid transitions from Listening
    [Fact]
    public void Listening_CanTransitionToReasoning()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Listening, AssistantState.Reasoning)
            .Should().BeTrue();
    }

    [Fact]
    public void Listening_CanTransitionToIdle()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Listening, AssistantState.Idle)
            .Should().BeTrue();
    }

    // Valid transitions from Reasoning
    [Fact]
    public void Reasoning_CanTransitionToProposing()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Reasoning, AssistantState.Proposing)
            .Should().BeTrue();
    }

    [Fact]
    public void Reasoning_CanTransitionToActing()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Reasoning, AssistantState.Acting)
            .Should().BeTrue();
    }

    [Fact]
    public void Reasoning_CanTransitionToIdle()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Reasoning, AssistantState.Idle)
            .Should().BeTrue();
    }

    // Valid transitions from Proposing
    [Fact]
    public void Proposing_CanTransitionToAwaitingApproval()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Proposing, AssistantState.AwaitingApproval)
            .Should().BeTrue();
    }

    // Valid transitions from AwaitingApproval
    [Fact]
    public void AwaitingApproval_CanTransitionToActing()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.AwaitingApproval, AssistantState.Acting)
            .Should().BeTrue();
    }

    [Fact]
    public void AwaitingApproval_CanTransitionToIdle()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.AwaitingApproval, AssistantState.Idle)
            .Should().BeTrue();
    }

    // Valid transitions from Acting
    [Fact]
    public void Acting_CanTransitionToIdle()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Acting, AssistantState.Idle)
            .Should().BeTrue();
    }

    [Fact]
    public void Acting_CanTransitionToBlocked()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Acting, AssistantState.Blocked)
            .Should().BeTrue();
    }

    // Valid transitions from Blocked
    [Fact]
    public void Blocked_CanTransitionToIdle()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Blocked, AssistantState.Idle)
            .Should().BeTrue();
    }

    [Fact]
    public void Blocked_CannotTransitionToActing()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Blocked, AssistantState.Acting)
            .Should().BeFalse();
    }

    // Invalid transitions
    [Fact]
    public void Acting_CannotTransitionDirectlyToProposing()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.Acting, AssistantState.Proposing)
            .Should().BeFalse();
    }

    [Fact]
    public void AwaitingApproval_CannotTransitionToReasoning()
    {
        AssistantStateMachine.IsValidTransition(AssistantState.AwaitingApproval, AssistantState.Reasoning)
            .Should().BeFalse();
    }
}

public class StateTransitionRecordTests
{
    [Fact]
    public void StateTransition_RecordsAllProperties()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var transition = new StateTransition(
            From: AssistantState.Idle,
            To: AssistantState.Listening,
            Reason: "User input",
            Timestamp: timestamp
        );

        transition.From.Should().Be(AssistantState.Idle);
        transition.To.Should().Be(AssistantState.Listening);
        transition.Reason.Should().Be("User input");
        transition.Timestamp.Should().Be(timestamp);
    }
}
