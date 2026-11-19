using Perun.Client.View;

namespace Perun.Tests;

public class CameraFlightTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.2f, 0.5f)]
    [InlineData(0.4f, 1f)]
    [InlineData(1f, 1f)]
    [InlineData(-1f, 0f)]
    public void ProgressRunsFromZeroToOneOverTheDuration(float elapsed, float expected) =>
        Assert.Equal(expected, CameraFlight.Progress(elapsed, 0.4f), 4);

    [Fact]
    public void ZeroDurationArrivesAtOnce() => Assert.Equal(1f, CameraFlight.Progress(0f, 0f), 4);

    [Fact]
    public void EasingStartsAndEndsInPlaceAndIsHalfwayInTheMiddle()
    {
        Assert.Equal(0f, CameraFlight.Ease(0f), 4);
        Assert.Equal(0.5f, CameraFlight.Ease(0.5f), 4);
        Assert.Equal(1f, CameraFlight.Ease(1f), 4);
    }

    [Fact]
    public void EasingNeverGoesBackwards()
    {
        float previous = 0f;
        for (int i = 1; i <= 100; i++)
        {
            float eased = CameraFlight.Ease(i / 100f);
            Assert.True(eased >= previous, $"eased value went back at {i}%");
            previous = eased;
        }
    }
}
