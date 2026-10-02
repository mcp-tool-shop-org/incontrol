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
    public string BaseUrl { get; set; } = "http://localhost:11434";

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
