using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Time;

/// <summary>
/// Provides the real wall-clock time from the operating system.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
