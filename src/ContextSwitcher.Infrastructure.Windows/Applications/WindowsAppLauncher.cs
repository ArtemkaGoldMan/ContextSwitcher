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

    public WindowsAppLauncher(IProcessRunner processRunner, StartApps startApps, AppWindows appWindows)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(startApps);
        ArgumentNullException.ThrowIfNull(appWindows);

        this.processRunner = processRunner;
        this.startApps = startApps;
        this.appWindows = appWindows;
    }

    /// <inheritdoc />
    public async Task<AppLaunch> LaunchAsync(string app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);

        IReadOnlyList<StartApp> apps = await this.startApps.GetAsync(cancellationToken).ConfigureAwait(false);
        StartApp? found = apps.FirstOrDefault(candidate => string.Equals(candidate.Name, app.Trim(), StringComparison.OrdinalIgnoreCase));
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
}
