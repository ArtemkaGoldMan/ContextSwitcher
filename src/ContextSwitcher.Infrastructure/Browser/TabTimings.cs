namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// How long a browser is given to say what tabs it has, and how a profile's newly opened tabs are
/// looked for. Data, chosen with the platform's services.
/// </summary>
public sealed record TabTimings
{
    /// <summary>What the app has always used.</summary>
    public static TabTimings Default { get; } = new();

    /// <summary>For tests: no wait between looks for the tabs just opened.</summary>
    public static TabTimings Immediate { get; } = Default with { LookForOpenedEvery = TimeSpan.Zero };

    /// <summary>
    /// Duplicate-tab inspection is a best-effort nicety, so it gets a small slice of the step's
    /// budget rather than all of it. A browser that answers AppleEvents at all answers in
    /// milliseconds; one that does not (no Automation permission, or a wedged process) blocks until
    /// the script is killed. Handing it the whole step timeout used to starve the plain
    /// <c>open</c> fallback that docs/architecture.md requires, so no URL opened at all.
    /// </summary>
    public TimeSpan Probe { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>How long to wait between looks for the tabs just opened.</summary>
    public TimeSpan LookForOpenedEvery { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>How many times to look for the tabs just opened before settling for what has appeared.</summary>
    public int OpenedLooks { get; init; } = 8;
}
