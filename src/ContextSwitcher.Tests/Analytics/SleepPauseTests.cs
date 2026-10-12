using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Analytics;

public sealed class SleepPauseTests
{
    private readonly FakeClock clock = new();
    private readonly InMemoryAnalyticsSessionStore store = new();
    private string active = "work";

    /// <summary>A profile left on overnight counts the evening and the morning, not the night.</summary>
    [Fact]
    public async Task TheNightIsNotCounted()
    {
        (AnalyticsService analytics, SleepPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);

        this.clock.UtcNow = this.clock.UtcNow.AddHours(2);
        await pause.OnWillSleepAsync(CancellationToken.None);
        this.clock.UtcNow = this.clock.UtcNow.AddHours(10);
        await pause.OnDidWakeAsync(CancellationToken.None);
        this.clock.UtcNow = this.clock.UtcNow.AddHours(1);
        await analytics.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.Equal(
            [("work", 7200L, SessionEndReason.Sleep), ("work", 3600L, SessionEndReason.Switch)],
            this.store.Sessions.Select(session => (session.ContextId, session.DurationSeconds ?? -1, session.EndReason ?? SessionEndReason.AppShutdown)));
    }

    /// <summary>Nothing restarts for a profile that is no longer on - or when none was running.</summary>
    [Fact]
    public async Task WakingResumesOnlyTheProfileStillOn()
    {
        (AnalyticsService analytics, SleepPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);
        await pause.OnWillSleepAsync(CancellationToken.None);

        this.active = string.Empty;
        await pause.OnDidWakeAsync(CancellationToken.None);
        Assert.Null(await this.store.ReadActiveMarkerAsync(CancellationToken.None));

        this.active = "work";
        await pause.OnDidWakeAsync(CancellationToken.None);
        Assert.Null(await this.store.ReadActiveMarkerAsync(CancellationToken.None));
    }

    /// <summary>macOS can say "going to sleep" twice; the second, with nothing running, keeps what to resume.</summary>
    [Fact]
    public async Task ASecondSleepKeepsTheProfileToResume()
    {
        (AnalyticsService analytics, SleepPause pause) = this.Create();
        await analytics.StartSessionAsync("work", CancellationToken.None);

        await pause.OnWillSleepAsync(CancellationToken.None);
        await pause.OnWillSleepAsync(CancellationToken.None);
        await pause.OnDidWakeAsync(CancellationToken.None);

        Assert.Equal("work", (await this.store.ReadActiveMarkerAsync(CancellationToken.None))?.ContextId);
    }

    private (AnalyticsService Analytics, SleepPause Pause) Create()
    {
        AnalyticsService analytics = new(this.clock, this.store);
        return (analytics, new SleepPause(analytics, () => this.active));
    }
}
