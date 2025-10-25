using System;

namespace AudioVisualization.Audio;

public sealed class AudioDeviceInfo
{
    public AudioDeviceInfo(string id, string displayName, AudioInputCaptureMode captureMode, bool isDefault)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        CaptureMode = captureMode;
        IsDefault = isDefault;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public AudioInputCaptureMode CaptureMode { get; }
    public bool IsDefault { get; }

    public override string ToString() => DisplayName;
}
