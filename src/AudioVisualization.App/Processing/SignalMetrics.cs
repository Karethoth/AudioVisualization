using System;

namespace AudioVisualization.App.Processing;

internal static class SignalMetrics
{
    internal const float MinimumDecibels = -120f;

    internal static float CalculateDecibelLevel(ReadOnlySpan<float> buffer, int sampleCount, float minimumDb = MinimumDecibels)
    {
        var length = Math.Min(buffer.Length, sampleCount);
        if (length <= 0)
        {
            return minimumDb;
        }

        double sumSquares = 0d;
        for (var i = 0; i < length; i++)
        {
            var sample = buffer[i];
            sumSquares += sample * sample;
        }

        var mean = sumSquares / length;
        if (mean <= 0d || double.IsNaN(mean) || double.IsInfinity(mean))
        {
            return minimumDb;
        }

        var rms = Math.Sqrt(mean);
        var decibels = rms <= 0d
            ? minimumDb
            : (float)(20d * Math.Log10(rms));

        return Math.Clamp(decibels, minimumDb, 0f);
    }

    internal static float[] CreateHannWindow(int size)
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
