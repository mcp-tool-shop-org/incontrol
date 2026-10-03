namespace InControl.Core.Configuration;

/// <summary>
/// Configuration options for the Ollama backend.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Inference:Ollama";

    /// <summary>
    /// Base URL for the Ollama API.
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

    /// <summary>
    /// The address chat should use on this PC. A blank value falls back to 127.0.0.1, and the
    /// name localhost is rewritten to 127.0.0.1 because Windows resolves it to IPv6 first and
    /// then misses an Ollama that listens on IPv4 only.
    /// </summary>
    public string ResolveBaseUrl()
    {
        const string fallback = "http://127.0.0.1:11434";
        var url = BaseUrl?.Trim();
        if (string.IsNullOrEmpty(url))
            return fallback;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            return url;

        var rewritten = new UriBuilder(uri) { Host = "127.0.0.1" }.Uri.AbsoluteUri;
        return url.EndsWith('/') ? rewritten : rewritten.TrimEnd('/');
    }

    /// <summary>
    /// Whether to keep models loaded in memory between requests.
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// Keep-alive duration in minutes (0 = until explicit unload).
    /// </summary>
    public int KeepAliveMinutes { get; set; } = 5;

    /// <summary>
    /// Number of GPU layers to offload (-1 = all, 0 = CPU only).
    /// </summary>
    public int NumGpuLayers { get; set; } = -1;

    /// <summary>
    /// Context window size in tokens.
    /// </summary>
    public int ContextSize { get; set; } = 8192;

    /// <summary>
    /// Number of threads for CPU inference.
    /// </summary>
    public int? NumThreads { get; set; }
}
