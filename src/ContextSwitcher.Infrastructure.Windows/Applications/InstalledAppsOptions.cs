namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>What the app picker's icons are like.</summary>
/// <param name="IconSize">The side of an icon, in pixels.</param>
public sealed record InstalledAppsOptions(int IconSize)
{
    /// <summary>The picker shows an icon at 32 points; twice that stays sharp on a high-density screen.</summary>
    public static InstalledAppsOptions Default { get; } = new(64);
}
