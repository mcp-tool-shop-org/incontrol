using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using InControl.Core.Configuration;

namespace InControl.Services.Voice;

/// <summary>
/// Voice synthesis service using the KokoroSharp ONNX engine.
/// Runs fully in-process — no external servers or dependencies.
/// </summary>
public sealed class KokoroVoiceService : IVoiceService, IDisposable
{
    private readonly IOptions<VoiceOptions> _options;
    private readonly IAudioPlayer _audioPlayer;
    private readonly ILogger<KokoroVoiceService> _logger;
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private KokoroTTS? _engine;
    private CancellationTokenSource? _speakCts;
    private bool _audioInitialized;

    private VoiceConnectionState _connectionState = VoiceConnectionState.Disconnected;
    private bool _isSpeaking;
    private List<string> _availableVoices = [];

    private const int SampleRate = 24000;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public event EventHandler<VoiceConnectionState>? ConnectionStateChanged;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public event EventHandler? SpeakingStarted;

    /// <inheritdoc />
    public event EventHandler? SpeakingStopped;

    /// <inheritdoc />
    public IReadOnlyList<string> AvailableVoices => _availableVoices;

    public KokoroVoiceService(
        IOptions<VoiceOptions> options,
        IAudioPlayer audioPlayer,
        ILogger<KokoroVoiceService> logger)
    {
        _options = options;
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (ConnectionState == VoiceConnectionState.Connected)
            return;

        ConnectionState = VoiceConnectionState.Connecting;

        try
        {
            await EnsureEngineAsync(ct);
            ConnectionState = VoiceConnectionState.Connected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize voice engine");
            ConnectionState = VoiceConnectionState.Error;
        }
    }

    /// <inheritdoc />
    public async Task SpeakAsync(string text, string? voice = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Lazy-init engine if not connected yet
        if (ConnectionState != VoiceConnectionState.Connected)
        {
            await ConnectAsync(ct);
            if (ConnectionState != VoiceConnectionState.Connected)
            {
                _logger.LogDebug("Skipping speak — voice engine not available");
                return;
            }
        }

        await _speakLock.WaitAsync(ct);
        try
        {
            // Stop any current speech
            if (IsSpeaking)
                await StopSpeakingInternalAsync();

            var opts = _options.Value;
            var voiceName = voice ?? opts.DefaultVoice;

            _speakCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            IsSpeaking = true;

            _audioPlayer.Volume = opts.Volume;

            // Initialize audio player if not done yet
            if (!_audioInitialized)
            {
                await _audioPlayer.InitializeAsync(SampleRate);
                _audioInitialized = true;
            }

            _logger.LogInformation(
                "Speaking: voice={Voice}, speed={Speed}, length={Length}chars",
                voiceName, opts.Speed, text.Length);

            // Get the voice style
            var kokoroVoice = KokoroVoiceManager.GetVoice(voiceName);

            // Tokenize text
            var tokens = Tokenizer.Tokenize(text, "en-us");

            // Segment for streaming playback
            var segments = SegmentationSystem.SplitToSegments(tokens, new DefaultSegmentationConfig());

            // Create a TaskCompletionSource to await all segments
            var tcs = new TaskCompletionSource();
            var localCts = _speakCts;
            var segmentCount = segments.Count;
            var completedSegments = 0;

            // Create job with callback that routes audio to our player
            var job = KokoroJob.Create(segments, kokoroVoice, opts.Speed, samples =>
            {
                try
                {
                    if (localCts?.IsCancellationRequested == true)
                        return;

                    _logger.LogDebug("Audio segment received: {SampleCount} samples", samples.Length);

                    var pcmBytes = FloatToPcm16(samples);
                    _audioPlayer.SubmitSamples(pcmBytes);

                    var done = Interlocked.Increment(ref completedSegments);
                    if (done >= segmentCount)
                    {
                        _logger.LogDebug("All {Count} segments complete", segmentCount);
                        tcs.TrySetResult();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in audio callback");
                    tcs.TrySetException(ex);
                }
            });

            // Enqueue the job
            _engine!.EnqueueJob(job);

            // Wait for completion or cancellation
            using var reg = localCts.Token.Register(() =>
            {
                job.Cancel();
                tcs.TrySetCanceled();
            });

            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Speech cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Speech failed");
            ConnectionState = VoiceConnectionState.Error;
            throw;
        }
        finally
        {
            IsSpeaking = false;
            _speakCts?.Dispose();
            _speakCts = null;
            _speakLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopSpeakingAsync(CancellationToken ct = default)
    {
        await _speakLock.WaitAsync(ct);
        try
        {
            await StopSpeakingInternalAsync();
        }
        finally
        {
            _speakLock.Release();
        }
    }

    private async Task StopSpeakingInternalAsync()
    {
        _speakCts?.Cancel();

        try
        {
            await _audioPlayer.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping audio player");
        }

        IsSpeaking = false;
    }

    /// <summary>
    /// Ensures the ONNX engine is loaded. Thread-safe, one-time initialization.
    /// </summary>
    private async Task EnsureEngineAsync(CancellationToken ct = default)
    {
        if (_engine is not null)
            return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_engine is not null)
                return;

            var opts = _options.Value;

            _logger.LogInformation("Loading voice engine (CPU)...");

            // Load voices from the bundled voices directory
            KokoroVoiceManager.LoadVoicesFromPath("voices");

            _engine = await LoadModelAsync(opts);

            // Populate available voices
            _availableVoices = KokoroVoiceManager.Voices
                .Select(v => v.Name)
                .Order()
                .ToList();

            _logger.LogInformation(
                "Voice engine loaded: {VoiceCount} voices available",
                _availableVoices.Count);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<KokoroTTS> LoadModelAsync(VoiceOptions opts)
    {
        if (!string.IsNullOrEmpty(opts.ModelPath))
        {
            return KokoroTTS.LoadModel(opts.ModelPath);
        }

        return await KokoroTTS.LoadModelAsync(
            KModel.float32,
            progress => _logger.LogDebug("Model download progress: {Progress:P0}", progress));
    }

    /// <summary>
    /// Converts float audio samples [-1.0, 1.0] to 16-bit PCM bytes.
    /// </summary>
    private static byte[] FloatToPcm16(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short pcm = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), pcm);
        }
        return bytes;
    }

    public void Dispose()
    {
        _speakCts?.Cancel();
        _speakCts?.Dispose();
        _speakLock.Dispose();
        _initLock.Dispose();
        _engine?.Dispose();
    }
}
