using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AudioVisualization.Audio;

namespace AudioVisualization.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<AudioDeviceInfo> _devices = new();
    private float _currentLevelDecibels = -120f;
    private AudioInputCaptureMode _selectedCaptureMode = AudioInputCaptureMode.Loopback;
    private AudioDeviceInfo? _selectedDevice;
    private bool _suppressSelectionNotifications;

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
