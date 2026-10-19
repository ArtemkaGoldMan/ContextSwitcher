using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.App.Startup;

/// <summary>No time counts while the Mac sleeps, its screen is locked, or another user is in front.</summary>
public sealed class AwayWatcher(AwayEvents events, AwayPause pause, ILogger logger) : IHousekeeping
{
    /// <inheritdoc />
    public void Start()
    {
        events.Away += (_, reason) => _ = TimeHistory.WriteQuietlyAsync(() => pause.OnAwayAsync(reason, CancellationToken.None), logger);
        events.Back += (_, reason) => _ = TimeHistory.WriteQuietlyAsync(() => pause.OnBackAsync(reason, CancellationToken.None), logger);
    }
}
