namespace InControl.Core.Models;

/// <summary>
/// Information about an available LLM model.
/// </summary>
public sealed record ModelInfo
{
    /// <summary>
    /// The model identifier used for inference requests.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Model size in bytes (if known).
    /// </summary>
    public long? SizeBytes { get; init; }

    /// <summary>
    /// Parameter count (e.g., 7B, 13B).
    /// </summary>
    public string? ParameterCount { get; init; }

    /// <summary>
    /// Quantization level (e.g., Q4_K_M, Q8_0).
    /// </summary>
    public string? Quantization { get; init; }

    /// <summary>
    /// Model family or architecture (e.g., llama, mistral, phi).
    /// </summary>
    public string? Family { get; init; }

    /// <summary>
    /// When the model was last modified locally.
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>
    /// Gets the size in a human-readable format.
    /// </summary>
    public string SizeDisplay => SizeBytes switch
    {
        null => "Unknown",
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
