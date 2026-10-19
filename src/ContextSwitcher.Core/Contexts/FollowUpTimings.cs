namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// How long <see cref="ClosingAppsFollowUp"/> watches the apps a switch left running, how often it
/// looks, and how long a force quit is given. Data, chosen with the platform's services.
/// </summary>
public sealed record FollowUpTimings
{
    /// <summary>What the app has always used.</summary>
    public static FollowUpTimings Default { get; } = new();

    /// <summary>For tests: every wait as short as a test can make it and still see an app go.</summary>
    public static FollowUpTimings Immediate { get; } = new()
    {
        WatchFor = TimeSpan.FromMilliseconds(150),
        LookEvery = TimeSpan.FromMilliseconds(5),
        ForceQuitWait = TimeSpan.FromMilliseconds(200)
    };

    /// <summary>How long the apps are watched after the switch.</summary>
    public TimeSpan WatchFor { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>How often they are looked at.</summary>
    public TimeSpan LookEvery { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long a force quit is given to take effect before it counts as not having worked.</summary>
    public TimeSpan ForceQuitWait { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>How often a force-quit app is looked for while it is given that time.</summary>
    public TimeSpan ForceQuitLookEvery { get; init; } = TimeSpan.FromMilliseconds(100);
}
