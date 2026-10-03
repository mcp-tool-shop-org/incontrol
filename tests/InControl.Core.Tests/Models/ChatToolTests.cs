using FluentAssertions;
using InControl.Core.Models;
using Xunit;

namespace InControl.Core.Tests.Models;

public class ChatToolTests
{
    [Fact]
    public async Task Tool_KeepsItsNameAndRunsWithTheArguments()
    {
        var tool = new ChatTool(
            "web_search",
            "Search the web.",
            [
                new ChatToolParameter("query", "What to search for."),
                new ChatToolParameter("limit", "How many.", Required: false)
            ],
            (args, _) => Task.FromResult(args["query"]),
            args => $"Searched the web: {args["query"]}");

        tool.Name.Should().Be("web_search");
        tool.Description.Should().Be("Search the web.");
        tool.Parameters.Select(p => (p.Name, p.Description, p.Required)).Should().Equal(
            ("query", "What to search for.", true),
            ("limit", "How many.", false));

        var args = new Dictionary<string, string> { ["query"] = "ollama" };
        (await tool.Run(args, CancellationToken.None)).Should().Be("ollama");
        tool.Describe(args).Should().Be("Searched the web: ollama");
    }
}
