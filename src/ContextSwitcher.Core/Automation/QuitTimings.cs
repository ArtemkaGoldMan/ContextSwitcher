namespace ContextSwitcher.Core.Automation;

/// <summary>
/// How an app asked to quit is watched going: how much of its share of the step the asking and the
/// watching each get, and how often and how many times it is looked for. Data, chosen with the
/// platform's services: closing an app on Windows will want waits of its own.
/// </summary>
public sealed record QuitTimings
{
    /// <summary>What the app has always used.</summary>
    public static QuitTimings Default { get; } = new();

    /// <summary>For tests: no wait between looks, so an app that never quits takes no time.</summary>
    public static QuitTimings Immediate { get; } = Default with { LookEvery = TimeSpan.Zero };

    /// <summary>
    /// The share of an app's budget the quit itself gets. A graceful quit can block indefinitely on a
    /// modal "save this document?" sheet, so it must not have it all.
    /// </summary>
    public double QuitShare { get; init; } = 0.5;

    /// <summary>
    /// The share for seeing it go. With <see cref="QuitShare"/> it stays inside the app's budget, so
    /// the step's own timeout never cuts the report off.
    /// </summary>
    public double WatchShare { get; init; } = 0.4;

    /// <summary>
    /// How often to look again. Electron apps - VS Code, Cursor, Slack - answer "quit" at once and
    /// then take a second or two to actually go, so a single look straight after the quit reported
    /// them as "Could not close" while they were closing fine.
    /// </summary>
    public TimeSpan LookEvery { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>A bound on the looks for one app, whatever the interval.</summary>
    public int MaxLooks { get; init; } = 15;

    /// <summary>The longest one look may take; it normally takes about 0.1s.</summary>
    public TimeSpan MaxLook { get; init; } = TimeSpan.FromSeconds(2);
}
