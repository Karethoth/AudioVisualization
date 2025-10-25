using System;

namespace AudioVisualization.Audio;

public sealed class AudioBufferReadyEventArgs : EventArgs
{
    public AudioBufferReadyEventArgs(float[] buffer, int sampleCount, int sampleRate, int channels)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        SampleCount = sampleCount;
        SampleRate = sampleRate;
        Channels = channels;
    }

    public float[] Buffer { get; }
    public int SampleCount { get; }
    public int SampleRate { get; }
    public int Channels { get; }

    public TimeSpan Duration => SampleRate <= 0 || Channels <= 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds((double)SampleCount / (SampleRate * Channels));
}
