using FluentAssertions;
using InControl.Core.Errors;
using Xunit;

namespace InControl.Core.Tests.Errors;

public class InControlErrorTests
{
    [Fact]
    public void Create_SetsCodeAndMessage()
    {
        var error = InControlError.Create(ErrorCode.InvalidArgument, "Test message");

        error.Code.Should().Be(ErrorCode.InvalidArgument);
        error.Message.Should().Be("Test message");
        error.Severity.Should().Be(ErrorSeverity.Error);
    }

    [Fact]
    public void Cancelled_CreatesInfoSeverityError()
    {
        var error = InControlError.Cancelled("Download");

        error.Code.Should().Be(ErrorCode.Cancelled);
        error.Message.Should().Contain("Download");
        error.Message.Should().Contain("cancelled");
        error.Severity.Should().Be(ErrorSeverity.Info);
    }

    [Fact]
    public void Timeout_IncludesDuration_WhenProvided()
    {
        var error = InControlError.Timeout("Request", TimeSpan.FromSeconds(30));

        error.Code.Should().Be(ErrorCode.Timeout);
        error.Message.Should().Contain("30");
        error.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void ConnectionFailed_IncludesSuggestions()
    {
        var error = InControlError.ConnectionFailed("http://localhost:11434", "Ollama");

        error.Code.Should().Be(ErrorCode.ConnectionFailed);
        error.Message.Should().Contain("localhost:11434");
        error.Message.Should().Contain("Ollama");
        error.Suggestions.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ModelNotFound_IncludesPullSuggestion()
    {
        var error = InControlError.ModelNotFound("llama3.2");

        error.Code.Should().Be(ErrorCode.ModelNotFound);
        error.Message.Should().Contain("llama3.2");
        error.Suggestions.Should().Contain(s => s.Contains("ollama pull"));
    }

    [Fact]
    public void PathNotAllowed_HasCriticalSeverity()
    {
        var error = InControlError.PathNotAllowed("/etc/passwd");

        error.Code.Should().Be(ErrorCode.PathNotAllowed);
        error.Severity.Should().Be(ErrorSeverity.Critical);
        error.Detail.Should().Contain("/etc/passwd");
    }

    [Fact]
    public void FromException_MapsTimeoutException()
    {
        var exception = new TimeoutException("Timed out");

        var error = InControlError.FromException(exception);

        error.Code.Should().Be(ErrorCode.Timeout);
        error.Message.Should().NotBeEmpty();
        error.Detail.Should().Be("Timed out");
    }

    [Fact]
    public void FromException_MapsCancelledException()
    {
        var exception = new OperationCanceledException();

        var error = InControlError.FromException(exception);

        error.Code.Should().Be(ErrorCode.Cancelled);
    }

    [Fact]
    public void FromException_MapsUnknownException()
    {
        var exception = new InvalidDataException("Bad data");

        var error = InControlError.FromException(exception);

        error.Code.Should().Be(ErrorCode.Unknown);
    }

    [Fact]
    public void FromException_AllowsCodeOverride()
    {
        var exception = new Exception("Generic error");

        var error = InControlError.FromException(exception, ErrorCode.InferenceFailed);

        error.Code.Should().Be(ErrorCode.InferenceFailed);
    }

    [Fact]
    public void Timestamp_IsSetAutomatically()
    {
        var before = DateTimeOffset.UtcNow;
        var error = InControlError.Create(ErrorCode.Unknown, "Test");
        var after = DateTimeOffset.UtcNow;

        error.Timestamp.Should().BeOnOrAfter(before);
        error.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Error_IsSerializable()
    {
        var error = new InControlError
        {
            Code = ErrorCode.ConnectionFailed,
            Message = "Connection failed",
            Detail = "Could not reach server",
            Suggestions = ["Check network", "Retry"],
            Severity = ErrorSeverity.Error,
            CorrelationId = "abc-123",
            Source = "OllamaClient"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(error);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<InControlError>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Code.Should().Be(error.Code);
        deserialized.Message.Should().Be(error.Message);
        deserialized.Detail.Should().Be(error.Detail);
        deserialized.Suggestions.Should().BeEquivalentTo(error.Suggestions);
        deserialized.CorrelationId.Should().Be(error.CorrelationId);
    }
}
