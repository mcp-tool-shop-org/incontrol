namespace InControl.Core.Compute;

/// <summary>
/// GET <c>http://127.0.0.1:&lt;port&gt;/api/version</c>. Redirects are off.
/// Any other host is refused before a request is sent.
/// </summary>
public sealed class HttpOllamaReadyProbe : IOllamaReadyProbe
{
    private readonly HttpClient _http;

    public HttpOllamaReadyProbe(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler { AllowAutoRedirect = false };
        _http = new HttpClient(handler, disposeHandler: handler is HttpClientHandler)
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
    }

    public async Task<string?> VersionAsync(string baseUrl, CancellationToken cancellationToken)
    {
        if (!TunnelPort.IsLoopbackProbe(baseUrl))
        {
            return null;
        }

        var root = baseUrl.TrimEnd('/');
        try
        {
            using var response = await _http.GetAsync(root + "/api/version", cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (body.Length > 512)
            {
                body = body[..512];
            }

            return OllamaVersion.Read(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }
}
