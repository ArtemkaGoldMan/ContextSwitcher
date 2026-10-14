using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class DisplayAvailabilityTests
{
    [Fact]
    public void ItDoesNotWaitWhenADisplayIsAwake()
    {
        List<TimeSpan> pauses = [];

        TimeSpan waited = DisplayAvailability.WaitUntilAvailable(() => true, TimeSpan.FromSeconds(3), pauses.Add);

        Assert.Equal(TimeSpan.Zero, waited);
        Assert.Empty(pauses);
    }

    /// <summary>A launch onto a dark screen looks again every few seconds and starts when one wakes - it neither gives up nor aborts.</summary>
    [Fact]
    public void ItLooksAgainUntilADisplayWakes()
    {
        int looks = 0;
        List<TimeSpan> pauses = [];

        TimeSpan waited = DisplayAvailability.WaitUntilAvailable(() => ++looks > 4, TimeSpan.FromSeconds(3), pauses.Add);

        Assert.Equal(5, looks);
        Assert.Equal(TimeSpan.FromSeconds(12), waited);
        Assert.All(pauses, pause => Assert.Equal(TimeSpan.FromSeconds(3), pause));
        Assert.Equal(4, pauses.Count);
    }

    /// <summary>The exact message a launch with every display asleep died of.</summary>
    [Fact]
    public void ItRecognisesTheRenderTimerFailure()
    {
        InvalidOperationException failure = new("Avalonia.Native was not able to start the RenderTimer. Native error code is: -6661");

        Assert.True(DisplayAvailability.IsRenderTimerFailure(failure));
    }

    [Fact]
    public void ItLeavesEveryOtherFailureAlone()
    {
        Assert.False(DisplayAvailability.IsRenderTimerFailure(new InvalidOperationException("Sequence contains no elements")));
        Assert.False(DisplayAvailability.IsRenderTimerFailure(new ArgumentException("RenderTimer")));
    }

    /// <summary>
    /// The answer depends on whether this machine has a display awake - a CI runner may not - so only
    /// that asking does not throw is checked.
    /// </summary>
    [Fact]
    public void AskingTheSystemDoesNotThrow()
    {
        _ = DisplayAvailability.CanDraw();
    }
}
