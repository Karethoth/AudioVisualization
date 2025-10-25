using System;

namespace AudioVisualization.Audio;

public sealed class AudioInputOptions
{
    public AudioInputCaptureMode CaptureMode { get; init; } = AudioInputCaptureMode.Loopback;
    public string? DeviceId { get; init; }
    public int? SampleRate { get; init; }
    public int? Channels { get; init; }
    public int BufferMilliseconds { get; init; } = 20;

    public static AudioInputOptions DefaultLoopback() => new() { CaptureMode = AudioInputCaptureMode.Loopback };

    public static AudioInputOptions DefaultMicrophone() => new() { CaptureMode = AudioInputCaptureMode.Microphone };

    public AudioInputOptions WithBufferMilliseconds(int bufferMilliseconds) => bufferMilliseconds <= 0
        ? throw new ArgumentOutOfRangeException(nameof(bufferMilliseconds), "Buffer duration must be positive")
        : new AudioInputOptions
        {
            CaptureMode = CaptureMode,
            DeviceId = DeviceId,
            SampleRate = SampleRate,
            Channels = Channels,
            BufferMilliseconds = bufferMilliseconds
        };
}
