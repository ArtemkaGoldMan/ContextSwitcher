using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Persists context sessions to <c>analytics.jsonl</c> and tracks the in-progress session marker
/// used for crash recovery (section 6.3).
/// </summary>
public interface IAnalyticsSessionStore
{
    /// <summary>
    /// Appends one completed session to the log.
    /// </summary>
    Task AppendCompletedSessionAsync(ContextSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every session in the log. Malformed lines are skipped rather than failing the read.
    /// </summary>
    Task<IReadOnlyList<ContextSession>> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Atomically replaces the entire log with <paramref name="sessions"/>, used by retention pruning.
    /// </summary>
    Task ReplaceAllAsync(IReadOnlyList<ContextSession> sessions, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the in-progress session marker, or <see langword="null"/> if none is set.
    /// </summary>
    Task<ContextSession?> ReadActiveMarkerAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes the in-progress session marker when a session starts.
    /// </summary>
    Task WriteActiveMarkerAsync(ContextSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the in-progress session marker once a session ends cleanly.
    /// </summary>
    Task ClearActiveMarkerAsync(CancellationToken cancellationToken);
}
