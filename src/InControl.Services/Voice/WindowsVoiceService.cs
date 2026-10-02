using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InControl.Core.Configuration;

namespace InControl.Services.Voice;

/// <summary>
/// Voice synthesis service using Windows built-in SAPI voices (System.Speech).
/// Used as a safe fallback when the primary engine is unavailable.
/// </summary>
public sealed class WindowsVoiceService : IVoiceService, IDisposable
{
    private readonly IOptions<VoiceOptions> _options;
    private readonly ILogger<WindowsVoiceService> _logger;
    private readonly SpeechSynthesizer _synth = new();

    private VoiceConnectionState _connectionState = VoiceConnectionState.Disconnected;
    private bool _isSpeaking;
    private List<string> _availableVoices = [];

    public WindowsVoiceService(IOptions<VoiceOptions> options, ILogger<WindowsVoiceService> logger)
    {
        _options = options;
        _logger = logger;

        _synth.SpeakStarted += (_, _) => IsSpeaking = true;
        _synth.SpeakCompleted += (_, _) => IsSpeaking = false;
    }

    public VoiceConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState != value)
            {
                _connectionState = value;
                ConnectionStateChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<VoiceConnectionState>? ConnectionStateChanged;

    public bool IsSpeaking
    {
        get => _isSpeaking;
        private set
        {
            if (_isSpeaking != value)
            {
                _isSpeaking = value;
                if (value)
                    SpeakingStarted?.Invoke(this, EventArgs.Empty);
                else
                    SpeakingStopped?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? SpeakingStarted;
    public event EventHandler? SpeakingStopped;

    public IReadOnlyList<string> AvailableVoices => _availableVoices;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (ConnectionState == VoiceConnectionState.Connected)
            return Task.CompletedTask;

        ConnectionState = VoiceConnectionState.Connecting;

        try
        {
            _availableVoices = _synth.GetInstalledVoices()
                .Select(v => v.VoiceInfo.Name)
                .Distinct()
                .Order()
                .ToList();

            ConnectionState = VoiceConnectionState.Connected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Windows TTS (System.Speech)");
            ConnectionState = VoiceConnectionState.Error;
        }

        return Task.CompletedTask;
    }

    public async Task SpeakAsync(string text, string? voice = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (ConnectionState != VoiceConnectionState.Connected)
        {
            await ConnectAsync(ct);
            if (ConnectionState != VoiceConnectionState.Connected)
                return;
        }

        // Cancel/stop any current speech
        await StopSpeakingAsync(ct);

        var opts = _options.Value;

        // Volume: System.Speech expects 0..100
        _synth.Volume = (int)Math.Round(Math.Clamp(opts.Volume, 0f, 1f) * 100f);

        // Rate: -10..10. Map speed 0.5..2.0 to roughly -5..+5.
        _synth.Rate = SpeedToRate(opts.Speed);

        // Voice selection: best-effort; ignore if not found
        if (!string.IsNullOrWhiteSpace(voice))
        {
            try
            {
                _synth.SelectVoice(voice);
            }
            catch
            {
                // Ignore; fall back to default voice
            }
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<SpeakCompletedEventArgs>? completed = null;
        completed = (_, e) =>
        {
            _synth.SpeakCompleted -= completed;
            if (e.Cancelled)
                tcs.TrySetCanceled();
            else if (e.Error is not null)
                tcs.TrySetException(e.Error);
            else
                tcs.TrySetResult();
        };

        _synth.SpeakCompleted += completed;

        using var reg = ct.Register(() =>
        {
            try { _synth.SpeakAsyncCancelAll(); } catch { /* ignore */ }
        });

        try
        {
            _synth.SpeakAsync(text);
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Windows TTS speak failed");
        }
    }

    public Task StopSpeakingAsync(CancellationToken ct = default)
    {
        try
        {
            _synth.SpeakAsyncCancelAll();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Windows TTS stop failed");
        }

        IsSpeaking = false;
        return Task.CompletedTask;
    }

    private static int SpeedToRate(float speed)
    {
        speed = Math.Clamp(speed, 0.5f, 2.0f);

        // Map [0.5, 2.0] -> [-5, +5]
        var normalized = (speed - 1.0f) / 1.0f; // [-0.5, +1.0]
        var rate = (int)Math.Round(normalized * 5.0f);
        return Math.Clamp(rate, -10, 10);
    }

    public void Dispose()
    {
        try { _synth.Dispose(); } catch { /* ignore */ }
    }
}
