using FluentAssertions;
using InControl.Core.UX;
using InControl.ViewModels.Execution;
using Xunit;

namespace InControl.Core.Tests.Execution;

public class ExecutionTimerViewModelTests : IDisposable
{
    private readonly ExecutionTimerViewModel _vm;

    public ExecutionTimerViewModelTests()
    {
        _vm = new ExecutionTimerViewModel();
    }

    public void Dispose()
    {
        _vm.Dispose();
    }

    [Fact]
    public void State_DefaultsToIdle()
    {
        _vm.State.Should().Be(ExecutionState.Idle);
        _vm.IsExecuting.Should().BeFalse();
        _vm.CanCancel.Should().BeFalse();
        _vm.AllowsInput.Should().BeTrue();
    }

    [Fact]
    public void StateText_ReflectsState()
    {
        _vm.State = ExecutionState.Running;

        _vm.StateText.Should().Be("Running inference...");
    }

    [Fact]
    public void CapsuleText_ReflectsState()
    {
        _vm.State = ExecutionState.Streaming;

        _vm.CapsuleText.Should().Be("Running");
    }

    [Fact]
    public void Start_SetsInitialState()
    {
        _vm.Start(ExecutionState.LoadingModel);

        _vm.State.Should().Be(ExecutionState.LoadingModel);
        _vm.IsExecuting.Should().BeTrue();
    }

    [Fact]
    public void Start_ResetsElapsedTime()
    {
        _vm.Start();

        _vm.ElapsedTime.Should().BeCloseTo(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void TransitionTo_ChangesState()
    {
        _vm.Start(ExecutionState.Initializing);

        _vm.TransitionTo(ExecutionState.Running);

        _vm.State.Should().Be(ExecutionState.Running);
    }

    [Fact]
    public void TransitionTo_StopsTimer_WhenNotExecuting()
    {
        _vm.Start(ExecutionState.Running);

        _vm.TransitionTo(ExecutionState.Complete);

        _vm.IsExecuting.Should().BeFalse();
        _vm.ShowTimer.Should().BeFalse();
    }

    [Fact]
    public void Complete_SetsStateToComplete()
    {
        _vm.Start(ExecutionState.Running);

        _vm.Complete();

        _vm.State.Should().Be(ExecutionState.Complete);
    }

    [Fact]
    public void Cancel_SetsStateToCancelled()
    {
        _vm.Start(ExecutionState.Running);

        _vm.Cancel();

        _vm.State.Should().Be(ExecutionState.Cancelled);
    }

    [Fact]
    public void SetIssue_SetsStateToIssue()
    {
        _vm.Start(ExecutionState.Running);

        _vm.SetIssue();

        _vm.State.Should().Be(ExecutionState.Issue);
    }

    [Fact]
    public void Reset_ReturnsToIdle()
    {
        _vm.Start(ExecutionState.Running);

        _vm.Reset();

        _vm.State.Should().Be(ExecutionState.Idle);
        _vm.ElapsedTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ElapsedTimeText_FormatsCorrectly()
    {
        // Less than 1 second
        _vm.Start();
        _vm.ElapsedTimeText.Should().Be("< 1s");
    }

    [Fact]
    public void ShowTimer_TrueWhenExecuting()
    {
        _vm.State = ExecutionState.Idle;
        _vm.ShowTimer.Should().BeFalse();

        _vm.State = ExecutionState.Running;
        _vm.ShowTimer.Should().BeTrue();

        _vm.State = ExecutionState.Complete;
        _vm.ShowTimer.Should().BeFalse();
    }

    [Fact]
    public void CanCancel_TrueWhenExecuting()
    {
        _vm.State = ExecutionState.Idle;
        _vm.CanCancel.Should().BeFalse();

        _vm.State = ExecutionState.Running;
        _vm.CanCancel.Should().BeTrue();

        _vm.State = ExecutionState.Streaming;
        _vm.CanCancel.Should().BeTrue();
    }

    [Fact]
    public void PropertyChanged_RaisedForStateChanges()
    {
        var changed = new List<string>();
        _vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        _vm.State = ExecutionState.Running;

        changed.Should().Contain(nameof(_vm.State));
        changed.Should().Contain(nameof(_vm.StateText));
        changed.Should().Contain(nameof(_vm.IsExecuting));
        changed.Should().Contain(nameof(_vm.CanCancel));
    }
}

public class ExecutionTimerElapsedTimeTests
{
    [Theory]
    [InlineData(0.5, "< 1s")]
    [InlineData(5, "5s")]
    [InlineData(30, "30s")]
    [InlineData(60, "1m 0s")]
    [InlineData(90, "1m 30s")]
    [InlineData(125, "2m 5s")]
    public void ElapsedTimeText_FormatsVariousValues(double seconds, string expected)
    {
        using var vm = new ExecutionTimerViewModel();

        // Use reflection to set the elapsed time for testing
        var field = typeof(ExecutionTimerViewModel).GetField("_elapsedTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(vm, TimeSpan.FromSeconds(seconds));

        vm.ElapsedTimeText.Should().Be(expected);
    }
}
