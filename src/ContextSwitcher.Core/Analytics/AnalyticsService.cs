using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Tracks the current session boundary in memory and persists completed sessions, crash recovery,
/// retention pruning, and balance summaries through <see cref="IAnalyticsSessionStore"/>.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IClock clock;
    private readonly IAnalyticsSessionStore store;
    private readonly Lock gate = new();
    private ContextSession? currentSession;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsService"/> class.
    /// </summary>
    /// <param name="clock">Supplies session start/end timestamps.</param>
    /// <param name="store">Persists sessions and the active-session marker.</param>
    public AnalyticsService(IClock clock, IAnalyticsSessionStore store)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);

        this.clock = clock;
        this.store = store;
    }

    /// <inheritdoc />
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public async Task StartSessionAsync(string contextId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextId);

        if (!this.Enabled)
        {
            return;
        }

        ContextSession session = new()
        {
            SessionId = Guid.CreateVersion7().ToString(),
            ContextId = contextId,
            StartedAt = this.clock.UtcNow
        };

        lock (this.gate)
        {
            this.currentSession = session;
        }

        await this.store.WriteActiveMarkerAsync(session, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContextSession?> EndCurrentSessionAsync(SessionEndReason reason, CancellationToken cancellationToken)
    {
        if (!this.Enabled)
        {
            return null;
        }

        ContextSession? completed = this.CompleteCurrentSession(reason);
        if (completed is null)
        {
            return null;
        }

        await this.store.AppendCompletedSessionAsync(completed, cancellationToken).ConfigureAwait(false);
        await this.store.ClearActiveMarkerAsync(cancellationToken).ConfigureAwait(false);
        return completed;
    }

    /// <inheritdoc />
    public async Task RecoverFromCrashAsync(CancellationToken cancellationToken)
    {
        if (!this.Enabled)
        {
            return;
        }

        ContextSession? stale = await this.store.ReadActiveMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (stale is null)
        {
            return;
        }

        DateTimeOffset endedAt = this.clock.UtcNow;
        ContextSession recovered = stale with
        {
            EndedAt = endedAt,
            DurationSeconds = (long)(endedAt - stale.StartedAt).TotalSeconds,
            EndReason = SessionEndReason.RecoveredAfterCrash
        };

        await this.store.AppendCompletedSessionAsync(recovered, cancellationToken).ConfigureAwait(false);
        await this.store.ClearActiveMarkerAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PruneOldSessionsAsync(int retentionDays, CancellationToken cancellationToken)
    {
        if (!this.Enabled)
        {
            return;
        }

        IReadOnlyList<ContextSession> all = await this.store.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset cutoff = this.clock.UtcNow.AddDays(-retentionDays);
        List<ContextSession> retained = all.Where(session => session.StartedAt >= cutoff).ToList();

        if (retained.Count != all.Count)
        {
            await this.store.ReplaceAllAsync(retained, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BalanceSummary>> GetDailyBalanceAsync(int days, CancellationToken cancellationToken)
    {
        if (!this.Enabled || days <= 0)
        {
            return [];
        }

        IReadOnlyList<ContextSession> all = await this.store.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        DateOnly today = DateOnly.FromDateTime(this.clock.UtcNow.LocalDateTime);
        return BalanceSummaryCalculator.ComputeDaily(all, today.AddDays(-(days - 1)), today);
    }

    private ContextSession? CompleteCurrentSession(SessionEndReason reason)
    {
        lock (this.gate)
        {
            if (this.currentSession is null)
            {
                return null;
            }

            DateTimeOffset endedAt = this.clock.UtcNow;
            ContextSession completed = this.currentSession with
            {
                EndedAt = endedAt,
                DurationSeconds = (long)(endedAt - this.currentSession.StartedAt).TotalSeconds,
                EndReason = reason
            };

            this.currentSession = null;
            return completed;
        }
    }
}
