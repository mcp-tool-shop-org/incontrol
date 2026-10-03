using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

/// <summary>
/// Every error type: its severity, the guidance shown to the user, and the recovery it triggers.
/// </summary>
public class AssistantErrorMatrixTests
{
    public static TheoryData<AssistantErrorType, AssistantErrorSeverity, RecoveryStrategy, string> Matrix => new()
    {
        { AssistantErrorType.InputError, AssistantErrorSeverity.Low, RecoveryStrategy.Escalate, "rephrase" },
        { AssistantErrorType.NotFound, AssistantErrorSeverity.Low, RecoveryStrategy.Fallback, "verify the reference" },
        { AssistantErrorType.RateLimited, AssistantErrorSeverity.Medium, RecoveryStrategy.Retry, "Too many requests" },
        { AssistantErrorType.Timeout, AssistantErrorSeverity.Medium, RecoveryStrategy.Retry, "too long" },
        { AssistantErrorType.NetworkError, AssistantErrorSeverity.Medium, RecoveryStrategy.Retry, "check your connection" },
        { AssistantErrorType.ToolFailure, AssistantErrorSeverity.Medium, RecoveryStrategy.Retry, "retried automatically" },
        { AssistantErrorType.ExternalServiceError, AssistantErrorSeverity.Medium, RecoveryStrategy.Retry, "external service" },
        { AssistantErrorType.MemoryFailure, AssistantErrorSeverity.High, RecoveryStrategy.Fallback, "could not be saved" },
        { AssistantErrorType.PermissionDenied, AssistantErrorSeverity.High, RecoveryStrategy.Escalate, "administrator" },
        { AssistantErrorType.InvalidStateTransition, AssistantErrorSeverity.High, RecoveryStrategy.Reset, "safe state" },
        { AssistantErrorType.ConfigurationError, AssistantErrorSeverity.High, RecoveryStrategy.Escalate, "configuration" },
        { AssistantErrorType.InternalError, AssistantErrorSeverity.High, RecoveryStrategy.Reset, "report it" }
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task EachType_HasTheSeverityGuidanceAndRecoveryItIsDocumentedWith(
        AssistantErrorType type, AssistantErrorSeverity severity, RecoveryStrategy strategy, string guidanceFragment)
    {
        var handler = new AssistantErrorHandler();
        RecoveryAttemptEventArgs? attempt = null;
        handler.RecoveryAttempted += (_, e) => attempt = e;

        var error = handler.HandleError(type, "something happened");
        var recovery = await handler.AttemptRecoveryAsync(error);

        error.Severity.Should().Be(severity);
        error.RecoveryGuidance.Should().Contain(guidanceFragment);
        error.Details.Should().BeNull();
        attempt!.Strategy.Should().Be(strategy);
        attempt.Error.Should().BeSameAs(error);

        switch (strategy)
        {
            case RecoveryStrategy.Retry:
                recovery.Recovered.Should().BeFalse();
                recovery.Message.Should().Contain("Retry scheduled");
                break;
            case RecoveryStrategy.Fallback:
                recovery.Recovered.Should().BeTrue();
                recovery.Message.Should().Contain("Fallback applied");
                break;
            case RecoveryStrategy.Reset:
                recovery.Recovered.Should().BeTrue();
                recovery.Message.Should().Contain("safe defaults");
                break;
            case RecoveryStrategy.Escalate:
                recovery.Recovered.Should().BeFalse();
                recovery.Message.Should().Be($"User action required: {error.RecoveryGuidance}");
                break;
        }
    }

    [Fact]
    public async Task AnUnknownType_IsMediumSeverityAndEscalatesWithGenericGuidance()
    {
        var handler = new AssistantErrorHandler();

        var error = handler.HandleError((AssistantErrorType)99, "mystery");
        var recovery = await handler.AttemptRecoveryAsync(error);

        error.Severity.Should().Be(AssistantErrorSeverity.Medium);
        error.RecoveryGuidance.Should().Contain("contact support");
        recovery.Recovered.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(InvalidOperationException))]
    public void InternalError_IsCriticalOnlyForOutOfMemory(Type exceptionType)
    {
        var exception = (Exception)(exceptionType == typeof(OutOfMemoryException)
            ? new OutOfMemoryException("oom")
            : new InvalidOperationException("oops"));

        var error = new AssistantErrorHandler().HandleError(AssistantErrorType.InternalError, "failed", exception: exception);

        if (exceptionType == typeof(OutOfMemoryException))
        {
            error.Severity.Should().Be(AssistantErrorSeverity.Critical);
            error.RecoveryGuidance.Should().Be("restart the application");
            error.ToUserMessage().Should().Be("Critical failure: failed. Please restart the application");
            error.RequiresUserAction().Should().BeTrue();
        }
        else
        {
            error.Severity.Should().Be(AssistantErrorSeverity.High);
            error.RecoveryGuidance.Should().Contain("This is a bug");
        }

        error.Details.Should().Be(exception.Message, "details default to the exception message");
        error.Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public void ExplicitDetails_BeatTheExceptionMessage()
    {
        var error = new AssistantErrorHandler().HandleError(
            AssistantErrorType.ToolFailure, "failed", details: "custom", exception: new IOException("io"));

        error.Details.Should().Be("custom");
    }

    [Theory]
    [InlineData(AssistantErrorSeverity.Low, "Minor issue: msg")]
    [InlineData(AssistantErrorSeverity.Medium, "Problem encountered: msg. do this")]
    [InlineData(AssistantErrorSeverity.High, "Significant error: msg. do this")]
    [InlineData(AssistantErrorSeverity.Critical, "Critical failure: msg. Please do this")]
    [InlineData((AssistantErrorSeverity)99, "msg")]
    public void ToUserMessage_DependsOnSeverity(AssistantErrorSeverity severity, string expected)
    {
        var error = new AssistantError(
            Guid.NewGuid(), AssistantErrorType.ToolFailure, "msg", null, severity, "do this", DateTimeOffset.UtcNow);

        error.ToUserMessage().Should().Be(expected);
    }

    [Theory]
    [InlineData(AssistantErrorType.ToolFailure, AssistantErrorSeverity.Medium, true, false)]
    [InlineData(AssistantErrorType.Timeout, AssistantErrorSeverity.Medium, true, false)]
    [InlineData(AssistantErrorType.NetworkError, AssistantErrorSeverity.Medium, true, false)]
    [InlineData(AssistantErrorType.RateLimited, AssistantErrorSeverity.Medium, true, false)]
    [InlineData(AssistantErrorType.RateLimited, AssistantErrorSeverity.Critical, false, true)]
    [InlineData(AssistantErrorType.MemoryFailure, AssistantErrorSeverity.High, false, false)]
    [InlineData(AssistantErrorType.InputError, AssistantErrorSeverity.Low, false, true)]
    [InlineData(AssistantErrorType.PermissionDenied, AssistantErrorSeverity.High, false, true)]
    [InlineData(AssistantErrorType.ConfigurationError, AssistantErrorSeverity.High, false, true)]
    [InlineData(AssistantErrorType.InternalError, AssistantErrorSeverity.Critical, false, true)]
    [InlineData(AssistantErrorType.NotFound, AssistantErrorSeverity.Low, false, false)]
    public void AutoRecoverableAndRequiresUserAction_FollowTypeAndSeverity(
        AssistantErrorType type, AssistantErrorSeverity severity, bool autoRecoverable, bool requiresUser)
    {
        var error = new AssistantError(Guid.NewGuid(), type, "m", null, severity, "g", DateTimeOffset.UtcNow);

        error.IsAutoRecoverable().Should().Be(autoRecoverable);
        error.RequiresUserAction().Should().Be(requiresUser);
    }

    [Fact]
    public async Task ErrorBoundary_ReturnsTheValue_OrTheFallbackAndTheRecordedError()
    {
        var handler = new AssistantErrorHandler();

        var ok = await handler.WithErrorBoundaryAsync(() => Task.FromResult(7), AssistantErrorType.ToolFailure, "adding");
        var failed = await handler.WithErrorBoundaryAsync<int>(
            () => throw new InvalidOperationException("nope"), AssistantErrorType.ToolFailure, "adding", fallbackValue: -1);

        ok.Success.Should().BeTrue();
        ok.Value.Should().Be(7);
        failed.Success.Should().BeFalse();
        failed.Value.Should().Be(-1);
        failed.Error!.Message.Should().Be("Error in adding");
        failed.Error.Details.Should().Be("nope");
        handler.ErrorHistory.Should().ContainSingle();
    }

    [Fact]
    public async Task AsyncErrorBoundary_DoesNotSwallowCancellation()
    {
        var handler = new AssistantErrorHandler();

        var act = async () => await handler.WithErrorBoundaryAsync<int>(
            () => throw new OperationCanceledException(), AssistantErrorType.Timeout, "waiting");

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.ErrorHistory.Should().BeEmpty();
    }

    [Fact]
    public void SyncErrorBoundary_ReturnsTheValue_OrTheFallbackAndTheRecordedError()
    {
        var handler = new AssistantErrorHandler();

        var ok = handler.WithErrorBoundary(() => "fine", AssistantErrorType.InputError, "parsing");
        var failed = handler.WithErrorBoundary<string>(
            () => throw new FormatException("bad format"), AssistantErrorType.InputError, "parsing", fallbackValue: "default");

        ok.Success.Should().BeTrue();
        ok.Value.Should().Be("fine");
        failed.Success.Should().BeFalse();
        failed.Value.Should().Be("default");
        failed.Error!.Type.Should().Be(AssistantErrorType.InputError);
        failed.Error.Exception.Should().BeOfType<FormatException>();
    }

    [Fact]
    public void QueriesAndExport_ReflectTheHistory()
    {
        var handler = new AssistantErrorHandler();
        handler.HandleError(AssistantErrorType.InputError, "low one");
        handler.HandleError(AssistantErrorType.ToolFailure, "medium one", exception: new IOException("io"));
        handler.HandleError(AssistantErrorType.MemoryFailure, "high one");

        handler.GetErrorsByType(AssistantErrorType.ToolFailure).Should().ContainSingle();
        handler.GetErrorsBySeverity(AssistantErrorSeverity.Medium).Should().HaveCount(2);
        handler.GetErrorsBySeverity(AssistantErrorSeverity.Critical).Should().BeEmpty();
        handler.GetRecentErrors(2).Select(e => e.Message).Should().Equal("medium one", "high one");

        var json = handler.ExportToJson();
        json.Should().Contain("\"Type\": \"ToolFailure\"");
        json.Should().Contain("\"ExceptionType\": \"IOException\"");
        json.Should().Contain("\"ExceptionMessage\": \"io\"");

        handler.ClearHistory();
        handler.ErrorHistory.Should().BeEmpty();
        handler.ExportToJson().Should().Be("[]");
    }

    [Fact]
    public void ErrorOccurred_IsRaisedWithTheRecordedError()
    {
        var handler = new AssistantErrorHandler();
        var raised = new List<AssistantError>();
        handler.ErrorOccurred += (_, e) => raised.Add(e.Error);

        var error = handler.HandleError(AssistantErrorType.NetworkError, "offline");

        raised.Should().ContainSingle().Which.Should().BeSameAs(error);
    }
}
