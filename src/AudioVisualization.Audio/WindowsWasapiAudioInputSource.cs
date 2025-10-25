using System;
using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioVisualization.Audio;

internal sealed class WindowsWasapiAudioInputSource : IAudioInputSource
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");

    private readonly AudioInputOptions _options;
    private readonly object _gate = new();
    private WasapiCapture? _capture;
    private bool _disposed;

    public WindowsWasapiAudioInputSource(AudioInputOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public event EventHandler<AudioBufferReadyEventArgs>? AudioBufferReady;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        EnsureNotDisposed();

        lock (_gate)
        {
            if (IsRunning)
            {
                return;
            }

            _capture = CreateCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            _capture.StartRecording();
            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!IsRunning || _capture is null)
            {
                return;
            }

            try
            {
                _capture.StopRecording();
            }
            catch
            {
                CleanupCapture();
                throw;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_gate)
        {
            if (_capture is null)
            {
                return;
            }

            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;

            if (IsRunning)
            {
                try
                {
                    _capture.StopRecording();
                }
                catch
                {
                    // Ignore failures during shutdown; capture will still be disposed.
                }
            }

            _capture.Dispose();
            _capture = null;
            IsRunning = false;
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_gate)
        {
            CleanupCapture();
        }

        if (e.Exception is not null && !_disposed)
        {
            throw new InvalidOperationException("Audio capture stopped unexpectedly.", e.Exception);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        WasapiCapture? capture;
        lock (_gate)
        {
            capture = _capture;
        }

        if (capture is null)
        {
            return;
        }

        var format = capture.WaveFormat;
        var buffer = ConvertToFloatBuffer(format, e.Buffer, e.BytesRecorded, out var sampleCount);

        if (buffer.Length == 0 || sampleCount <= 0)
        {
            return;
        }

        AudioBufferReady?.Invoke(this, new AudioBufferReadyEventArgs(buffer, sampleCount, format.SampleRate, format.Channels));
    }

    private static float[] ConvertToFloatBuffer(WaveFormat format, byte[] source, int bytesRecorded, out int sampleCount)
    {
        if (bytesRecorded <= 0)
        {
            sampleCount = 0;
            return Array.Empty<float>();
        }

        var bitsPerSample = format.BitsPerSample;
        if (format is WaveFormatExtensible extFormat && extFormat.BitsPerSample > 0)
        {
            bitsPerSample = extFormat.BitsPerSample;
        }

        if (bitsPerSample <= 0)
        {
            sampleCount = 0;
            return Array.Empty<float>();
        }

        var bytesPerSample = bitsPerSample / 8;
        if (bytesPerSample <= 0)
        {
            sampleCount = 0;
            return Array.Empty<float>();
        }

        sampleCount = bytesRecorded / bytesPerSample;
        if (sampleCount <= 0)
        {
            sampleCount = 0;
            return Array.Empty<float>();
        }

        var usableBytes = sampleCount * bytesPerSample;

        switch (format.Encoding)
        {
            case WaveFormatEncoding.IeeeFloat:
                var floatCount = usableBytes / sizeof(float);
                var floatBuffer = new float[floatCount];
                Buffer.BlockCopy(source, 0, floatBuffer, 0, floatCount * sizeof(float));
                sampleCount = floatCount;
                return floatBuffer;

            case WaveFormatEncoding.Pcm:
            {
                var pcmBuffer = ConvertPcmToFloat(bitsPerSample, source, sampleCount);
                sampleCount = pcmBuffer.Length;
                return pcmBuffer;
            }

            case WaveFormatEncoding.Extensible when format is WaveFormatExtensible extensible:
                if (extensible.SubFormat == FloatSubFormat)
                {
                    var extFloatCount = usableBytes / sizeof(float);
                    var extFloatBuffer = new float[extFloatCount];
                    Buffer.BlockCopy(source, 0, extFloatBuffer, 0, extFloatCount * sizeof(float));
                    sampleCount = extFloatCount;
                    return extFloatBuffer;
                }

                if (extensible.SubFormat == PcmSubFormat)
                {
                    var extPcmBuffer = ConvertPcmToFloat(extensible.BitsPerSample, source, sampleCount);
                    sampleCount = extPcmBuffer.Length;
                    return extPcmBuffer;
                }

                sampleCount = 0;
                return Array.Empty<float>();

            default:
                sampleCount = 0;
                return Array.Empty<float>();
        }
    }

    private static float[] ConvertPcmToFloat(int bitsPerSample, byte[] source, int sampleCount)
        => bitsPerSample switch
        {
            8 => ConvertPcm8ToFloat(source, sampleCount),
            16 => ConvertPcm16ToFloat(source, sampleCount),
            24 => ConvertPcm24ToFloat(source, sampleCount),
            32 => ConvertPcm32ToFloat(source, sampleCount),
            _ => Array.Empty<float>()
        };

    private static float[] ConvertPcm8ToFloat(byte[] source, int sampleCount)
    {
        var result = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var value = source[i] - 128;
            result[i] = value / 128f;
        }

        return result;
    }

    private static float[] ConvertPcm16ToFloat(byte[] source, int sampleCount)
    {
        var result = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(i * 2));
            result[i] = sample / 32768f;
        }

        return result;
    }

    private static float[] ConvertPcm24ToFloat(byte[] source, int sampleCount)
    {
        var result = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var offset = i * 3;
            var sample = source[offset] | (source[offset + 1] << 8) | (source[offset + 2] << 16);

            if ((sample & 0x800000) != 0)
            {
                sample |= unchecked((int)0xFF000000);
            }

            result[i] = sample / 8388608f;
        }

        return result;
    }

    private static float[] ConvertPcm32ToFloat(byte[] source, int sampleCount)
    {
        var result = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(i * 4));
            result[i] = sample / 2147483648f;
        }

        return result;
    }

    private WasapiCapture CreateCapture()
    {
        var enumerator = new MMDeviceEnumerator();
        MMDevice device;

        if (!string.IsNullOrWhiteSpace(_options.DeviceId))
        {
            device = enumerator.GetDevice(_options.DeviceId);
        }
        else
        {
            var flow = _options.CaptureMode == AudioInputCaptureMode.Loopback ? DataFlow.Render : DataFlow.Capture;
            device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
        }

        return _options.CaptureMode == AudioInputCaptureMode.Loopback
            ? new WasapiLoopbackCapture(device)
            : new WasapiCapture(device);
    }

    private void CleanupCapture()
    {
        var capture = _capture;
        if (capture is null)
        {
            return;
        }

        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;
        capture.Dispose();

        _capture = null;
        IsRunning = false;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsWasapiAudioInputSource));
        }
    }
}
