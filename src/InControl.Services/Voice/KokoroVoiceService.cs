using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using InControl.Core.Configuration;
using InControl.Core.Storage;

namespace InControl.Services.Voice;

/// <summary>
/// Voice synthesis service using the KokoroSharp ONNX engine in-process.
/// KokoroSharp.CPU supplies the native ONNX runtime the session is constructed with.
/// No external speech server is used. The model is loaded from the application cache
/// (the LocalApplicationData InControl root DataPaths already uses), not the process working directory.
/// </summary>
public sealed class KokoroVoiceService : IVoiceService, IDisposable
{
    private readonly IOptions<VoiceOptions> _options;
    private readonly IAudioPlayer _audioPlayer;
    private readonly ILogger<KokoroVoiceService> _logger;
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private readonly SemaphoreSlim _playerLock = new(1, 1);
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly object _stateGate = new();

    private KokoroTTS? _engine;
    private CancellationTokenSource? _speakCts;
    private KokoroJob? _activeJob;
    private int _playbackEpoch;
    private bool _audioInitialized;

    /// <summary>
    /// Relative file name KokoroSharp 0.6.2 writes for <see cref="KModel.float32"/>.
    /// </summary>
    private const string WorkingDirectoryModelFileName = "kokoro.onnx";

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

        // Claim this utterance and cancel the previous one before waiting on the setup lock,
        // so Stop and a second Speak are not stuck behind playback.
        var epoch = Interlocked.Increment(ref _playbackEpoch);
        CancelPublishedSpeech();
        await StopPlayerIfCurrentAsync(epoch);

