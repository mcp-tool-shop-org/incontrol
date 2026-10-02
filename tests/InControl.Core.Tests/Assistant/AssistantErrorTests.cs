using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class AssistantErrorHandlerTests
{
    private readonly AssistantErrorHandler _handler;

    public AssistantErrorHandlerTests()
    {
        _handler = new AssistantErrorHandler();
    }

    [Fact]
    public void HandleError_CreatesError()
    {
        var error = _handler.HandleError(
            AssistantErrorType.ToolFailure,
            "Tool crashed");

        error.Should().NotBeNull();
        error.Type.Should().Be(AssistantErrorType.ToolFailure);
        error.Message.Should().Be("Tool crashed");
    }

    [Fact]
    public void HandleError_AddsToHistory()
    {
        var error = _handler.HandleError(
            AssistantErrorType.NetworkError,
            "Connection failed");

        _handler.ErrorHistory.Should().Contain(error);
    }

    [Fact]
    public void HandleError_AssignsSeverity()
    {
        var error = _handler.HandleError(
            AssistantErrorType.InputError,
            "Invalid input");

        error.Severity.Should().Be(AssistantErrorSeverity.Low);
    }

    [Fact]
    public void HandleError_AssignsHighSeverity_ForMemoryFailure()
    {
        var error = _handler.HandleError(
            AssistantErrorType.MemoryFailure,
            "Save failed");

        error.Severity.Should().Be(AssistantErrorSeverity.High);
    }

    [Fact]
    public void HandleError_IncludesRecoveryGuidance()
    {
        var error = _handler.HandleError(
            AssistantErrorType.Timeout,
            "Operation timed out");

        error.RecoveryGuidance.Should().NotBeNullOrEmpty();
        error.RecoveryGuidance.Should().Contain("retried");
    }

    [Fact]
    public void HandleError_IncludesExceptionDetails()
    {
        var ex = new InvalidOperationException("Test exception");
        var error = _handler.HandleError(
            AssistantErrorType.InternalError,
            "Something went wrong",
            exception: ex);

        error.Exception.Should().Be(ex);
        error.Details.Should().Be("Test exception");
    }

    [Fact]
    public void HandleError_RaisesEvent()
    {
        AssistantErrorEventArgs? capturedArgs = null;
        _handler.ErrorOccurred += (_, args) => capturedArgs = args;

        var error = _handler.HandleError(
            AssistantErrorType.NotFound,
            "Item not found");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Error.Id.Should().Be(error.Id);
    }

    [Fact]
    public void HandleError_WithTrace_RecordsError()
    {
        var trace = new AssistantTrace();
        var handler = new AssistantErrorHandler(trace: trace);

        handler.HandleError(
            AssistantErrorType.ToolFailure,
            "Tool failed");

        trace.GetByType(TraceType.Error).Should().HaveCount(1);
    }

    [Fact]
    public async Task AttemptRecoveryAsync_ReturnsResult()
    {
        var error = _handler.HandleError(
            AssistantErrorType.ToolFailure,
            "Tool crashed");

        var result = await _handler.AttemptRecoveryAsync(error);

        result.Should().NotBeNull();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AttemptRecoveryAsync_RaisesEvent()
    {
        RecoveryAttemptEventArgs? capturedArgs = null;
        _handler.RecoveryAttempted += (_, args) => capturedArgs = args;

        var error = _handler.HandleError(
            AssistantErrorType.Timeout,
            "Timed out");

        await _handler.AttemptRecoveryAsync(error);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Strategy.Should().Be(RecoveryStrategy.Retry);
    }

    [Fact]
    public async Task AttemptRecoveryAsync_UsesCorrectStrategy_ForInputError()
    {
        RecoveryAttemptEventArgs? capturedArgs = null;
        _handler.RecoveryAttempted += (_, args) => capturedArgs = args;

        var error = _handler.HandleError(
            AssistantErrorType.InputError,
            "Bad input");

        await _handler.AttemptRecoveryAsync(error);

        capturedArgs!.Strategy.Should().Be(RecoveryStrategy.Escalate);
    }

    [Fact]
    public async Task AttemptRecoveryAsync_UsesResetStrategy_ForInvalidState()
    {
        RecoveryAttemptEventArgs? capturedArgs = null;
        _handler.RecoveryAttempted += (_, args) => capturedArgs = args;

        var error = _handler.HandleError(
            AssistantErrorType.InvalidStateTransition,
            "Invalid state");

        await _handler.AttemptRecoveryAsync(error);

        capturedArgs!.Strategy.Should().Be(RecoveryStrategy.Reset);
    }

    [Fact]
    public async Task WithErrorBoundaryAsync_ReturnsValue_OnSuccess()
    {
        var result = await _handler.WithErrorBoundaryAsync(
            async () =>
            {
                await Task.Delay(1);
                return 42;
            },
            AssistantErrorType.InternalError,
            "test operation");

        result.Success.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task WithErrorBoundaryAsync_CatchesException()
    {
        var result = await _handler.WithErrorBoundaryAsync<int>(
            async () =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("Test");
            },
            AssistantErrorType.InternalError,
            "test operation",
            fallbackValue: -1);

        result.Success.Should().BeFalse();
        result.Value.Should().Be(-1);
        result.Error.Should().NotBeNull();
        result.Error!.Type.Should().Be(AssistantErrorType.InternalError);
    }

    [Fact]
    public async Task WithErrorBoundaryAsync_PropagatesCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await _handler.WithErrorBoundaryAsync(
            async () =>
            {
                await Task.Delay(1000, cts.Token);
                return 1;
            },
            AssistantErrorType.InternalError,
            "test");

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void WithErrorBoundary_ReturnsValue_OnSuccess()
    {
        var result = _handler.WithErrorBoundary(
            () => "hello",
            AssistantErrorType.InternalError,
            "test operation");

        result.Success.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void WithErrorBoundary_CatchesException()
    {
        var result = _handler.WithErrorBoundary<string>(
            () => throw new Exception("Test"),
            AssistantErrorType.InternalError,
            "test operation",
            fallbackValue: "fallback");

        result.Success.Should().BeFalse();
        result.Value.Should().Be("fallback");
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void GetErrorsByType_FiltersCorrectly()
    {
        _handler.HandleError(AssistantErrorType.ToolFailure, "Error 1");
        _handler.HandleError(AssistantErrorType.NetworkError, "Error 2");
        _handler.HandleError(AssistantErrorType.ToolFailure, "Error 3");

        var toolErrors = _handler.GetErrorsByType(AssistantErrorType.ToolFailure);

        toolErrors.Should().HaveCount(2);
    }

    [Fact]
    public void GetErrorsBySeverity_FiltersCorrectly()
    {
        _handler.HandleError(AssistantErrorType.InputError, "Low"); // Low
        _handler.HandleError(AssistantErrorType.NetworkError, "Medium"); // Medium
        _handler.HandleError(AssistantErrorType.MemoryFailure, "High"); // High

        var highErrors = _handler.GetErrorsBySeverity(AssistantErrorSeverity.High);

        highErrors.Should().HaveCount(1);
        highErrors[0].Message.Should().Be("High");
    }

    [Fact]
    public void GetRecentErrors_ReturnsLastN()
    {
        for (int i = 1; i <= 5; i++)
        {
            _handler.HandleError(AssistantErrorType.InternalError, $"Error {i}");
        }

        var recent = _handler.GetRecentErrors(2);

        recent.Should().HaveCount(2);
        recent[0].Message.Should().Be("Error 4");
        recent[1].Message.Should().Be("Error 5");
    }

    [Fact]
    public void ClearHistory_RemovesAllErrors()
    {
        _handler.HandleError(AssistantErrorType.InternalError, "Error 1");
        _handler.HandleError(AssistantErrorType.InternalError, "Error 2");

        _handler.ClearHistory();

        _handler.ErrorHistory.Should().BeEmpty();
    }

    [Fact]
    public void MaxHistorySize_TrimsOldErrors()
    {
        var smallHandler = new AssistantErrorHandler(maxHistorySize: 3);

        for (int i = 1; i <= 5; i++)
        {
            smallHandler.HandleError(AssistantErrorType.InternalError, $"Error {i}");
        }

        smallHandler.ErrorHistory.Should().HaveCount(3);
        smallHandler.ErrorHistory[0].Message.Should().Be("Error 3");
    }

    [Fact]
    public void ExportToJson_ProducesValidJson()
    {
        _handler.HandleError(AssistantErrorType.ToolFailure, "Test error");

        var json = _handler.ExportToJson();

        json.Should().Contain("\"Type\": \"ToolFailure\"");
        json.Should().Contain("Test error");
    }

    [Fact]
    public void ErrorHistory_IsThreadSafe()
    {
        Parallel.For(0, 50, i =>
        {
            _handler.HandleError(AssistantErrorType.InternalError, $"Error {i}");
        });

        _handler.ErrorHistory.Count.Should().Be(50);
    }
}

public class AssistantErrorTests
{
    [Fact]
    public void ToUserMessage_FormatsLowSeverity()
    {
        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: AssistantErrorType.InputError,
            Message: "Invalid input",
            Details: null,
            Severity: AssistantErrorSeverity.Low,
            RecoveryGuidance: "Try again",
            OccurredAt: DateTimeOffset.UtcNow);

        var message = error.ToUserMessage();

        message.Should().StartWith("Minor issue:");
        message.Should().Contain("Invalid input");
    }

    [Fact]
    public void ToUserMessage_FormatsCriticalSeverity()
    {
        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: AssistantErrorType.InternalError,
            Message: "System failure",
            Details: null,
            Severity: AssistantErrorSeverity.Critical,
            RecoveryGuidance: "restart",
            OccurredAt: DateTimeOffset.UtcNow);

        var message = error.ToUserMessage();

        message.Should().StartWith("Critical failure:");
        message.Should().Contain("Please restart");
    }

    [Fact]
    public void IsAutoRecoverable_ReturnsTrue_ForToolFailure()
    {
        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: AssistantErrorType.ToolFailure,
            Message: "Tool crashed",
            Details: null,
            Severity: AssistantErrorSeverity.Medium,
            RecoveryGuidance: "Retry",
            OccurredAt: DateTimeOffset.UtcNow);

        error.IsAutoRecoverable().Should().BeTrue();
    }

    [Fact]
    public void IsAutoRecoverable_ReturnsFalse_ForCriticalSeverity()
    {
        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: AssistantErrorType.ToolFailure,
            Message: "Tool crashed",
            Details: null,
            Severity: AssistantErrorSeverity.Critical,
            RecoveryGuidance: "Restart",
            OccurredAt: DateTimeOffset.UtcNow);

        error.IsAutoRecoverable().Should().BeFalse();
    }

    [Fact]
    public void RequiresUserAction_ReturnsTrue_ForInputError()
    {
        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: AssistantErrorType.InputError,
            Message: "Bad input",
            Details: null,
            Severity: AssistantErrorSeverity.Low,
            RecoveryGuidance: "Fix input",
            OccurredAt: DateTimeOffset.UtcNow);

        error.RequiresUserAction().Should().BeTrue();
    }

    [Fact]
    public void RequiresUserAction_ReturnsTrue_ForCriticalSeverity()
    {
        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: AssistantErrorType.InternalError,
            Message: "Critical",
            Details: null,
            Severity: AssistantErrorSeverity.Critical,
            RecoveryGuidance: "Restart",
            OccurredAt: DateTimeOffset.UtcNow);

        error.RequiresUserAction().Should().BeTrue();
    }

    [Fact]
    public void RequiresUserAction_ReturnsFalse_ForNetworkError()
    {
        var error = new AssistantError(
            Id: Guid.NewGuid(),
            Type: AssistantErrorType.NetworkError,
            Message: "Connection lost",
            Details: null,
            Severity: AssistantErrorSeverity.Medium,
            RecoveryGuidance: "Reconnecting",
            OccurredAt: DateTimeOffset.UtcNow);

        error.RequiresUserAction().Should().BeFalse();
    }
}

