namespace InControl.Services.Voice;

/// <summary>
/// Platform-agnostic interface for playing PCM audio chunks.
/// </summary>
public interface IAudioPlayer : IAsyncDisposable
{
    /// <summary>
    /// Whether audio is currently playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Audio volume (0.0 to 1.0).
    /// </summary>
    double Volume { get; set; }

    /// <summary>
    /// Raised when playback starts.
    /// </summary>
    event EventHandler? PlaybackStarted;

    /// <summary>
    /// Raised when playback stops.
    /// </summary>
    event EventHandler? PlaybackStopped;

    /// <summary>
    /// Initializes the audio pipeline for the given format.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (e.g. 24000).</param>
    /// <param name="channels">Number of channels (default 1 = mono).</param>
    /// <param name="bitsPerSample">Bits per sample (default 16).</param>
    Task InitializeAsync(int sampleRate, int channels = 1, int bitsPerSample = 16);

    /// <summary>
    /// Submits raw PCM samples for playback.
    /// </summary>
    /// <param name="pcmData">PCM audio bytes.</param>
    void SubmitSamples(byte[] pcmData);

    /// <summary>
    /// Stops playback and clears the audio buffer.
    /// </summary>
    Task StopAsync();
}
