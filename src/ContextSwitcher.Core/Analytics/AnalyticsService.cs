using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Tracks the current session boundary and persists completed sessions, crash recovery, retention
/// pruning, and balance summaries through <see cref="IAnalyticsSessionStore"/>.
///
/// The running session is the active marker on disk, not anything held in memory. A switch from
/// Shortcuts, Siri or the command line runs in a process of its own: when each process kept its own
/// session, that switch never ended the one the app had running - the app went on counting the old
/// profile until its own next switch, and the new profile's time was lost when that switch cleared
/// the marker. Every process ending and starting the one marker keeps the sessions in step with the
/// switches, whoever makes them.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IClock clock;
    private readonly IAnalyticsSessionStore store;

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

        await this.store.WriteActiveMarkerAsync(session, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContextSession?> EndCurrentSessionAsync(SessionEndReason reason, CancellationToken cancellationToken)
    {
        if (!this.Enabled)
        {
            return null;
        }

        return await this.CompleteRunningSessionAsync(reason, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecoverFromCrashAsync(CancellationToken cancellationToken)
    {
        if (!this.Enabled)
        {
            return;
        }

        await this.CompleteRunningSessionAsync(SessionEndReason.RecoveredAfterCrash, cancellationToken).ConfigureAwait(false);
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

        List<ContextSession> sessions = [.. await this.store.ReadAllAsync(cancellationToken).ConfigureAwait(false)];

        // The profile that is on counts up to now: it used to show only once it was switched away
        // from, so a whole day in one profile read as nothing at all until evening.
        if (await this.store.ReadActiveMarkerAsync(cancellationToken).ConfigureAwait(false) is { } running)
        {
            sessions.Add(this.EndedNow(running, reason: null));
        }

        DateOnly today = DateOnly.FromDateTime(this.clock.UtcNow.LocalDateTime);
        return BalanceSummaryCalculator.ComputeDaily(sessions, today.AddDays(-(days - 1)), today);
    }

    /// <summary>Ends whatever session is running, whichever process started it, and records it.</summary>
    private async Task<ContextSession?> CompleteRunningSessionAsync(SessionEndReason reason, CancellationToken cancellationToken)
    {
        if (await this.store.ReadActiveMarkerAsync(cancellationToken).ConfigureAwait(false) is not { } running)
        {
            return null;
        }

        ContextSession completed = this.EndedNow(running, reason);
        await this.store.AppendCompletedSessionAsync(completed, cancellationToken).ConfigureAwait(false);
        await this.store.ClearActiveMarkerAsync(cancellationToken).ConfigureAwait(false);
        return completed;
    }

    private ContextSession EndedNow(ContextSession running, SessionEndReason? reason)
    {
        DateTimeOffset endedAt = this.clock.UtcNow;
        return running with
        {
            EndedAt = endedAt,
            DurationSeconds = Math.Max(0, (long)(endedAt - running.StartedAt).TotalSeconds),
            EndReason = reason
        };
    }
}