public class AssistantErrorTypeTests
{
    [Theory]
    [InlineData(AssistantErrorType.ToolFailure)]
    [InlineData(AssistantErrorType.MemoryFailure)]
    [InlineData(AssistantErrorType.InvalidStateTransition)]
    [InlineData(AssistantErrorType.NetworkError)]
    [InlineData(AssistantErrorType.InputError)]
    [InlineData(AssistantErrorType.NotFound)]
    [InlineData(AssistantErrorType.PermissionDenied)]
    [InlineData(AssistantErrorType.Timeout)]
    [InlineData(AssistantErrorType.RateLimited)]
    [InlineData(AssistantErrorType.InternalError)]
    [InlineData(AssistantErrorType.ConfigurationError)]
    [InlineData(AssistantErrorType.ExternalServiceError)]
    public void AssistantErrorType_AllValuesAreDefined(AssistantErrorType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }
}

public class RecoveryStrategyTests
{
    [Theory]
    [InlineData(RecoveryStrategy.Retry)]
    [InlineData(RecoveryStrategy.Fallback)]
    [InlineData(RecoveryStrategy.Reset)]
    [InlineData(RecoveryStrategy.Escalate)]
    [InlineData(RecoveryStrategy.Ignore)]
    public void RecoveryStrategy_AllValuesAreDefined(RecoveryStrategy strategy)
    {
        Enum.IsDefined(strategy).Should().BeTrue();
    }
}
