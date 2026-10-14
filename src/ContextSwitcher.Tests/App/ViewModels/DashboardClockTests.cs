using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class DashboardClockTests
{
    /// <summary>Seconds are shown for the first minute, so it ticks each one; after that, only the minute turning over changes the text.</summary>
    [Theory]
    [InlineData(0, 1000)]
    [InlineData(30, 1000)]
    [InlineData(59.9, 1000)]
    [InlineData(60, 60050)]
    [InlineData(90, 30050)]
    [InlineData(119, 1050)]
    [InlineData(3600 + 59, 1050)]
    [InlineData(3 * 3600 + 15 * 60 + 10, 50050)]
    public void ItTicksWhenTheTextNextChanges(double elapsedSeconds, double expectedMilliseconds)
    {
        TimeSpan interval = DashboardViewModel.UntilElapsedTextChanges(TimeSpan.FromSeconds(elapsedSeconds));

        Assert.Equal(expectedMilliseconds, interval.TotalMilliseconds, 1);
    }

    /// <summary>A clock that ran backwards - a profile switched at a time that is now in the future - still ticks sensibly.</summary>
    [Fact]
    public void ANegativeElapsedTimeStillTicksEverySecond()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), DashboardViewModel.UntilElapsedTextChanges(TimeSpan.FromSeconds(-5)));
    }
}
