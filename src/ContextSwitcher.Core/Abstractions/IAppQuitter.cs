namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Asks apps to quit, as quitting one from its menu does, and looks whether they have gone. An app
/// may ask to save first, or take a moment to go, so the two are separate.
/// </summary>
public interface IAppQuitter
{
    /// <summary>
    /// Asks <paramref name="app"/> to quit if it is running. Returns what the system said, for the
    /// log; empty when it said nothing. Whether the app went is for <see cref="IsRunningAsync"/>.
    /// </summary>
    Task<string> QuitAsync(string app, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="app"/> is running; null when the look failed or took too long.</summary>
    Task<bool?> IsRunningAsync(string app, TimeSpan timeout, CancellationToken cancellationToken);
}
