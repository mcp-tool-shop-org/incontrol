using FluentAssertions;
using InControl.Core.Errors;
using Xunit;

namespace InControl.Core.Tests.Errors;

public class ErrorTextTests
{
    [Fact]
    public void Readable_PullsTheMessageOutOfOllamaJson()
    {
        const string raw = "{\"error\":{\"code\":400,\"message\":\"Multimodal data provided, but model does not support multimodal requests.\",\"type\":\"invalid_request_error\"}}";

        ErrorText.Readable(raw).Should().Be("Multimodal data provided, but model does not support multimodal requests.");
    }

    [Fact]
    public void Readable_UnescapesQuotesInsideTheMessage()
    {
        ErrorText.Readable("{\"message\":\"model \\\"x\\\" not found\"}").Should().Be("model \"x\" not found");
    }

    [Theory]
    [InlineData("Connection refused")]
    [InlineData("")]
    public void Readable_LeavesPlainTextAlone(string text)
    {
        ErrorText.Readable(text).Should().Be(text);
    }

    [Fact]
    public void Readable_KeepsTheMessageWhenTheEscapeIsNotValid()
    {
        // \u needs four hex digits. Unescape throws, and the sentence is still the message.
        ErrorText.Readable("{\"message\":\"bad\\u12\"}").Should().Be("bad\\u12");
    }
}