        var lockHeld = false;
        CancellationTokenSource? speakCts = null;
        CancellationTokenRegistration registration = default;
        KokoroJob? job = null;
        try
        {
            await _speakLock.WaitAsync(ct);
            lockHeld = true;

            if (Volatile.Read(ref _playbackEpoch) != epoch)
            {
                ct.ThrowIfCancellationRequested();
                return;
            }

            await _playerLock.WaitAsync(ct);
            try
            {
                if (Volatile.Read(ref _playbackEpoch) != epoch)
                {
                    ct.ThrowIfCancellationRequested();
                    return;
                }

                if (!_audioInitialized)
                {
                    await _audioPlayer.InitializeAsync(SampleRate);
                    _audioInitialized = true;
                }
            }
            finally
            {
                _playerLock.Release();
            }

            if (Volatile.Read(ref _playbackEpoch) != epoch)
            {
                ct.ThrowIfCancellationRequested();
                return;
            }

            var opts = _options.Value;
            var voiceName = voice ?? opts.DefaultVoice;
            speakCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lock (_stateGate)
            {
                _speakCts = speakCts;
            }

            IsSpeaking = true;

            _audioPlayer.Volume = opts.Volume;

            _logger.LogInformation(
                "Speaking: voice={Voice}, speed={Speed}, length={Length}chars",
                voiceName, opts.Speed, text.Length);

            var kokoroVoice = KokoroVoiceManager.GetVoice(voiceName);
            var tokens = Tokenizer.Tokenize(text, "en-us");
            var segments = SegmentationSystem.SplitToSegments(tokens, new DefaultSegmentationConfig());

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var segmentCount = segments.Count;
            var completedSegments = 0;
            var localCts = speakCts;

            job = KokoroJob.Create(segments, kokoroVoice, opts.Speed, samples =>
            {
                try
                {
                    if (localCts.IsCancellationRequested || Volatile.Read(ref _playbackEpoch) != epoch)
                    {
                        tcs.TrySetCanceled();
                        return;
                    }

                    _logger.LogDebug("Audio segment received: {SampleCount} samples", samples.Length);
                    var pcmBytes = FloatToPcm16(samples);

                    // Hold the player lock so a stop or a newer speak cannot re-init between the check and the submit.
                    _playerLock.Wait();
                    try
                    {
                        if (localCts.IsCancellationRequested
                            || Volatile.Read(ref _playbackEpoch) != epoch
                            || !_audioInitialized)
                        {
                            tcs.TrySetCanceled();
                            return;
                        }

                        _audioPlayer.SubmitSamples(pcmBytes);
                    }
                    finally
                    {
                        _playerLock.Release();
                    }

                    if (Interlocked.Increment(ref completedSegments) >= segmentCount)
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

            registration = localCts.Token.Register(() =>
            {
                try
                {
                    job.Cancel();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error cancelling speech job");
                }

                tcs.TrySetCanceled();
                if (Volatile.Read(ref _playbackEpoch) == epoch)
                    _ = StopPlayerIfCurrentAsync(epoch);
            });

            lock (_stateGate)
            {
                _activeJob = job;
            }

            if (Volatile.Read(ref _playbackEpoch) != epoch || localCts.IsCancellationRequested)
            {
                job.Cancel();
                tcs.TrySetCanceled();
            }
            else
            {
                if (_engine is null)
                    throw new InvalidOperationException("Voice engine is not loaded.");

                _engine.EnqueueJob(job);
                if (segmentCount == 0)
                    tcs.TrySetResult();
            }

            // Playback wait must not hold the setup lock, or Stop cannot cancel this utterance.
            _speakLock.Release();
            lockHeld = false;

            await tcs.Task;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug("Speech cancelled");
            await StopPlayerIfCurrentAsync(epoch);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Speech cancelled");
            await StopPlayerIfCurrentAsync(epoch);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Speech failed");
            ConnectionState = VoiceConnectionState.Error;
            await StopPlayerIfCurrentAsync(epoch);
            throw;
        }
        finally
        {
            if (lockHeld)
                _speakLock.Release();

            registration.Dispose();
            var clearSpeaking = false;
            lock (_stateGate)
            {
                if (Volatile.Read(ref _playbackEpoch) == epoch)
                {
                    if (ReferenceEquals(_speakCts, speakCts))
                        _speakCts = null;
                    if (ReferenceEquals(_activeJob, job))
                        _activeJob = null;
                    clearSpeaking = true;
                }
            }

            if (clearSpeaking && Volatile.Read(ref _playbackEpoch) == epoch)
                IsSpeaking = false;

            speakCts?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task StopSpeakingAsync(CancellationToken ct = default)
    {
        // A cancelled caller token must not skip the stop. Playback is cleared either way.
        _ = ct;
        var epoch = Interlocked.Increment(ref _playbackEpoch);
        CancelPublishedSpeech();
        await StopPlayerIfCurrentAsync(epoch);
        if (Volatile.Read(ref _playbackEpoch) == epoch)
            IsSpeaking = false;
    }

    private void CancelPublishedSpeech()
    {
        CancellationTokenSource? cts;
        KokoroJob? job;
        lock (_stateGate)
        {
            cts = _speakCts;
            job = _activeJob;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The utterance already finished and disposed its source.
        }

        try
        {
            job?.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error cancelling speech job");
        }
    }

    private async Task StopPlayerIfCurrentAsync(int epoch)
    {
        await _playerLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _playbackEpoch) != epoch)
                return;

            try
            {
                await _audioPlayer.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error stopping audio player");
            }

            _audioInitialized = false;
        }
        finally
        {
            _playerLock.Release();
        }
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
        if (!string.IsNullOrWhiteSpace(opts.ModelPath))
            return KokoroTTS.LoadModel(Path.GetFullPath(opts.ModelPath));

        Directory.CreateDirectory(DataPaths.Cache);
        var cachePath = Path.GetFullPath(Path.Combine(DataPaths.Cache, WorkingDirectoryModelFileName));
        if (File.Exists(cachePath))
        {
            DeleteWorkingDirectoryCopy(cachePath);
            return KokoroTTS.LoadModel(cachePath);
        }

        // KokoroSharp 0.6.2 only downloads the float32 model beside the process.
        // Move that file into the app cache and load the absolute path.
        KokoroTTS? downloaded = null;
        try
        {
            downloaded = await KokoroTTS.LoadModelAsync(
                KModel.float32,
                progress => _logger.LogDebug("Model download progress: {Progress:P0}", progress));
        }
        finally
        {
            try
            {
                downloaded?.Dispose();
            }
            finally
            {
                MoveModelOutOfWorkingDirectory(cachePath);
            }
        }

        if (!File.Exists(cachePath))
            throw new FileNotFoundException("Kokoro model was not stored in the application cache.", cachePath);

        return KokoroTTS.LoadModel(cachePath);
    }

    private static void MoveModelOutOfWorkingDirectory(string cachePath)
    {
        var downloaded = Path.GetFullPath(WorkingDirectoryModelFileName);
        if (!File.Exists(downloaded))
            return;

        if (string.Equals(downloaded, cachePath, StringComparison.OrdinalIgnoreCase))
            return;

        var cacheDirectory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(cacheDirectory))
            Directory.CreateDirectory(cacheDirectory);

        File.Move(downloaded, cachePath, overwrite: true);
        DeleteWorkingDirectoryCopy(cachePath);
    }

    private static void DeleteWorkingDirectoryCopy(string cachePath)
    {
        var downloaded = Path.GetFullPath(WorkingDirectoryModelFileName);
        if (!File.Exists(downloaded))
            return;

        if (string.Equals(downloaded, cachePath, StringComparison.OrdinalIgnoreCase))
            return;

        File.Delete(downloaded);
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
        Interlocked.Increment(ref _playbackEpoch);
        try
        {
            CancelPublishedSpeech();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error cancelling speech during dispose");
        }

        _speakCts?.Dispose();
        _speakLock.Dispose();
        _playerLock.Dispose();
        _initLock.Dispose();
        _engine?.Dispose();
    }
}
