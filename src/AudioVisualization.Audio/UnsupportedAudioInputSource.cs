using System;

namespace AudioVisualization.Audio;

internal sealed class UnsupportedAudioInputSource : IAudioInputSource
{
    private readonly string _reason;

    public UnsupportedAudioInputSource(string reason)
    {
        _reason = reason;
    }

    public event EventHandler<AudioBufferReadyEventArgs>? AudioBufferReady
    {
        add { }
        remove { }
    }

    public bool IsRunning => false;

    public void Start() => throw new PlatformNotSupportedException(_reason);

    public void Stop()
    {
        // Nothing to stop when the platform is unsupported.
    }

    public void Dispose()
    {
        // Nothing to dispose when the platform is unsupported.
    }
}
