using System.Linq;
using AudioVisualization.App.Processing;
using Xunit;

namespace AudioVisualization.App.Tests;

public class SignalMetricsTests
{
    [Fact]
    public void CalculateDecibelLevel_ReturnsFloorForSilence()
    {
        var buffer = new float[1024];

        var result = SignalMetrics.CalculateDecibelLevel(buffer, buffer.Length);

        Assert.Equal(SignalMetrics.MinimumDecibels, result, 3);
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0.5f, -6.0206f)]
    [InlineData(0.25f, -12.0412f)]
    public void CalculateDecibelLevel_ComputesExpectedValues(float amplitude, float expectedDb)
    {
        var buffer = Enumerable.Repeat(amplitude, 2048).ToArray();

        var result = SignalMetrics.CalculateDecibelLevel(buffer, buffer.Length);

        Assert.Equal(expectedDb, result, 3);
    }

    [Fact]
    public void CreateHannWindow_HasZeroesAtExtremes()
    {
        var window = SignalMetrics.CreateHannWindow(8);

        Assert.Equal(0f, window[0], 4);
        Assert.Equal(0f, window[^1], 4);

        // Interior coefficients should be positive and symmetric.
        Assert.True(window[1] > 0f);
        Assert.Equal(window[1], window[^2], 4);
    }

    [Fact]
    public void CreateHannWindow_SizeOneReturnsUnity()
    {
        var window = SignalMetrics.CreateHannWindow(1);

        Assert.Single(window);
        Assert.Equal(1f, window[0], 5);
    }
}