using Avalonia.Threading;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.App.Startup;

/// <summary>A sign of life every couple of minutes: after a crash, the session ends at the last one.</summary>
public sealed class AliveRecorder(IAnalyticsService analytics, ILogger logger) : IHousekeeping
{
    private DispatcherTimer? timer;

    /// <inheritdoc />
    public void Start()
    {
        _ = this.RecordAsync();
        this.timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        this.timer.Tick += (_, _) => _ = this.RecordAsync();
        this.timer.Start();
    }

    private Task RecordAsync() => TimeHistory.WriteQuietlyAsync(() => analytics.RecordAliveAsync(CancellationToken.None), logger);
}
