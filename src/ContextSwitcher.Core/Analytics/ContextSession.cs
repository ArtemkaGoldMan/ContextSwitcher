namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// A single recorded interval of time spent in one context, appended to <c>analytics.jsonl</c>.
/// </summary>
public sealed record ContextSession
{
    /// <summary>
    /// Gets or sets the schema version for the session record.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets or sets the unique identifier for the session.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the context this session was recorded for.
    /// </summary>
    public string ContextId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the time the session started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets or sets the time the session ended, or <see langword="null"/> while still active.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>
    /// Gets or sets the session duration in seconds, once ended.
    /// </summary>
    public long? DurationSeconds { get; init; }

    /// <summary>
    /// Gets or sets why the session ended, once ended.
    /// </summary>
    public SessionEndReason? EndReason { get; init; }
}
