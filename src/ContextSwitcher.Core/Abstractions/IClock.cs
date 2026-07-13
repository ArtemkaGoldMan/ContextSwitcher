namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Provides the current time so that domain logic never calls the system clock directly.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
