using System;
using System.Collections.Generic;

namespace AudioVisualization.Audio;

public static class AudioInputDeviceService
{
    public static IReadOnlyList<AudioDeviceInfo> GetAvailableDevices(AudioInputCaptureMode captureMode)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsAudioInputDeviceService.GetDevices(captureMode);
        }

        return Array.Empty<AudioDeviceInfo>();
    }

    private static class WindowsAudioInputDeviceService
    {
        public static IReadOnlyList<AudioDeviceInfo> GetDevices(AudioInputCaptureMode captureMode)
        {
            var devices = new List<AudioDeviceInfo>();

            var dataFlow = captureMode == AudioInputCaptureMode.Loopback ? NAudio.CoreAudioApi.DataFlow.Render : NAudio.CoreAudioApi.DataFlow.Capture;

            using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();

            NAudio.CoreAudioApi.MMDevice? defaultDevice = null;
            try
            {
                defaultDevice = enumerator.GetDefaultAudioEndpoint(dataFlow, NAudio.CoreAudioApi.Role.Multimedia);
            }
            catch
            {
                // Ignore failures when resolving the default endpoint; continue with available devices.
            }

            string? defaultDeviceId = null;
            if (defaultDevice is not null)
            {
                defaultDeviceId = defaultDevice.ID;
            }

            try
            {
                var endpoints = enumerator.EnumerateAudioEndPoints(dataFlow, NAudio.CoreAudioApi.DeviceState.Active);

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var isDefault = defaultDeviceId is not null && string.Equals(endpoint.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase);
                        var displayName = endpoint.FriendlyName;
                        if (isDefault)
                        {
                            displayName = string.Concat(displayName, " (Default)");
                        }

                        devices.Add(new AudioDeviceInfo(endpoint.ID, displayName, captureMode, isDefault));
                    }
                    finally
                    {
                        endpoint.Dispose();
                    }
                }
            }
            catch
            {
                // Swallow enumeration failures and return whatever devices we have collected.
            }
            finally
            {
                defaultDevice?.Dispose();
            }

            return devices;
        }
    }
}
