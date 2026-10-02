using FluentAssertions;
using InControl.Core.Errors;
using Xunit;

namespace InControl.Core.Tests.Errors;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_IsFailureTrue()
    {
        var error = InControlError.Create(ErrorCode.InvalidArgument, "Bad input");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Failure_WithCodeAndMessage_CreatesError()
    {
        var result = Result.Failure(ErrorCode.Timeout, "Operation timed out");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.Timeout);
        result.Error.Message.Should().Be("Operation timed out");
    }

    [Fact]
    public void FromException_CreatesFailure()
    {
        var exception = new TimeoutException("Timed out");

        var result = Result.FromException(exception);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.Timeout);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        var error = InControlError.Create(ErrorCode.Unknown, "Error");

        Result result = error;

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void OnSuccess_ExecutesAction_WhenSuccess()
    {
        var executed = false;
        var result = Result.Success();

        result.OnSuccess(() => executed = true);

        executed.Should().BeTrue();
    }

    [Fact]
    public void OnSuccess_DoesNotExecute_WhenFailure()
    {
        var executed = false;
        var result = Result.Failure(ErrorCode.Unknown, "Error");

        result.OnSuccess(() => executed = true);

        executed.Should().BeFalse();
    }

    [Fact]
    public void OnFailure_ExecutesAction_WhenFailure()
    {
        InControlError? capturedError = null;
        var error = InControlError.Create(ErrorCode.Timeout, "Timeout");
        var result = Result.Failure(error);

        result.OnFailure(e => capturedError = e);

        capturedError.Should().Be(error);
    }

    [Fact]
    public void ThrowIfFailure_Throws_WhenFailure()
    {
        var result = Result.Failure(ErrorCode.InvalidState, "Bad state");

        var action = () => result.ThrowIfFailure();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Bad state");
    }

    [Fact]
    public void ThrowIfFailure_DoesNotThrow_WhenSuccess()
    {
        var result = Result.Success();

        var action = () => result.ThrowIfFailure();

        action.Should().NotThrow();
    }
}

public class ResultOfTTests
{
    [Fact]
    public void Success_HasValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_HasNoValue()
    {
        var result = Result<int>.Failure(ErrorCode.Unknown, "Error");

        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default);
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        var error = InControlError.Create(ErrorCode.Unknown, "Error");

        Result<int> result = error;

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetValueOrThrow_ReturnsValue_WhenSuccess()
    {
        var result = Result<int>.Success(42);

        var value = result.GetValueOrThrow();

        value.Should().Be(42);
    }

    [Fact]
    public void GetValueOrThrow_Throws_WhenFailure()
    {
        var result = Result<int>.Failure(ErrorCode.Unknown, "Error message");

        var action = () => result.GetValueOrThrow();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Error message");
    }

    [Fact]
    public void GetValueOrDefault_ReturnsValue_WhenSuccess()
    {
        var result = Result<int>.Success(42);

        var value = result.GetValueOrDefault(0);

        value.Should().Be(42);
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefault_WhenFailure()
    {
        var result = Result<int>.Failure(ErrorCode.Unknown, "Error");

        var value = result.GetValueOrDefault(99);

        value.Should().Be(99);
    }

    [Fact]
    public void GetValueOrDefault_WithFactory_UsesError()
    {
        var result = Result<string>.Failure(ErrorCode.FileNotFound, "Not found");

        var value = result.GetValueOrDefault(e => $"Error: {e.Code}");

        value.Should().Be("Error: FileNotFound");
    }

    [Fact]
    public void Map_TransformsValue_WhenSuccess()
    {
        var result = Result<int>.Success(10);

        var mapped = result.Map(x => x * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(20);
    }

    [Fact]
    public void Map_PropagatesError_WhenFailure()
    {
        var error = InControlError.Create(ErrorCode.Unknown, "Error");
        var result = Result<int>.Failure(error);

        var mapped = result.Map(x => x * 2);

        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_ChainsOperations_WhenSuccess()
    {
        var result = Result<int>.Success(10);

        var bound = result.Bind(x =>
            x > 0
                ? Result<string>.Success($"Value: {x}")
                : Result<string>.Failure(ErrorCode.InvalidArgument, "Negative"));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("Value: 10");
    }

    [Fact]
    public void Bind_PropagatesFirstError()
    {
        var error = InControlError.Create(ErrorCode.Unknown, "First error");
        var result = Result<int>.Failure(error);

        var bound = result.Bind(x => Result<string>.Success($"Value: {x}"));

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(error);
    }

    [Fact]
    public void Match_ExecutesOnSuccess_WhenSuccess()
    {
        var result = Result<int>.Success(42);

        var output = result.Match(
            onSuccess: v => $"Got {v}",
            onFailure: e => $"Error: {e.Code}");

        output.Should().Be("Got 42");
    }

    [Fact]
    public void Match_ExecutesOnFailure_WhenFailure()
    {
        var result = Result<int>.Failure(ErrorCode.Timeout, "Timeout");

        var output = result.Match(
            onSuccess: v => $"Got {v}",
            onFailure: e => $"Error: {e.Code}");

        output.Should().Be("Error: Timeout");
    }

    [Fact]
    public void ToResult_DiscardsValue()
    {
        var result = Result<int>.Success(42);

        var nonGeneric = result.ToResult();

        nonGeneric.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ToResult_PreservesError()
    {
        var error = InControlError.Create(ErrorCode.Unknown, "Error");
        var result = Result<int>.Failure(error);

        var nonGeneric = result.ToResult();

        nonGeneric.IsFailure.Should().BeTrue();
        nonGeneric.Error.Should().Be(error);
    }

    [Fact]
    public void OnSuccess_ExecutesAction_WhenSuccess()
    {
        var captured = 0;
        var result = Result<int>.Success(42);

        result.OnSuccess(v => captured = v);

        captured.Should().Be(42);
    }

    [Fact]
    public void OnFailure_ExecutesAction_WhenFailure()
    {
        InControlError? captured = null;
        var error = InControlError.Create(ErrorCode.Unknown, "Error");
        var result = Result<int>.Failure(error);

        result.OnFailure(e => captured = e);

        captured.Should().Be(error);
    }
}
