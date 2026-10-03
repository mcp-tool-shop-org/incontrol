namespace InControl.Core.Configuration;

/// <summary>
/// Configuration options for the inference layer.
/// </summary>
public sealed class InferenceOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Inference";

    /// <summary>
    /// The backend to use for inference (e.g., "Ollama", "LlamaCpp").
    /// </summary>
    public string Backend { get; set; } = "Ollama";

    /// <summary>
    /// Default model to use when none is specified.
    /// </summary>
    public string DefaultModel { get; set; } = "llama3.2";

    /// <summary>
    /// Default temperature for generation.
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.7;

    /// <summary>
    /// Default maximum tokens to generate.
    /// </summary>
    public int DefaultMaxTokens { get; set; } = 2048;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether models may search the web through DuckDuckGo. Off until the person turns it on,
    /// because a search query leaves this PC.
    /// </summary>
    public bool WebSearch { get; set; }

    /// <summary>
    /// Number of retry attempts for failed requests.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}
