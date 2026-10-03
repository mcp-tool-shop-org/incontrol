using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using InControl.Services.Web;
using Xunit;

namespace InControl.Services.Tests.Web;

public class WebToolsTests
{
    private static string Sample() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Web", "duckduckgo-sample.html"));

    /// <summary>
    /// Answers every request with one canned response and remembers what was asked.
    /// </summary>
    private sealed class CannedHandler(string body, string mediaType = "text/html") : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType),
                RequestMessage = request
            });
        }
    }

    [Fact]
    public void ParseDuckDuckGo_ReadsTitlesRealLinksAndSnippets()
    {
        var results = WebTools.ParseDuckDuckGo(Sample());

        results.Should().HaveCount(2);
        results[0].Url.Should().Be("https://github.com/mcp-tool-shop-org/incontrol");
        results[0].Title.Should().StartWith("GitHub - mcp-tool-shop-org/incontrol");
        results[0].Snippet.Should().Contain("Ollama chat for Windows");
        results[0].Snippet.Should().NotContain("<b>");
        results[1].Url.Should().Be("https://github.com/mcp-tool-shop-org");
    }

    [Fact]
    public void ParseDuckDuckGo_WithNoResults_IsEmpty()
    {
        WebTools.ParseDuckDuckGo("<html><body>No results.</body></html>").Should().BeEmpty();
    }

    [Fact]
    public async Task Search_SendsTheQueryToDuckDuckGo_AndNumbersTheResults()
    {
        var handler = new CannedHandler(Sample());
        var tools = new WebTools(NullLogger<WebTools>.Instance, handler);

        var text = await tools.SearchAsTextAsync("incontrol github", CancellationToken.None);

        handler.Requests.Single().AbsoluteUri.Should().Be("https://html.duckduckgo.com/html/?q=incontrol%20github");
        text.Should().StartWith("1. GitHub - mcp-tool-shop-org/incontrol");
        text.Should().Contain("\nhttps://github.com/mcp-tool-shop-org/incontrol\n");
        text.Should().Contain("2. ");
    }

    [Fact]
    public async Task Search_WithAnEmptyQuery_DoesNotGoOnline()
    {
        var handler = new CannedHandler(Sample());
        var tools = new WebTools(NullLogger<WebTools>.Instance, handler);

        (await tools.SearchAsTextAsync("  ", CancellationToken.None)).Should().Contain("empty");
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434/api/tags")]
    [InlineData("http://localhost:8080/")]
    [InlineData("http://192.168.1.10/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/")]
    public async Task ReadPage_RefusesThisPcAndPrivateNetworks(string url)
    {
        var handler = new CannedHandler("secret");
        var tools = new WebTools(NullLogger<WebTools>.Instance, handler);

        var text = await tools.ReadPageAsync(url, CancellationToken.None);

        text.Should().Contain("only reads public pages");
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.com/x")]
    [InlineData("not a url")]
    public async Task ReadPage_RefusesAnythingButHttp(string url)
    {
        var tools = new WebTools(NullLogger<WebTools>.Instance, new CannedHandler("x"));

        (await tools.ReadPageAsync(url, CancellationToken.None)).Should().Contain("not an http or https address");
    }

    [Fact]
    public async Task ReadPage_ReturnsThePageAsText()
    {
        var html = "<html><head><title>t</title><script>var x=1;</script></head><body><h1>Release notes</h1><p>Version 2.0 adds <b>attachments</b>.</p></body></html>";
        var tools = new WebTools(NullLogger<WebTools>.Instance, new CannedHandler(html));

        var text = await tools.ReadPageAsync("https://93.184.215.14/notes", CancellationToken.None);

        text.Should().Contain("Release notes");
        text.Should().Contain("Version 2.0 adds attachments.");
        text.Should().NotContain("var x");
        text.Should().NotContain("<p>");
    }

    [Fact]
    public async Task ReadPage_RefusesBinaryContent()
    {
        var tools = new WebTools(NullLogger<WebTools>.Instance, new CannedHandler("PK", "application/zip"));

        (await tools.ReadPageAsync("https://93.184.215.14/a.zip", CancellationToken.None)).Should().Contain("not a text page");
    }

    [Fact]
    public void HtmlToText_DropsStylesAndDecodesEntities()
    {
        WebTools.HtmlToText("<style>p{}</style><p>Fish &amp; chips</p><p>Two</p>").Should().Be("Fish & chips\nTwo");
    }

    [Fact]
    public void Tools_DescribeWhatTheyDid()
    {
        var tools = new WebTools(NullLogger<WebTools>.Instance, new CannedHandler("")).Tools;

        tools.Select(t => t.Name).Should().Equal("web_search", "read_web_page");
        tools[0].Describe(new Dictionary<string, string> { ["query"] = "ollama" }).Should().Be("Searched the web: ollama");
        tools[1].Describe(new Dictionary<string, string> { ["url"] = "https://github.com/x" }).Should().Be("Read github.com");
    }
}
