using System;

namespace AudioVisualization.Audio;

public interface IAudioInputSource : IDisposable
{
    event EventHandler<AudioBufferReadyEventArgs>? AudioBufferReady;

    bool IsRunning { get; }

    void Start();

    void Stop();
}
