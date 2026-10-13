using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Analytics;

public sealed class AwayPauseTests
{
    private readonly FakeClock clock = new();
    private readonly InMemoryAnalyticsSessionStore store = new();
    private string active = "work";

    /// <summary>A profile left on overnight counts the evening and the morning, not the night.</summary>
    [Fact]
    public async Task TheNightIsNotCounted()
    {
        (AnalyticsService analytics, AwayPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);

        this.clock.UtcNow = this.clock.UtcNow.AddHours(2);
        await pause.OnAwayAsync(AwayReason.Sleep, CancellationToken.None);
        this.clock.UtcNow = this.clock.UtcNow.AddHours(10);
        await pause.OnBackAsync(AwayReason.Sleep, CancellationToken.None);
        this.clock.UtcNow = this.clock.UtcNow.AddHours(1);
        await analytics.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.Equal(
            [("work", 7200L, SessionEndReason.Sleep), ("work", 3600L, SessionEndReason.Switch)],
            this.store.Sessions.Select(session => (session.ContextId, session.DurationSeconds ?? -1, session.EndReason ?? SessionEndReason.AppShutdown)));
    }

    /// <summary>Lunch with the screen locked isn't work.</summary>
    [Fact]
    public async Task ALockedScreenIsNotCounted()
    {
        (AnalyticsService analytics, AwayPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);

        this.clock.UtcNow = this.clock.UtcNow.AddHours(3);
        await pause.OnAwayAsync(AwayReason.ScreenLocked, CancellationToken.None);
        this.clock.UtcNow = this.clock.UtcNow.AddHours(1);
        await pause.OnBackAsync(AwayReason.ScreenLocked, CancellationToken.None);

        ContextSession ended = Assert.Single(this.store.Sessions);
        Assert.Equal((10800L, SessionEndReason.Away), (ended.DurationSeconds ?? -1, ended.EndReason ?? SessionEndReason.Switch));
        Assert.Equal("work", (await this.store.ReadActiveMarkerAsync(CancellationToken.None))?.ContextId);
    }

    /// <summary>
    /// A Mac that sleeps also locks, and wakes to the lock screen: counting resumes only once it is
    /// both awake and unlocked - in whichever order macOS reports them.
    /// </summary>
    [Theory]
    [InlineData(AwayReason.Sleep, AwayReason.ScreenLocked)]
    [InlineData(AwayReason.ScreenLocked, AwayReason.Sleep)]
    public async Task CountingResumesOnlyWhenEveryReasonHasCleared(AwayReason first, AwayReason second)
    {
        (AnalyticsService analytics, AwayPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);

        await pause.OnAwayAsync(first, CancellationToken.None);
        await pause.OnAwayAsync(second, CancellationToken.None);
        await pause.OnBackAsync(AwayReason.Sleep, CancellationToken.None);
        Assert.Null(await this.store.ReadActiveMarkerAsync(CancellationToken.None));
        Assert.True(pause.IsAway);

        await pause.OnBackAsync(AwayReason.ScreenLocked, CancellationToken.None);
        Assert.Equal("work", (await this.store.ReadActiveMarkerAsync(CancellationToken.None))?.ContextId);
        Assert.Single(this.store.Sessions);
    }

    /// <summary>Nothing restarts for a profile that is no longer on - or when none was running.</summary>
    [Fact]
    public async Task ComingBackResumesOnlyTheProfileStillOn()
    {
        (AnalyticsService analytics, AwayPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);
        await pause.OnAwayAsync(AwayReason.ScreenLocked, CancellationToken.None);

        this.active = string.Empty;
        await pause.OnBackAsync(AwayReason.ScreenLocked, CancellationToken.None);
        Assert.Null(await this.store.ReadActiveMarkerAsync(CancellationToken.None));
    }

    /// <summary>The same reason reported twice - macOS can say "going to sleep" again - changes nothing.</summary>
    [Fact]
    public async Task ARepeatedReasonKeepsWhatToResume()
    {
        (AnalyticsService analytics, AwayPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);

        await pause.OnAwayAsync(AwayReason.Sleep, CancellationToken.None);
        await pause.OnAwayAsync(AwayReason.Sleep, CancellationToken.None);
        await pause.OnBackAsync(AwayReason.Sleep, CancellationToken.None);

        Assert.Equal("work", (await this.store.ReadActiveMarkerAsync(CancellationToken.None))?.ContextId);
    }

    private (AnalyticsService Analytics, AwayPause Pause) Create()
    {
        AnalyticsService analytics = new(this.clock, this.store);
        return (analytics, new AwayPause(analytics, () => this.active));
    }
}
