namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// What an app that was asked to quit is doing since: whether it is still running, and whether it
/// still has a window - one asking to save, say - or closed them all and stayed on in the
/// background. And ending it by force, which only ever happens when the person asks.
/// </summary>
public interface IAppProcesses
{
    /// <summary>Whether <paramref name="app"/> is running; null when that cannot be told.</summary>
    bool? IsRunning(string app);

    /// <summary>
    /// Whether <paramref name="app"/> has a window the person can get to - on screen or on another
    /// desktop, not one it keeps hidden; null when that cannot be told - the app isn't found, or the
    /// system won't say.
    /// </summary>
    bool? HasWindow(string app);

    /// <summary>
    /// Ends <paramref name="app"/> at once, as Force Quit does: anything unsaved in it is lost.
    /// Returns whether it was running to be ended.
    /// </summary>
    bool ForceQuit(string app);
}
