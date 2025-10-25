using System;

namespace AudioVisualization.Audio;

public static class AudioInputSourceFactory
{
    public static IAudioInputSource CreateDefault(AudioInputOptions? options = null)
    {
        options ??= AudioInputOptions.DefaultLoopback();

        if (OperatingSystem.IsWindows())
        {
            return new WindowsWasapiAudioInputSource(options);
        }

        return new UnsupportedAudioInputSource("Audio capture is not yet implemented for this platform.");
    }
}
