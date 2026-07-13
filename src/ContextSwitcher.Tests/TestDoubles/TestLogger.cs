using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class TestLogger : ILogger
{
    public List<LogEntry> Entries { get; } = [];

    public Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        this.Entries.Add(entry);
        return Task.CompletedTask;
    }
}
