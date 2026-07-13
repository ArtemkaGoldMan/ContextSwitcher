using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Appends structured log entries to the local application log.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Appends a log entry.
    /// </summary>
    /// <param name="entry">The entry to append.</param>
    /// <param name="cancellationToken">A token that can cancel the write.</param>
    Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default);
}
