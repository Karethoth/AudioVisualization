using System;
using AudioVisualization.App.ViewModels;
using AudioVisualization.Audio;
using Avalonia.Controls;
using Avalonia.Threading;
using MathNet.Numerics.IntegralTransforms;
using Complex32 = MathNet.Numerics.Complex32;

namespace AudioVisualization.App;

public partial class MainWindow : Window
{
    private IAudioInputSource? _audioInputSource;
    private readonly MainWindowViewModel _viewModel;
    private const float MinimumDecibels = -120f;
    private const int FftSize = 1024;
    private const float SpectrumFloorDecibels = -90f;
    private readonly Complex32[] _fftBuffer = new Complex32[FftSize];
    private readonly float[] _fftWindow = CreateHannWindow(FftSize);
    private readonly float[] _spectrumBuffer = new float[FftSize / 2];

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
        var spectrumLength = CalculateSpectrum(e.Buffer, e.SampleCount, e.Channels, _spectrumBuffer);

        Dispatcher.UIThread.Post(() =>
        {
            _viewModel.UpdateLevels(decibelLevel);
            _viewModel.UpdateSpectrum(_spectrumBuffer.AsSpan(0, spectrumLength));
        });
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

    private int CalculateSpectrum(float[] buffer, int sampleCount, int channels, float[] target)
    {
        if (channels <= 0)
        {
            channels = 1;
        }

        var frames = sampleCount / channels;
        var usableFrames = Math.Min(frames, FftSize);

        for (var i = 0; i < usableFrames; i++)
        {
            var sample = buffer[i * channels];
            _fftBuffer[i] = new Complex32(sample * _fftWindow[i], 0f);
        }

        for (var i = usableFrames; i < FftSize; i++)
        {
            _fftBuffer[i] = Complex32.Zero;
        }

        Fourier.Forward(_fftBuffer, FourierOptions.Matlab);

        var halfSize = Math.Min(target.Length, FftSize / 2);
        var scale = 1f / FftSize;

        for (var i = 0; i < halfSize; i++)
        {
            var magnitude = _fftBuffer[i].Magnitude * scale;
            var db = magnitude <= 1e-9f
                ? SpectrumFloorDecibels
                : 20f * MathF.Log10(magnitude);

            if (db < SpectrumFloorDecibels)
            {
                db = SpectrumFloorDecibels;
            }

            var normalized = (db - SpectrumFloorDecibels) / -SpectrumFloorDecibels;
            target[i] = Math.Clamp(normalized, 0f, 1f);
        }

        return halfSize;
    }

    private static float[] CreateHannWindow(int size)
    {
        var window = new float[size];
        if (size <= 1)
        {
            if (size == 1)
            {
                window[0] = 1f;
            }
            return window;
        }

        var factor = 2f * MathF.PI / (size - 1);
        for (var i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(factor * i));
        }

        return window;
    }
}