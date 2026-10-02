using FluentAssertions;
using InControl.Core.UX;
using Xunit;

namespace InControl.Core.Tests.UX;

public class ExecutionStateTests
{
    [Theory]
    [InlineData(ExecutionState.Idle, "Idle")]
    [InlineData(ExecutionState.Initializing, "Initializing...")]
    [InlineData(ExecutionState.Running, "Running inference...")]
    [InlineData(ExecutionState.Complete, "Complete")]
    [InlineData(ExecutionState.Issue, "Issue")]
    public void ToDisplayText_ReturnsCorrectText(ExecutionState state, string expected)
    {
        state.ToDisplayText().Should().Be(expected);
    }

    [Theory]
    [InlineData(ExecutionState.Idle, "Idle")]
    [InlineData(ExecutionState.Initializing, "Loading")]
    [InlineData(ExecutionState.Running, "Running")]
    [InlineData(ExecutionState.Complete, "Done")]
    [InlineData(ExecutionState.Issue, "Issue")]
    public void ToCapsuleText_ReturnsShortText(ExecutionState state, string expected)
    {
        state.ToCapsuleText().Should().Be(expected);
    }

    [Theory]
    [InlineData(ExecutionState.Initializing, true)]
    [InlineData(ExecutionState.LoadingModel, true)]
    [InlineData(ExecutionState.Running, true)]
    [InlineData(ExecutionState.Streaming, true)]
    [InlineData(ExecutionState.Completing, true)]
    [InlineData(ExecutionState.Idle, false)]
    [InlineData(ExecutionState.Complete, false)]
    [InlineData(ExecutionState.Cancelled, false)]
    [InlineData(ExecutionState.Issue, false)]
    public void IsExecuting_IdentifiesActiveStates(ExecutionState state, bool expected)
    {
        state.IsExecuting().Should().Be(expected);
    }

    [Theory]
    [InlineData(ExecutionState.Idle, true)]
    [InlineData(ExecutionState.Complete, true)]
    [InlineData(ExecutionState.Cancelled, true)]
    [InlineData(ExecutionState.Issue, true)]
    [InlineData(ExecutionState.Running, false)]
    [InlineData(ExecutionState.Streaming, false)]
    public void AllowsInput_IdentifiesInputStates(ExecutionState state, bool expected)
    {
        state.AllowsInput().Should().Be(expected);
    }

    [Theory]
    [InlineData(ExecutionState.Running, true)]
    [InlineData(ExecutionState.Streaming, true)]
    [InlineData(ExecutionState.Idle, false)]
    [InlineData(ExecutionState.Complete, false)]
    public void CanCancel_MatchesIsExecuting(ExecutionState state, bool expected)
    {
        state.CanCancel().Should().Be(expected);
    }
}
