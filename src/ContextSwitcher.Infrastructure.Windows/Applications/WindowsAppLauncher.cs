using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Opens an app by the name it has in the Start menu, through File Explorer's <c>shell:AppsFolder</c>,
/// which starts a desktop app or a Store app the same way. One that is open already isn't started
/// again - Windows would open another window of it - but has its minimised windows brought back.
/// </summary>
public sealed class WindowsAppLauncher : IAppLauncher
{
    private readonly IProcessRunner processRunner;
    private readonly StartApps startApps;

    private readonly AppWindows appWindows;
    private readonly IAppsMap appsMap;

    public WindowsAppLauncher(IProcessRunner processRunner, StartApps startApps, AppWindows appWindows, IAppsMap appsMap)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(startApps);
        ArgumentNullException.ThrowIfNull(appWindows);
        ArgumentNullException.ThrowIfNull(appsMap);

        this.processRunner = processRunner;
        this.startApps = startApps;
        this.appWindows = appWindows;
        this.appsMap = appsMap;
    }

    /// <inheritdoc />
    public async Task<AppLaunch> LaunchAsync(string app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);

        IReadOnlyList<StartApp> apps = await this.startApps.GetAsync(cancellationToken).ConfigureAwait(false);
        StartApp? found = this.Find(app, apps);
        if (found is null)
        {
            return new AppLaunch(AppLaunchOutcome.NotInstalled, $"No app in the Start menu is called {app}.");
        }

        // An app with a window open is not opened again: Windows would give a Notepad, a Chrome or a
        // VS Code another window of its own. Entering a profile brings back what leaving it minimised,
        // and that is all an app already open needs. One only in the tray, or with a window Windows
        // cloaks - a suspended Store app's - has nothing to show, and is opened as usual.
        if (this.appWindows.Of(app) is { } open && open.Any(window => !window.IsCloaked))
        {
            this.appWindows.Restore(app);
            return new AppLaunch(AppLaunchOutcome.Launched, string.Empty);
        }

        // File Explorer ends with 1 whether or not the app opened, so its exit code says nothing; only
        // it not ending does.
        ProcessResult result = await this.processRunner.RunAsync(
            new ProcessStartOptions("explorer", [$@"shell:AppsFolder\{found.AppId}"], timeout), cancellationToken).ConfigureAwait(false);
        return result.TimedOut
            ? new AppLaunch(AppLaunchOutcome.Failed, $"{app} didn't open in time.")
            : new AppLaunch(AppLaunchOutcome.Launched, string.Empty);
    }

    /// <summary>
    /// The Start menu's app for <paramref name="app"/>: the one the <c>apps</c> map says by its AppUserModelID, if
    /// the Start menu still lists it - so an app the Start menu has since renamed is still found, and a person
    /// can say which of two of one name is meant - else the one of that name. An id the Start menu doesn't
    /// list is no app to open, and falls back to the name.
    /// </summary>
    private StartApp? Find(string app, IReadOnlyList<StartApp> apps)
    {
        if (this.appsMap.Of(app.Trim())?.WindowsAppId?.Trim() is { Length: > 0 } id
            && apps.FirstOrDefault(candidate => string.Equals(candidate.AppId, id, StringComparison.OrdinalIgnoreCase)) is { } mapped)
        {
            return mapped;
        }

        return apps.FirstOrDefault(candidate => string.Equals(candidate.Name, app.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
