using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Opens an app by the name it has in the Start menu, through File Explorer's <c>shell:AppsFolder</c>,
/// which starts a desktop app or a Store app the same way and brings one already running forward.
/// </summary>
public sealed class WindowsAppLauncher : IAppLauncher
{
    private readonly IProcessRunner processRunner;
    private readonly StartApps startApps;

    public WindowsAppLauncher(IProcessRunner processRunner, StartApps startApps)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(startApps);

        this.processRunner = processRunner;
        this.startApps = startApps;
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

        // File Explorer ends with 1 whether or not the app opened, so its exit code says nothing; only
        // it not ending does.
        ProcessResult result = await this.processRunner.RunAsync(
            new ProcessStartOptions("explorer", [$@"shell:AppsFolder\{found.AppId}"], timeout), cancellationToken).ConfigureAwait(false);
        return result.TimedOut
            ? new AppLaunch(AppLaunchOutcome.Failed, $"{app} didn't open in time.")
            : new AppLaunch(AppLaunchOutcome.Launched, string.Empty);
    }
}
