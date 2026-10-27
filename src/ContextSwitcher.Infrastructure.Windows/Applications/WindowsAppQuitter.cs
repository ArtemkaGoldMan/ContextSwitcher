using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Asks an app to quit as its close button does: <c>WM_CLOSE</c> to each of its main windows. An app
/// may ask to save first, or refuse, or - Slack, Teams, Discord - go to the tray and keep running;
/// whether it went is for <see cref="IsRunningAsync"/> and, past that, for Force quit.
/// </summary>
public sealed class WindowsAppQuitter : IAppQuitter
{
    private readonly AppIdentities identities;
    private readonly WindowsAppProcesses appProcesses;
    private readonly ISystemWindows windows;
    private readonly ISystemProcesses processes;

    public WindowsAppQuitter(AppIdentities identities, WindowsAppProcesses appProcesses, ISystemWindows windows, ISystemProcesses processes)
    {
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(appProcesses);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(processes);

        this.identities = identities;
        this.appProcesses = appProcesses;
        this.windows = windows;
        this.processes = processes;
    }

    /// <inheritdoc />
    public async Task<string> QuitAsync(string app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        if (await this.identities.FindAsync(app, cancellationToken).ConfigureAwait(false) is null)
        {
            return $"{app} isn't in the Start menu, or the Start menu doesn't say how to tell its processes.";
        }

        if (this.appProcesses.Look(app) is not { } look)
        {
            return $"Windows wouldn't list the processes running, so {app} was not asked to quit.";
        }

        if (look.Roots.Count == 0)
        {
            return string.Empty;
        }

        if (this.windows.List() is not { } all)
        {
            return $"Windows wouldn't list the windows open, so {app} was not asked to quit.";
        }

        HashSet<int> pids = AppProcessTree.WithChildren(look.Roots, look.All, this.processes.CurrentProcessId);
        List<WindowInfo> mainWindows = all
            .Where(window => pids.Contains(window.Pid) && window.IsVisible && !window.HasOwner && !window.IsToolWindow)
            .ToList();
        if (mainWindows.Count == 0)
        {
            return $"{app} has no window to close; it is running in the background.";
        }

        int refused = mainWindows.Count(window => !this.windows.RequestClose(window.Handle));
        return refused == 0 ? string.Empty : $"Windows wouldn't pass the request to close on to {refused} of {app}'s windows.";
    }

    /// <inheritdoc />
    public async Task<bool?> IsRunningAsync(string app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        await this.identities.FindAsync(app, cancellationToken).ConfigureAwait(false);
        return this.appProcesses.IsRunning(app);
    }
}
