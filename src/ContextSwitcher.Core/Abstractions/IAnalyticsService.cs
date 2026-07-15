using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Tracks the current context session boundary, persists completed sessions, recovers a session
/// left open by a crash, prunes old sessions, and computes balance summaries for the dashboard chart.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Gets or sets whether analytics tracking is active. When <see langword="false"/>, every
    /// method on this interface is a no-op - no session data is tracked, persisted, or read.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Starts tracking a new session for the given context.
    /// </summary>
    /// <param name="contextId">The context the session is for.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    Task StartSessionAsync(string contextId, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the currently tracked session, if any, persists it, and returns the completed record.
    /// </summary>
    /// <param name="reason">Why the session ended.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The completed session, or <see langword="null"/> when no session was active.</returns>
    Task<ContextSession?> EndCurrentSessionAsync(SessionEndReason reason, CancellationToken cancellationToken);

    /// <summary>
    /// Checks for a session left open by a crash (an active marker with no matching completed
    /// entry) and, if found, closes and persists it with <see cref="SessionEndReason.RecoveredAfterCrash"/>.
    /// Call once at startup, before <see cref="StartSessionAsync"/> for the new session.
    /// </summary>
    Task RecoverFromCrashAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Removes persisted sessions older than <paramref name="retentionDays"/>.
    /// </summary>
    Task PruneOldSessionsAsync(int retentionDays, CancellationToken cancellationToken);

    /// <summary>
    /// Computes per-context daily totals for the last <paramref name="days"/> days (including today).
    /// </summary>
    Task<IReadOnlyList<BalanceSummary>> GetDailyBalanceAsync(int days, CancellationToken cancellationToken);
}
