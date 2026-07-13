using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Tracks the current context session boundary. Persisting completed sessions to
/// <c>analytics.jsonl</c> and computing balance summaries is added in Phase 8; for now this only
/// tracks the in-progress session so the switch pipeline has a boundary to call into.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Starts tracking a new session for the given context.
    /// </summary>
    /// <param name="contextId">The context the session is for.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    Task StartSessionAsync(string contextId, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the currently tracked session, if any, and returns the completed record.
    /// </summary>
    /// <param name="reason">Why the session ended.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The completed session, or <see langword="null"/> when no session was active.</returns>
    Task<ContextSession?> EndCurrentSessionAsync(SessionEndReason reason, CancellationToken cancellationToken);
}
