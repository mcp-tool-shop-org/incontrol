using Microsoft.Extensions.Logging;

namespace InControl.Services.Voice;

/// <summary>
/// Tries the primary voice engine first (Kokoro), and falls back to Windows TTS when the primary
/// engine can't connect or errors at runtime.
/// </summary>
public sealed class FallbackVoiceService : IVoiceService
{
    private readonly KokoroVoiceService _primary;
    private readonly WindowsVoiceService _fallback;
    private readonly ILogger<FallbackVoiceService> _logger;

    public FallbackVoiceService(
        KokoroVoiceService primary,
        WindowsVoiceService fallback,
        ILogger<FallbackVoiceService> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;

        // Bubble events from whichever engine is active.
        _primary.ConnectionStateChanged += (_, _) => ConnectionStateChanged?.Invoke(this, ConnectionState);
        _fallback.ConnectionStateChanged += (_, _) => ConnectionStateChanged?.Invoke(this, ConnectionState);

        _primary.SpeakingStarted += (_, _) => SpeakingStarted?.Invoke(this, EventArgs.Empty);
        _primary.SpeakingStopped += (_, _) => SpeakingStopped?.Invoke(this, EventArgs.Empty);
        _fallback.SpeakingStarted += (_, _) => SpeakingStarted?.Invoke(this, EventArgs.Empty);
        _fallback.SpeakingStopped += (_, _) => SpeakingStopped?.Invoke(this, EventArgs.Empty);
    }

    private IVoiceService Active =>
        _primary.ConnectionState == VoiceConnectionState.Connected ? _primary : _fallback;

    public VoiceConnectionState ConnectionState =>
        _primary.ConnectionState == VoiceConnectionState.Connected
            ? _primary.ConnectionState
            : _fallback.ConnectionState;

    public event EventHandler<VoiceConnectionState>? ConnectionStateChanged;

    public bool IsSpeaking => Active.IsSpeaking;

    public event EventHandler? SpeakingStarted;
    public event EventHandler? SpeakingStopped;

    public IReadOnlyList<string> AvailableVoices => Active.AvailableVoices;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Try primary first
        await _primary.ConnectAsync(ct);

        if (_primary.ConnectionState == VoiceConnectionState.Connected)
            return;

        _logger.LogInformation("Primary voice engine unavailable (state={State}); using Windows TTS fallback", _primary.ConnectionState);

        await _fallback.ConnectAsync(ct);
    }

    public async Task SpeakAsync(string text, string? voice = null, CancellationToken ct = default)
    {
        // Ensure we have some engine connected
        if (ConnectionState != VoiceConnectionState.Connected)
            await ConnectAsync(ct);

        // Try primary if connected
        if (_primary.ConnectionState == VoiceConnectionState.Connected)
        {
            try
            {
                await _primary.SpeakAsync(text, voice, ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary TTS failed; switching to Windows TTS fallback");
            }
        }

        await _fallback.SpeakAsync(text, voice, ct);
    }

    public async Task StopSpeakingAsync(CancellationToken ct = default)
    {
        try { await _primary.StopSpeakingAsync(ct); } catch { /* ignore */ }
        try { await _fallback.StopSpeakingAsync(ct); } catch { /* ignore */ }
    }
}
