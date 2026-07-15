using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Analytics;

public sealed class AnalyticsServiceTests
{
    [Fact]
    public async Task EndCurrentSessionAsyncReturnsNullWhenNoSessionStarted()
    {
        (AnalyticsService service, _) = Create();

        ContextSession? session = await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.Null(session);
    }

    [Fact]
    public async Task EndCurrentSessionAsyncComputesDurationFromClockAndPersists()
    {
        (AnalyticsService service, FakeClock clock, InMemoryAnalyticsSessionStore store) = CreateWithClock();

        await service.StartSessionAsync("work", CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddHours(2);

        ContextSession? session = await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal("work", session.ContextId);
        Assert.Equal(7200, session.DurationSeconds);
        Assert.Equal(SessionEndReason.Switch, session.EndReason);

        ContextSession persisted = Assert.Single(store.Sessions);
        Assert.Equal(session, persisted);
    }

    [Fact]
    public async Task EndCurrentSessionAsyncClearsCurrentSession()
    {
        (AnalyticsService service, _) = Create();

        await service.StartSessionAsync("work", CancellationToken.None);
        await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);
        ContextSession? second = await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.Null(second);
    }

    [Fact]
    public async Task StartSessionAsyncWritesActiveMarker()
    {
        (AnalyticsService service, InMemoryAnalyticsSessionStore store) = Create();

        await service.StartSessionAsync("work", CancellationToken.None);

        ContextSession? marker = await store.ReadActiveMarkerAsync(CancellationToken.None);
        Assert.NotNull(marker);
        Assert.Equal("work", marker.ContextId);
    }

    [Fact]
    public async Task EndCurrentSessionAsyncClearsActiveMarker()
    {
        (AnalyticsService service, InMemoryAnalyticsSessionStore store) = Create();

        await service.StartSessionAsync("work", CancellationToken.None);
        await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.Null(await store.ReadActiveMarkerAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecoverFromCrashAsyncDoesNothingWhenNoMarkerPresent()
    {
        (AnalyticsService service, InMemoryAnalyticsSessionStore store) = Create();

        await service.RecoverFromCrashAsync(CancellationToken.None);

        Assert.Empty(store.Sessions);
    }

    [Fact]
    public async Task RecoverFromCrashAsyncClosesStaleMarkerAsRecoveredAfterCrash()
    {
        (AnalyticsService service, FakeClock clock, InMemoryAnalyticsSessionStore store) = CreateWithClock();
        await store.WriteActiveMarkerAsync(
            new ContextSession { SessionId = "s1", ContextId = "work", StartedAt = clock.UtcNow },
            CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(30);

        await service.RecoverFromCrashAsync(CancellationToken.None);

        ContextSession recovered = Assert.Single(store.Sessions);
        Assert.Equal(SessionEndReason.RecoveredAfterCrash, recovered.EndReason);
        Assert.Equal(1800, recovered.DurationSeconds);
        Assert.Null(await store.ReadActiveMarkerAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PruneOldSessionsAsyncRemovesSessionsOlderThanRetention()
    {
        (AnalyticsService service, FakeClock clock, InMemoryAnalyticsSessionStore store) = CreateWithClock();
        await store.ReplaceAllAsync(
            [
                new ContextSession { SessionId = "old", ContextId = "work", StartedAt = clock.UtcNow.AddDays(-40) },
                new ContextSession { SessionId = "recent", ContextId = "work", StartedAt = clock.UtcNow.AddDays(-5) }
            ],
            CancellationToken.None);

        await service.PruneOldSessionsAsync(retentionDays: 30, CancellationToken.None);

        ContextSession remaining = Assert.Single(store.Sessions);
        Assert.Equal("recent", remaining.SessionId);
    }

    [Fact]
    public async Task GetDailyBalanceAsyncSumsDurationPerContextPerDay()
    {
        (AnalyticsService service, FakeClock clock, InMemoryAnalyticsSessionStore store) = CreateWithClock();
        DateTimeOffset today = clock.UtcNow;
        await store.ReplaceAllAsync(
            [
                new ContextSession { SessionId = "1", ContextId = "work", StartedAt = today, DurationSeconds = 3600 },
                new ContextSession { SessionId = "2", ContextId = "personal", StartedAt = today, DurationSeconds = 1800 }
            ],
            CancellationToken.None);

        IReadOnlyList<BalanceSummary> summary = await service.GetDailyBalanceAsync(1, CancellationToken.None);

        BalanceSummary day = Assert.Single(summary);
        Assert.Equal(3600, day.SecondsByContextId["work"]);
        Assert.Equal(1800, day.SecondsByContextId["personal"]);
    }

    [Fact]
    public async Task DisabledServiceIsANoOpForEverything()
    {
        (AnalyticsService service, InMemoryAnalyticsSessionStore store) = Create();
        service.Enabled = false;

        await service.StartSessionAsync("work", CancellationToken.None);
        ContextSession? ended = await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);
        await service.RecoverFromCrashAsync(CancellationToken.None);
        await service.PruneOldSessionsAsync(30, CancellationToken.None);
        IReadOnlyList<BalanceSummary> balance = await service.GetDailyBalanceAsync(7, CancellationToken.None);

        Assert.Null(ended);
        Assert.Empty(store.Sessions);
        Assert.Empty(balance);
    }

    private static (AnalyticsService Service, InMemoryAnalyticsSessionStore Store) Create()
    {
        (AnalyticsService service, _, InMemoryAnalyticsSessionStore store) = CreateWithClock();
        return (service, store);
    }

    private static (AnalyticsService Service, FakeClock Clock, InMemoryAnalyticsSessionStore Store) CreateWithClock()
    {
        FakeClock clock = new();
        InMemoryAnalyticsSessionStore store = new();
        return (new AnalyticsService(clock, store), clock, store);
    }
}
