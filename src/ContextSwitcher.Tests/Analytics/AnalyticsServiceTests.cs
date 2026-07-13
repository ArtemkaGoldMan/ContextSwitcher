using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Analytics;

public sealed class AnalyticsServiceTests
{
    [Fact]
    public async Task EndCurrentSessionAsyncReturnsNullWhenNoSessionStarted()
    {
        FakeClock clock = new();
        AnalyticsService service = new(clock);

        ContextSession? session = await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.Null(session);
    }

    [Fact]
    public async Task EndCurrentSessionAsyncComputesDurationFromClock()
    {
        FakeClock clock = new();
        AnalyticsService service = new(clock);

        await service.StartSessionAsync("work", CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddHours(2);

        ContextSession? session = await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal("work", session.ContextId);
        Assert.Equal(7200, session.DurationSeconds);
        Assert.Equal(SessionEndReason.Switch, session.EndReason);
    }

    [Fact]
    public async Task EndCurrentSessionAsyncClearsCurrentSession()
    {
        FakeClock clock = new();
        AnalyticsService service = new(clock);

        await service.StartSessionAsync("work", CancellationToken.None);
        await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);
        ContextSession? second = await service.EndCurrentSessionAsync(SessionEndReason.Switch, CancellationToken.None);

        Assert.Null(second);
    }
}
