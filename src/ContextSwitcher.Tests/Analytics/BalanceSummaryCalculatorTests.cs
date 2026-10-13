using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Tests.Analytics;

public sealed class BalanceSummaryCalculatorTests
{
    /// <summary>Kyiv: UTC+3 in October, so 22:00 local is 19:00 UTC.</summary>
    private static readonly TimeZoneInfo Kyiv = TimeZoneInfo.CreateCustomTimeZone("Kyiv-test", TimeSpan.FromHours(3), "Kyiv", "Kyiv");

    private static readonly DateOnly Monday = new(2026, 10, 12);

    /// <summary>Four hours from 22:00 are two on each day - not four on the day they started.</summary>
    [Fact]
    public void ASessionPastMidnightIsSplitBetweenTheDays()
    {
        ContextSession lateNight = Session("work", new DateTimeOffset(2026, 10, 12, 22, 0, 0, TimeSpan.FromHours(3)), TimeSpan.FromHours(4));

        IReadOnlyList<BalanceSummary> days = BalanceSummaryCalculator.ComputeDaily([lateNight], Monday, Monday.AddDays(1), Kyiv);

        Assert.Equal([7200L, 7200L], days.Select(day => day.SecondsByContextId["work"]));
    }

    [Fact]
    public void ASessionWithinADayCountsWhole()
    {
        ContextSession morning = Session("work", new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.FromHours(3)), TimeSpan.FromMinutes(90));

        BalanceSummary day = Assert.Single(BalanceSummaryCalculator.ComputeDaily([morning], Monday, Monday, Kyiv));

        Assert.Equal(5400, day.SecondsByContextId["work"]);
    }

    /// <summary>Only the part inside the range counts: a session from before it adds its tail.</summary>
    [Fact]
    public void OnlyThePartInsideTheRangeCounts()
    {
        ContextSession fromSunday = Session("personal", new DateTimeOffset(2026, 10, 11, 23, 0, 0, TimeSpan.FromHours(3)), TimeSpan.FromHours(2));

        BalanceSummary day = Assert.Single(BalanceSummaryCalculator.ComputeDaily([fromSunday], Monday, Monday, Kyiv));

        Assert.Equal(3600, day.SecondsByContextId["personal"]);
    }

    private static ContextSession Session(string contextId, DateTimeOffset startedAt, TimeSpan duration) => new()
    {
        SessionId = Guid.NewGuid().ToString(),
        ContextId = contextId,
        StartedAt = startedAt,
        DurationSeconds = (long)duration.TotalSeconds
    };
}
