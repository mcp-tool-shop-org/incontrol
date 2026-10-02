namespace InControl.Services.Voice;

/// <summary>
/// Voice connection state.
/// </summary>
public enum VoiceConnectionState
{
    /// <summary>Not yet attempted connection.</summary>
    Disconnected,

    /// <summary>Currently connecting to the engine.</summary>
    Connecting,

    /// <summary>Connected and ready to speak.</summary>
    Connected,

    /// <summary>Connection failed or lost.</summary>
    Error
}

/// <summary>
/// Service for voice synthesis via the local TTS engine.
/// </summary>
public interface IVoiceService
{
    /// <summary>
    /// Current connection state.
    /// </summary>
    VoiceConnectionState ConnectionState { get; }

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    event EventHandler<VoiceConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Whether audio is currently being spoken.
    /// </summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Raised when speaking starts.
    /// </summary>
    event EventHandler? SpeakingStarted;

    /// <summary>
    /// Raised when speaking stops.
    /// </summary>
    event EventHandler? SpeakingStopped;

    /// <summary>
    /// Available voice identifiers from the engine.
    /// </summary>
    IReadOnlyList<string> AvailableVoices { get; }

    /// <summary>
    /// Initializes the voice engine and caches available voices.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Speaks the given text using the specified or default voice.
    /// Stops any currently playing speech first.
    /// </summary>
    /// <param name="text">Text to speak.</param>
    /// <param name="voice">Voice ID override (uses default if null).</param>
    /// <param name="ct">Cancellation token.</param>
    Task SpeakAsync(string text, string? voice = null, CancellationToken ct = default);

    /// <summary>
    /// Stops any currently playing speech.
    /// </summary>
    Task StopSpeakingAsync(CancellationToken ct = default);
}
