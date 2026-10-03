using FluentAssertions;
using InControl.Core.Errors;
using InControl.Core.State;
using Xunit;

namespace InControl.Core.Tests.Errors;

/// <summary>
/// How exceptions become errors the user sees: the code chosen, the text that is safe to show,
/// and that the exception's own message is kept out of the user-facing text.
/// </summary>
public class ErrorMappingTests
{
    public static TheoryData<Exception, ErrorCode, string> Mappings => new()
    {
        { new OperationCanceledException("internal detail"), ErrorCode.Cancelled, "The operation was cancelled." },
        { new TaskCanceledException("internal detail"), ErrorCode.Cancelled, "The operation was cancelled." },
        { new TimeoutException("internal detail"), ErrorCode.Timeout, "The operation timed out." },
        { new HttpRequestException("internal detail"), ErrorCode.ConnectionFailed, "Unable to connect to the server." },
        { new FileNotFoundException("internal detail"), ErrorCode.FileNotFound, "The requested file was not found." },
        { new UnauthorizedAccessException("internal detail"), ErrorCode.PermissionDenied, "Permission denied." },
        { new ArgumentException("internal detail"), ErrorCode.InvalidArgument, "An unexpected error occurred." },
        { new ArgumentNullException("internal detail"), ErrorCode.InvalidArgument, "An unexpected error occurred." },
        { new InvalidOperationException("internal detail"), ErrorCode.InvalidState, "An unexpected error occurred." },
        { new NotSupportedException("internal detail"), ErrorCode.NotSupported, "An unexpected error occurred." },
        { new System.Text.Json.JsonException("internal detail"), ErrorCode.DeserializationFailed, "Failed to read data." },
        { new IOException("internal detail"), ErrorCode.Unknown, "An unexpected error occurred." }
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public void FromException_PicksTheCode_AndShowsOnlyASafeMessage(Exception exception, ErrorCode code, string message)
    {
        var error = InControlError.FromException(exception);

        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Detail.Should().Be(exception.Message, "the raw text is kept for diagnostics only");
        error.Message.Should().NotContain("internal detail");
        error.Severity.Should().Be(ErrorSeverity.Error);
    }

    [Fact]
    public void FromException_ACallerSuppliedCodeWins()
    {
        var error = InControlError.FromException(new IOException("disk"), ErrorCode.FileOperationFailed);

        error.Code.Should().Be(ErrorCode.FileOperationFailed);
        error.Message.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public void Cancelled_IsInformational()
    {
        var error = InControlError.Cancelled("Export");

        error.Code.Should().Be(ErrorCode.Cancelled);
        error.Message.Should().Be("Export was cancelled.");
        error.Severity.Should().Be(ErrorSeverity.Info);
        InControlError.Cancelled().Message.Should().Be("Operation was cancelled.");
    }

    [Fact]
    public void Timeout_MentionsTheDurationWhenKnown()
    {
        InControlError.Timeout("Download", TimeSpan.FromSeconds(12.5)).Message.Should().Be("Download timed out after 12.5 seconds.");
        var unknown = InControlError.Timeout();
        unknown.Message.Should().Be("Operation timed out.");
        unknown.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void ConnectionFailed_NamesTheBackendOrFallsBackToGenericWords()
    {
        var named = InControlError.ConnectionFailed("http://127.0.0.1:11434", "Ollama");
        var generic = InControlError.ConnectionFailed("http://x");

        named.Message.Should().Be("Could not connect to Ollama at http://127.0.0.1:11434.");
        named.Suggestions.Should().Contain("Ensure Ollama is running.");
        generic.Message.Should().Be("Could not connect to backend at http://x.");
        generic.Suggestions.Should().Contain("Ensure the service is running.");
    }

    [Fact]
    public void ModelNotFound_SuggestsThePullCommand()
    {
        var error = InControlError.ModelNotFound("llama3.2");

        error.Code.Should().Be(ErrorCode.ModelNotFound);
        error.Suggestions.Should().Contain("Run 'ollama pull llama3.2' to download the model.");
    }

    [Fact]
    public void PathNotAllowed_IsCritical_AndKeepsThePathOutOfTheMessage()
    {
        var error = InControlError.PathNotAllowed("C:\\Windows\\system32");

        error.Severity.Should().Be(ErrorSeverity.Critical);
        error.Message.Should().NotContain("system32");
        error.Detail.Should().Contain("C:\\Windows\\system32");
    }
}

public class ResultHelpersTests
{
    [Fact]
    public void UnitValue_IsAlwaysEqualToItself()
    {
        var a = Unit.Value;
        var b = new Unit();

        a.Equals(b).Should().BeTrue();
        a.Equals((object)b).Should().BeTrue();
        a.Equals("not a unit").Should().BeFalse();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(0);
        a.ToString().Should().Be("()");
    }

    [Fact]
    public void FromException_WrapsTheMappedError()
    {
        var result = Result<int>.FromException(new TimeoutException("slow"));
        var coded = Result<int>.FromException(new TimeoutException("slow"), ErrorCode.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.Timeout);
        coded.Error!.Code.Should().Be(ErrorCode.Unknown);
    }

    [Fact]
    public void Unwrap_ReturnsTheValue_OrThrowsTheErrorMessage()
    {
        Result<int>.Success(5).Unwrap().Should().Be(5);

        var failed = Result<int>.Failure(ErrorCode.FileNotFound, "no such thing");
        var act = () => failed.Unwrap();
        act.Should().Throw<InvalidOperationException>().WithMessage("no such thing");
        var other = () => failed.GetValueOrThrow();
        other.Should().Throw<InvalidOperationException>().WithMessage("no such thing");
    }

    [Fact]
    public void GetValueOrDefault_UsesTheDefaultOrTheFactoryOnlyOnFailure()
    {
        var ok = Result<int>.Success(1);
        var failed = Result<int>.Failure(ErrorCode.FileNotFound, "x");

        ok.GetValueOrDefault(9).Should().Be(1);
        failed.GetValueOrDefault(9).Should().Be(9);
        ok.GetValueOrDefault(_ => 7).Should().Be(1);
        failed.GetValueOrDefault(error => error.Code == ErrorCode.FileNotFound ? 7 : 0).Should().Be(7);
    }

    [Fact]
    public void MapAndBind_TransformSuccess_AndPassFailureThrough()
    {
        var ok = Result<int>.Success(2);
        var failed = Result<int>.Failure(ErrorCode.FileNotFound, "x");

        ok.Map(v => v * 10).Value.Should().Be(20);
        failed.Map(v => v * 10).Error!.Code.Should().Be(ErrorCode.FileNotFound);
        ok.Bind(v => Result<string>.Success("n" + v)).Value.Should().Be("n2");
        failed.Bind(v => Result<string>.Success("n" + v)).IsFailure.Should().BeTrue();
        ok.ToResult().IsSuccess.Should().BeTrue();
        failed.ToResult().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversions_BuildSuccessAndFailure()
    {
        Result<string> fromValue = "text";
        Result<string> fromError = InControlError.Create(ErrorCode.Unknown, "oops");

        fromValue.Value.Should().Be("text");
        fromError.IsFailure.Should().BeTrue();
    }
}

public class StateSerializerEdgeTests
{
    private sealed record Sample(string Name, int Count);

    [Fact]
    public void Deserialize_JsonNull_IsAFailureForStringsBytesAndStreams()
    {
        StateSerializer.Deserialize<Sample>("null").IsFailure.Should().BeTrue();
        StateSerializer.Deserialize<Sample>("null"u8).Error!.Code.Should().Be(ErrorCode.DeserializationFailed);
    }

    [Fact]
    public async Task Deserialize_NullAndInvalidJson_FromAStream_AreFailures()
    {
        using var nullStream = new MemoryStream("null"u8.ToArray());
        using var badStream = new MemoryStream("{ nope"u8.ToArray());

        (await StateSerializer.DeserializeAsync<Sample>(nullStream)).Error!.Message.Should().Contain("null");
        (await StateSerializer.DeserializeAsync<Sample>(badStream)).Error!.Message.Should().StartWith("Invalid JSON");
    }

    [Fact]
    public void Deserialize_InvalidJson_FromBytes_IsAFailureWithTheParseMessage()
    {
        var result = StateSerializer.Deserialize<Sample>("{ nope"u8);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().StartWith("Invalid JSON");
    }

    [Fact]
    public async Task DeserializeAsync_Cancelled_ReportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var stream = new MemoryStream("{}"u8.ToArray());

        var result = await StateSerializer.DeserializeAsync<Sample>(stream, cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.Cancelled);
    }

    [Fact]
    public void Serialize_CompactAndIndented_AgreeOnContent_AndOmitNulls()
    {
        var value = new { Name = "a", Count = 3, Missing = (string?)null };

        var indented = StateSerializer.Serialize(value);
        var compact = StateSerializer.Serialize(value, compact: true);

        compact.Should().Be("{\"name\":\"a\",\"count\":3}");
        indented.Should().Contain("\n").And.Contain("\"name\": \"a\"");
        indented.Should().NotContain("missing");
        StateSerializer.SerializeToBytes(value, compact: true).Should().Equal(System.Text.Encoding.UTF8.GetBytes(compact));
        StateSerializer.SerializeToBytes(value).Length.Should().BeGreaterThan(compact.Length);
    }

    [Fact]
    public async Task SerializeAsync_WritesTheSameContent_CompactOrNot()
    {
        using var compactStream = new MemoryStream();
        using var indentedStream = new MemoryStream();

        await StateSerializer.SerializeAsync(compactStream, new Sample("a", 1), compact: true);
        await StateSerializer.SerializeAsync(indentedStream, new Sample("a", 1));

        System.Text.Encoding.UTF8.GetString(compactStream.ToArray()).Should().Be("{\"name\":\"a\",\"count\":1}");
        indentedStream.Length.Should().BeGreaterThan(compactStream.Length);
    }

    [Fact]
    public void ValidateRoundTrip_IsTrueForARecordThatSurvivesSerialization()
    {
        StateSerializer.ValidateRoundTrip(new Sample("a", 1)).Should().BeTrue();
    }

    [Fact]
    public void ValidateRoundTrip_IsFalseWhenWhatComesBackIsNotEqual()
    {
        StateSerializer.ValidateRoundTrip(new Lossy { Value = 5 }).Should().BeFalse();
    }

    private sealed class Lossy : IEquatable<Lossy>
    {
        public int Value { get; set; }

        // Never equal to a deserialized copy, so the round trip must report a mismatch.
        public bool Equals(Lossy? other) => ReferenceEquals(this, other);

        public override bool Equals(object? obj) => Equals(obj as Lossy);

        public override int GetHashCode() => Value;
    }
}
