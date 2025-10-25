using System;
using AudioVisualization.App.ViewModels;
using AudioVisualization.Audio;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AudioVisualization.App;

public partial class MainWindow : Window
{
    private IAudioInputSource? _audioInputSource;
    private readonly MainWindowViewModel _viewModel;
    private const float MinimumDecibels = -120f;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel();
        _viewModel.CaptureModeChanged += OnCaptureModeChanged;
        _viewModel.DeviceChanged += OnDeviceChanged;

        DataContext = _viewModel;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        RefreshDevices();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _viewModel.CaptureModeChanged -= OnCaptureModeChanged;
        _viewModel.DeviceChanged -= OnDeviceChanged;

        StopAudio();
    }

    private void RefreshDevices()
    {
        try
        {
            var devices = AudioInputDeviceService.GetAvailableDevices(_viewModel.SelectedCaptureMode);
            _viewModel.ReplaceDevices(devices);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            _viewModel.ReplaceDevices(Array.Empty<AudioDeviceInfo>());
            _viewModel.UpdateLevels(MinimumDecibels);
        }
    }

    private void OnCaptureModeChanged(object? sender, EventArgs e)
    {
        RefreshDevices();
    }

    private void OnDeviceChanged(object? sender, EventArgs e)
    {
        RestartAudio();
    }

    private void RestartAudio()
    {
        StopAudio();

        var selectedDevice = _viewModel.SelectedDevice;
        if (selectedDevice is null)
        {
            return;
        }

        var options = new AudioInputOptions
        {
            CaptureMode = selectedDevice.CaptureMode,
            DeviceId = selectedDevice.Id
        };

        IAudioInputSource? source = null;

        try
        {
            source = AudioInputSourceFactory.CreateDefault(options);
            source.AudioBufferReady += HandleAudioBufferReady;
            source.Start();

            _audioInputSource = source;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            _viewModel.UpdateLevels(MinimumDecibels);

            if (source is not null)
            {
                source.AudioBufferReady -= HandleAudioBufferReady;
                source.Dispose();
            }

            _audioInputSource = null;
        }
    }

    private void StopAudio()
    {
        if (_audioInputSource is null)
        {
            return;
        }

        _audioInputSource.AudioBufferReady -= HandleAudioBufferReady;

        try
        {
            if (_audioInputSource.IsRunning)
            {
                _audioInputSource.Stop();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        _audioInputSource.Dispose();
        _audioInputSource = null;

        _viewModel.UpdateLevels(MinimumDecibels);
    }

    private void HandleAudioBufferReady(object? sender, AudioBufferReadyEventArgs e)
    {
        if (e.SampleCount <= 0)
        {
            return;
        }

        var decibelLevel = CalculateDecibelLevel(e.Buffer, e.SampleCount);

        Dispatcher.UIThread.Post(() => _viewModel.UpdateLevels(decibelLevel));
    }

    private static float CalculateDecibelLevel(float[] buffer, int sampleCount)
    {
        var length = Math.Min(buffer.Length, sampleCount);
        if (length <= 0)
        {
            return MinimumDecibels;
        }

        double sumSquares = 0;
        for (var i = 0; i < length; i++)
        {
            var sample = buffer[i];
            sumSquares += sample * sample;
        }

        var mean = sumSquares / length;
        if (mean <= 0 || double.IsNaN(mean) || double.IsInfinity(mean))
        {
            return MinimumDecibels;
        }

        var rms = Math.Sqrt(mean);
        var decibels = rms <= 0
            ? MinimumDecibels
            : (float)(20 * Math.Log10(rms));

        decibels = Math.Clamp(decibels, MinimumDecibels, 0f);

        return decibels;
    }
}