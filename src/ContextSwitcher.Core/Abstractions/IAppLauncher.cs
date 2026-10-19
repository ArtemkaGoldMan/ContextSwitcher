namespace ContextSwitcher.Core.Abstractions;

/// <summary>Opens apps by the name a profile has for them.</summary>
public interface IAppLauncher
{
    /// <summary>Opens <paramref name="app"/>, or brings it forward when it is running already.</summary>
    Task<AppLaunch> LaunchAsync(string app, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>How opening an app went.</summary>
/// <param name="Outcome">Whether it opened, and if not, why not.</param>
/// <param name="Detail">What the system said, for the log; empty when it said nothing.</param>
public sealed record AppLaunch(AppLaunchOutcome Outcome, string Detail);

/// <summary>Whether an app opened, and if not, why not.</summary>
public enum AppLaunchOutcome
{
    Launched,

    /// <summary>No installed app has the name: a typo, or an app since removed.</summary>
    NotInstalled,

    /// <summary>The app is there but didn't open.</summary>
    Failed
}
