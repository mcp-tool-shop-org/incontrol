namespace InControl.Core.Configuration;

/// <summary>
/// Configuration options for the voice engine.
/// </summary>
public sealed class VoiceOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Voice";

    /// <summary>
    /// Whether to automatically speak assistant responses.
    /// </summary>
    public bool AutoSpeak { get; set; } = true;

    /// <summary>
    /// Default voice identifier (e.g. "af_bella").
    /// </summary>
    public string DefaultVoice { get; set; } = "af_bella";

    /// <summary>
    /// Audio volume (0.0 to 1.0).
    /// </summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>
    /// Speech speed multiplier (0.5 to 2.0).
    /// </summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>
    /// Whether to use GPU acceleration (DirectML) when available.
    /// Falls back to CPU if GPU is not available.
    /// </summary>
    public bool UseGpu { get; set; } = true;

    /// <summary>
    /// Optional custom path to the ONNX model file.
    /// When null, uses the model bundled with the application.
    /// </summary>
    public string? ModelPath { get; set; }
}
