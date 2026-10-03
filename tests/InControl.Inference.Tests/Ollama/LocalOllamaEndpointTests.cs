using FluentAssertions;
using Microsoft.Extensions.Options;
using InControl.Core.Configuration;
using Xunit;

namespace InControl.Inference.Ollama;

public class LocalOllamaEndpointTests
{
    [Theory]
    [InlineData("http://localhost:11434", "http://127.0.0.1:11434")]
    [InlineData("http://LocalHost:11434", "http://127.0.0.1:11434")]
    [InlineData("http://localhost:11434/", "http://127.0.0.1:11434/")]
    [InlineData("http://127.0.0.1:11434", "http://127.0.0.1:11434")]
    [InlineData("http://192.168.1.5:11434", "http://192.168.1.5:11434")]
    [InlineData("", "http://127.0.0.1:11434")]
    [InlineData("   ", "http://127.0.0.1:11434")]
    public void BaseUrl_NeverUsesTheNameLocalhost(string configured, string expected)
    {
        var endpoint = new LocalOllamaEndpoint(Options.Create(new OllamaOptions { BaseUrl = configured }));

        endpoint.BaseUrl.Should().Be(expected);
    }

    [Fact]
    public void BaseUrl_DefaultOptionsUseTheNumericAddress()
    {
        var endpoint = new LocalOllamaEndpoint(Options.Create(new OllamaOptions()));

        endpoint.BaseUrl.Should().Be("http://127.0.0.1:11434");
    }
}
