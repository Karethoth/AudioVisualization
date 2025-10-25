using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AudioVisualization.Audio;

namespace AudioVisualization.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<AudioDeviceInfo> _devices = new();
    private float _currentLevelDecibels = -120f;
    private AudioInputCaptureMode _selectedCaptureMode = AudioInputCaptureMode.Loopback;
    private AudioDeviceInfo? _selectedDevice;
    private bool _suppressSelectionNotifications;
    private WriteableBitmap? _spectrumBitmap;
    private byte[]? _spectrumPixels;

    public MainWindowViewModel()
    {
        Devices = new ReadOnlyObservableCollection<AudioDeviceInfo>(_devices);
        _devices.CollectionChanged += OnDevicesCollectionChanged;
    }

    public IReadOnlyList<AudioInputCaptureMode> CaptureModes { get; } = new[]
    {
        AudioInputCaptureMode.Loopback,
        AudioInputCaptureMode.Microphone
    };

    public ReadOnlyObservableCollection<AudioDeviceInfo> Devices { get; }

    public bool HasDevices => _devices.Count > 0;

    public float CurrentLevelDecibels
    {
        get => _currentLevelDecibels;
        private set
        {
            if (Math.Abs(_currentLevelDecibels - value) < 0.05f)
            {
                return;
            }

            _currentLevelDecibels = value;
            OnPropertyChanged();
        }
    }

    public WriteableBitmap? SpectrumBitmap
    {
        get => _spectrumBitmap;
        private set
        {
            if (ReferenceEquals(_spectrumBitmap, value))
            {
                return;
            }

            _spectrumBitmap?.Dispose();
            _spectrumBitmap = value;
            OnPropertyChanged();
        }
    }

    public AudioInputCaptureMode SelectedCaptureMode
    {
        get => _selectedCaptureMode;
        set
        {
            if (_selectedCaptureMode == value)
            {
                return;
            }

            _selectedCaptureMode = value;
            OnPropertyChanged();
            CaptureModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public AudioDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (Equals(_selectedDevice, value))
            {
                return;
            }

            _selectedDevice = value;
            OnPropertyChanged();

            if (!_suppressSelectionNotifications)
            {
                DeviceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? CaptureModeChanged;
    public event EventHandler? DeviceChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateLevels(float decibelLevel)
    {
        CurrentLevelDecibels = decibelLevel;
    }

    private const int SpectrumWidth = 480;
    private const int SpectrumHeight = 200;

    public void UpdateSpectrum(ReadOnlySpan<float> magnitudes)
    {
        if (magnitudes.Length == 0)
        {
            return;
        }

        EnsureSpectrumBitmap();

        if (_spectrumPixels is null)
        {
            return;
        }

        var rowStride = SpectrumWidth * 4;

        for (var row = 0; row < SpectrumHeight; row++)
        {
            var offset = row * rowStride;
            Array.Copy(_spectrumPixels, offset + 4, _spectrumPixels, offset, rowStride - 4);
        }

        for (var row = 0; row < SpectrumHeight; row++)
        {
            var frequencyRatio = SpectrumHeight <= 1 ? 0d : 1d - (double)row / (SpectrumHeight - 1);
            var index = (int)Math.Clamp(frequencyRatio * (magnitudes.Length - 1), 0, magnitudes.Length - 1);

            var intensity = Math.Clamp(magnitudes[index], 0f, 1f);
            var color = MapIntensityToColor(intensity);

            var pixelOffset = row * rowStride + (SpectrumWidth - 1) * 4;
            _spectrumPixels[pixelOffset + 0] = color.b;
            _spectrumPixels[pixelOffset + 1] = color.g;
            _spectrumPixels[pixelOffset + 2] = color.r;
            _spectrumPixels[pixelOffset + 3] = 255;
        }

        var bitmap = new WriteableBitmap(new PixelSize(SpectrumWidth, SpectrumHeight), new Vector(96, 96), PixelFormat.Bgra8888);
        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(_spectrumPixels, 0, framebuffer.Address, _spectrumPixels.Length);
        }

        SpectrumBitmap = bitmap;
    }

    private void EnsureSpectrumBitmap()
    {
        if (_spectrumPixels is not null)
        {
            return;
        }

        _spectrumPixels = new byte[SpectrumWidth * SpectrumHeight * 4];
        SpectrumBitmap = new WriteableBitmap(new PixelSize(SpectrumWidth, SpectrumHeight), new Vector(96, 96), PixelFormat.Bgra8888);

        using var framebuffer = SpectrumBitmap.Lock();
        Marshal.Copy(_spectrumPixels, 0, framebuffer.Address, _spectrumPixels.Length);
    }

    private static (byte r, byte g, byte b) MapIntensityToColor(float intensity)
    {
        var t = Math.Clamp(intensity, 0f, 1f);

        if (t <= 0.25f)
        {
            var ratio = t / 0.25f;
            return ((byte)(32 + 32 * ratio), (byte)(32 + 96 * ratio), (byte)(96 + 128 * ratio));
        }

        if (t <= 0.5f)
        {
            var ratio = (t - 0.25f) / 0.25f;
            return ((byte)(64 + 96 * ratio), (byte)(128 + 64 * ratio), 224);
        }

        if (t <= 0.75f)
        {
            var ratio = (t - 0.5f) / 0.25f;
            return ((byte)(160 + 64 * ratio), (byte)(192 + 32 * ratio), (byte)(224 - 96 * ratio));
        }

        var finalRatio = (t - 0.75f) / 0.25f;
        return ((byte)(224 + 31 * finalRatio), (byte)(224 + 31 * finalRatio), (byte)(128 - 112 * finalRatio));
    }

    public void ReplaceDevices(IEnumerable<AudioDeviceInfo> devices)
    {
        if (devices is null)
        {
            throw new ArgumentNullException(nameof(devices));
        }

        var previousDeviceId = _selectedDevice?.Id;

        _suppressSelectionNotifications = true;
        try
        {
            _devices.Clear();

            foreach (var device in devices)
            {
                _devices.Add(device);
            }

            OnPropertyChanged(nameof(HasDevices));

            var nextSelection = _devices.FirstOrDefault(d => d.IsDefault) ?? _devices.FirstOrDefault();
            SelectedDevice = nextSelection;
        }
        finally
        {
            _suppressSelectionNotifications = false;
        }

        var newDeviceId = _selectedDevice?.Id;
        if (!string.Equals(previousDeviceId, newDeviceId, StringComparison.Ordinal))
        {
            DeviceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasDevices));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
