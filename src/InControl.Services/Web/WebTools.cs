using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using InControl.Core.Models;

namespace InControl.Services.Web;

/// <summary>
/// A search result from the web search provider.
/// </summary>
public sealed record WebSearchResult(string Title, string Url, string Snippet);

/// <summary>
/// The web tools a model may use when the person turns web search on:
/// a DuckDuckGo search, and a reader for one page.
/// </summary>
public sealed partial class WebTools
{
    private const int MaxResults = 6;
    private const int MaxPageChars = 8000;
    private const long MaxPageBytes = 2 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly ILogger<WebTools> _logger;

    public WebTools(ILogger<WebTools> logger, HttpMessageHandler? handler = null)
    {
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) InControl/2.0");
    }

    /// <summary>
    /// The tools to offer the model.
    /// </summary>
    public IReadOnlyList<ChatTool> Tools =>
    [
        new ChatTool(
            "web_search",
            "Search the web with DuckDuckGo. Use it for current events, recent releases, or anything you are not sure of. Returns titles, links and short snippets. Cite the links you use.",
            [new ChatToolParameter("query", "What to search for.")],
            (args, ct) => SearchAsTextAsync(args.GetValueOrDefault("query") ?? string.Empty, ct),
            args => $"Searched the web: {args.GetValueOrDefault("query")}"),
        new ChatTool(
            "read_web_page",
            "Read the text of one public web page, for example a link from web_search. Returns the page text, shortened if long.",
            [new ChatToolParameter("url", "The full http or https address of the page.")],
            (args, ct) => ReadPageAsync(args.GetValueOrDefault("url") ?? string.Empty, ct),
            args => $"Read {HostOf(args.GetValueOrDefault("url"))}")
    ];

    /// <summary>
    /// Searches DuckDuckGo and formats the results for the model.
    /// </summary>
    public async Task<string> SearchAsTextAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "The search query was empty.";

        // The query itself is not logged. It may carry what the person asked.
        _logger.LogDebug("Web search, {Length} characters", query.Length);
        var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query.Trim());
        var html = await _http.GetStringAsync(url, ct);
        var results = ParseDuckDuckGo(html);
        if (results.Count == 0)
            return $"No results for \"{query}\".";

        var sb = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.Append(i + 1).Append(". ").Append(r.Title).Append('\n')
              .Append(r.Url).Append('\n');
            if (r.Snippet.Length > 0)
                sb.Append(r.Snippet).Append('\n');
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Reads one public page as plain text. Local and private network addresses are refused.
    /// </summary>
    public async Task<string> ReadPageAsync(string address, CancellationToken ct)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return $"\"{address}\" is not an http or https address.";
        }

        if (!await IsPublicHostAsync(uri, ct))
            return $"{uri.Host} is on this PC or a private network. InControl only reads public pages.";

        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            return $"{uri} answered {(int)response.StatusCode} {response.ReasonPhrase}.";

        // A redirect may land somewhere private. Check where it ended up.
        var final = response.RequestMessage?.RequestUri ?? uri;
        if (final != uri && !await IsPublicHostAsync(final, ct))
            return $"{uri} redirected to a private address, so it was not read.";

        var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (type.Length > 0 && !type.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            && !type.Contains("html", StringComparison.OrdinalIgnoreCase)
            && !type.Contains("json", StringComparison.OrdinalIgnoreCase)
            && !type.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return $"{uri} is {type}, not a text page.";
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= MaxPageBytes)
                break;
        }

        var raw = Encoding.UTF8.GetString(buffer.ToArray());
        var text = type.Contains("html", StringComparison.OrdinalIgnoreCase) || raw.TrimStart().StartsWith('<')
            ? HtmlToText(raw)
            : raw;

        if (text.Length > MaxPageChars)
            text = text[..MaxPageChars] + "\n[Page shortened.]";

        return text.Length == 0 ? $"{uri} has no readable text." : $"{final}\n\n{text}";
    }

    /// <summary>
    /// Pulls results out of DuckDuckGo's HTML results page.
    /// </summary>
    public static IReadOnlyList<WebSearchResult> ParseDuckDuckGo(string html)
    {
        var results = new List<WebSearchResult>();
        var titles = ResultTitle().Matches(html);
        foreach (Match title in titles)
        {
            if (results.Count >= MaxResults)
                break;

            var url = RealUrl(WebUtility.HtmlDecode(title.Groups["href"].Value));
            if (url is null || url.Contains("duckduckgo.com/y.js", StringComparison.Ordinal))
                continue; // Ads.

            // The snippet sits after its title, before the next title.
            var next = title.NextMatch();
            var end = next.Success ? next.Index : html.Length;
            var snippetMatch = ResultSnippet().Match(html, title.Index, end - title.Index);
            var snippet = snippetMatch.Success ? Clean(snippetMatch.Groups["text"].Value) : string.Empty;

            results.Add(new WebSearchResult(Clean(title.Groups["text"].Value), url, snippet));
        }

        return results;
    }

    /// <summary>
    /// Plain text from HTML: scripts and styles removed, block tags as line breaks, entities decoded.
    /// </summary>
    public static string HtmlToText(string html)
    {
        var text = NonContent().Replace(html, " ");
        text = BlockTag().Replace(text, "\n");
        text = AnyTag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = SpaceRun().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>
    /// True when every address the host resolves to is public, not loopback, private or link-local.
    /// </summary>
    internal static async Task<bool> IsPublicHostAsync(Uri uri, CancellationToken ct)
    {
        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        return addresses.Length > 0 && addresses.All(IsPublic);
    }

    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast);

        var b = address.GetAddressBytes();
        return !(b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] == 0
            || b[0] >= 224);
    }

    private static string? RealUrl(string href)
    {
        if (href.StartsWith("//", StringComparison.Ordinal))
            href = "https:" + href;

        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
            return null;

        // DuckDuckGo wraps each result in a redirect that carries the real address in uddg.
        if (uri.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath == "/l/")
        {
            foreach (var part in uri.Query.TrimStart('?').Split('&'))
            {
                if (part.StartsWith("uddg=", StringComparison.Ordinal))
                    return Uri.UnescapeDataString(part["uddg=".Length..]);
            }

            return null;
        }

        return uri.ToString();
    }

    private static string Clean(string fragment) =>
        SpaceRun().Replace(WebUtility.HtmlDecode(AnyTag().Replace(fragment, string.Empty)), " ").Trim();

    private static string HostOf(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url ?? "a page";

    [GeneratedRegex("""<a[^>]*class="result__a"[^>]*href="(?<href>[^"]*)"[^>]*>(?<text>.*?)</a>""", RegexOptions.Singleline)]
    private static partial Regex ResultTitle();

    [GeneratedRegex("""class="result__snippet"[^>]*>(?<text>.*?)</a>""", RegexOptions.Singleline)]
    private static partial Regex ResultSnippet();

    [GeneratedRegex("""<(script|style|noscript|svg|head)\b.*?</\1>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex NonContent();

    [GeneratedRegex("""<(br|/p|/div|/li|/h[1-6]|/tr|/section|/article|/pre)\b[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex("[ \t\r\f\v]+")]
    private static partial Regex SpaceRun();

    [GeneratedRegex("""\n\s*\n+""")]
    private static partial Regex BlankLines();
}
