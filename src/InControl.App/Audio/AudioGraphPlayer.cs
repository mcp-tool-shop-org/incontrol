using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WinRT;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using InControl.Services.Voice;

namespace InControl.App.Audio;

/// <summary>
/// PCM audio player using Windows.Media.Audio.AudioGraph.
/// Accepts raw PCM16 mono chunks and plays them through the default speaker.
/// Thread-safe: SubmitSamples can be called from any thread.
/// </summary>
public sealed class AudioGraphPlayer : IAudioPlayer
{
    private AudioGraph? _audioGraph;
    private AudioDeviceOutputNode? _outputNode;
    private AudioFrameInputNode? _inputNode;
    private readonly object _lock = new();
    private readonly ConcurrentQueue<byte[]> _pendingFrames = new();
    private bool _disposed;
    private int _currentSampleRate;

    /// <inheritdoc />
    public bool IsPlaying { get; private set; }

    /// <inheritdoc />
    public double Volume { get; set; } = 0.8;

    /// <inheritdoc />
    public event EventHandler? PlaybackStarted;

    /// <inheritdoc />
    public event EventHandler? PlaybackStopped;

    /// <inheritdoc />
    public async Task InitializeAsync(int sampleRate, int channels = 1, int bitsPerSample = 16)
    {
        // If already initialized with same format, reuse
        if (_audioGraph is not null && _currentSampleRate == sampleRate)
            return;

        // Tear down existing graph
        await DisposeGraphAsync();

        _currentSampleRate = sampleRate;

        var settings = new AudioGraphSettings(AudioRenderCategory.Speech)
        {
            EncodingProperties = AudioEncodingProperties.CreatePcm(
                (uint)sampleRate,
                (uint)channels,
                (uint)bitsPerSample),
            DesiredSamplesPerQuantum = 4800, // 200ms at 24kHz — larger for smoother playback
            QuantumSizeSelectionMode = QuantumSizeSelectionMode.ClosestToDesired
        };

        var result = await AudioGraph.CreateAsync(settings);
        if (result.Status != AudioGraphCreationStatus.Success)
            throw new InvalidOperationException(
                $"Failed to create AudioGraph: {result.Status} - {result.ExtendedError?.Message}");

        _audioGraph = result.Graph;

        var outputResult = await _audioGraph.CreateDeviceOutputNodeAsync();
        if (outputResult.Status != AudioDeviceNodeCreationStatus.Success)
            throw new InvalidOperationException(
                $"Failed to create output node: {outputResult.Status} - {outputResult.ExtendedError?.Message}");

        _outputNode = outputResult.DeviceOutputNode;

        var encoding = AudioEncodingProperties.CreatePcm(
            (uint)sampleRate,
            (uint)channels,
            (uint)bitsPerSample);

        _inputNode = _audioGraph.CreateFrameInputNode(encoding);
        _inputNode.AddOutgoingConnection(_outputNode);

        _audioGraph.QuantumStarted += OnQuantumStarted;
        _audioGraph.Start();
        _inputNode.Start();

        IsPlaying = true;
        PlaybackStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void SubmitSamples(byte[] pcmData)
    {
        if (_disposed || _inputNode is null)
            return;

        // Queue the data — it will be drained on the AudioGraph quantum thread
        _pendingFrames.Enqueue(pcmData);
    }

    private void OnQuantumStarted(AudioGraph sender, object args)
    {
        if (_inputNode is null || _disposed)
            return;

        _inputNode.OutgoingGain = Volume;

        // Consolidate all pending chunks into one large frame to avoid choppy playback
        if (_pendingFrames.IsEmpty)
            return;

        var totalSize = 0;
        var chunks = new List<byte[]>();
        while (_pendingFrames.TryDequeue(out var pcmData))
        {
            chunks.Add(pcmData);
            totalSize += pcmData.Length;
        }

        if (totalSize == 0)
            return;

        // Merge into a single contiguous buffer
        var merged = new byte[totalSize];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, merged, offset, chunk.Length);
            offset += chunk.Length;
        }

        var frame = CreateAudioFrame(merged);
        _inputNode.AddFrame(frame);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        lock (_lock)
        {
            if (_inputNode is not null)
            {
                _inputNode.Stop();
            }
            if (_audioGraph is not null)
            {
                _audioGraph.Stop();
            }
        }

        await DisposeGraphAsync();

        IsPlaying = false;
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return new ValueTask(DisposeGraphAsync());
    }

    private Task DisposeGraphAsync()
    {
        lock (_lock)
        {
            _inputNode?.Dispose();
            _inputNode = null;
            _outputNode?.Dispose();
            _outputNode = null;
            _audioGraph?.Dispose();
            _audioGraph = null;
        }
        return Task.CompletedTask;
    }

    private static unsafe AudioFrame CreateAudioFrame(byte[] pcmData)
    {
        var frame = new AudioFrame((uint)pcmData.Length);

        using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
        using (var reference = buffer.CreateReference())
        {
            // Use CsWinRT's .As<T>() to QueryInterface through ComWrappers-aware path.
            // Direct casts and Marshal.QueryInterface do NOT work with CsWinRT projections.
            var byteAccess = reference.As<IMemoryBufferByteAccess>();
            byteAccess.GetBuffer(out byte* dataInBytes, out uint capacity);

            var copyLen = Math.Min(pcmData.Length, (int)capacity);
            Marshal.Copy(pcmData, 0, (nint)dataInBytes, copyLen);
        }

        return frame;
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
