using Avalonia.Threading;
using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Clears time history past the setting's days every six hours. Pruning only at startup meant a menu
/// bar app left up for months honoured its retention setting exactly once, keeping every session past
/// the cutoff until the next restart.
/// </summary>
public sealed class HistoryPruner(IAnalyticsService analytics, IAppState appState, ILogger logger) : IHousekeeping
{
    private DispatcherTimer? timer;

    /// <inheritdoc />
    public void Start()
    {
        this.timer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        this.timer.Tick += (_, _) => _ = this.PruneAsync();
        this.timer.Start();
    }

    private async Task PruneAsync()
    {
        try
        {
            await analytics.PruneOldSessionsAsync(appState.Configuration.Analytics.RetentionDays, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warn("Analytics", "HistoryNotPruned", "Old time history could not be cleared away.", ex);
            // Nothing to do about it here; the next tick tries again.
        }
    }
}
