namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>How long listing the Start menu may take, how long a list is kept, and what of it is left out.</summary>
public sealed record StartAppsOptions(TimeSpan Timeout, TimeSpan KeepFor, StartAppsFilter Filter)
{
    /// <summary>
    /// PowerShell takes a few seconds to start on a slow disk; a list is kept for a few minutes, so
    /// opening the picker again or switching twice lists once, and an app installed since is found soon.
    /// </summary>
    public static StartAppsOptions Default { get; } = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), StartAppsFilter.Default);
}
