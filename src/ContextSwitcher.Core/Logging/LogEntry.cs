namespace ContextSwitcher.Core.Logging;

/// <summary>
/// Represents a single structured log record appended to <c>app.log.jsonl</c>.
/// </summary>
public sealed record LogEntry
{
    /// <summary>
    /// Gets the time at which the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the severity of the log entry.
    /// </summary>
    public required LogLevel Level { get; init; }

    /// <summary>
    /// Gets the logical component that produced the entry, e.g. "ContextSwitch".
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets the stable, PascalCase event identifier, e.g. "SwitchStarted".
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Gets the human-readable log message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the context identifier the entry relates to, if any.
    /// </summary>
    public string? ContextId { get; init; }

    /// <summary>
    /// Gets the correlation identifier linking related entries together, if any.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets additional structured data associated with the entry.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Data { get; init; }
}
