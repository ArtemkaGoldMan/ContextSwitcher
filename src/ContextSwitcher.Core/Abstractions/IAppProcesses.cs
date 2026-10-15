namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// What an app that was asked to quit is doing since: whether it is still running, and whether it
/// has a window on screen - one asking to save, say. And ending it by force, which only ever
/// happens when the person asks.
/// </summary>
public interface IAppProcesses
{
    /// <summary>Whether <paramref name="app"/> is running; null when that cannot be told.</summary>
    bool? IsRunning(string app);

    /// <summary>
    /// Whether <paramref name="app"/> has a window on screen now; null when that cannot be told - the
    /// app isn't found, or the system won't say. One on another desktop doesn't count: it can't be
    /// told from one an app keeps hidden.
    /// </summary>
    bool? HasWindowOnScreen(string app);

    /// <summary>
    /// Ends <paramref name="app"/> at once, as Force Quit does: anything unsaved in it is lost.
    /// Returns whether it was running to be ended.
    /// </summary>
    bool ForceQuit(string app);
}
