using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// The apps open on the taskbar now, for the list a switch asks about: the Start menu's apps that are
/// running with a main window, other than this app. Named as the Start menu names them, which is how a
/// profile lists an app. An app in the tray with no window isn't on the taskbar and isn't listed; File
/// Explorer has no identity and isn't either. One look at the processes and the windows serves every app.
/// </summary>
public sealed class WindowsRunningApplications : IRunningApplications
{
    private readonly StartApps startApps;
    private readonly AppIdentities identities;
    private readonly ISystemProcesses processes;
    private readonly ISystemWindows windows;

    public WindowsRunningApplications(StartApps startApps, AppIdentities identities, ISystemProcesses processes, ISystemWindows windows)
    {
        ArgumentNullException.ThrowIfNull(startApps);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(windows);

        this.startApps = startApps;
        this.identities = identities;
        this.processes = processes;
        this.windows = windows;
    }

    /// <inheritdoc />
    public IReadOnlyList<RunningApp> List()
    {
        IReadOnlyList<StartApp> known = this.startApps.Known;
        if (known.Count == 0 || this.processes.List() is not { } all || this.windows.List() is not { } open)
        {
            return [];
        }

        HashSet<int> withWindow = open.Where(window => AppWindows.IsMain(window) && !window.IsCloaked).Select(window => window.Pid).ToHashSet();
        int self = this.processes.CurrentProcessId;
        List<RunningApp> running = [];
        foreach (StartApp app in known)
        {
            if (this.identities.IdentityOf(app) is not { } identity)
            {
                continue;
            }

            List<ProcessInfo> roots = AppProcessTree.Of(identity, all, self);
            if (roots.Count > 0 && AppProcessTree.WithChildren(roots, all, self).Any(withWindow.Contains))
            {
                running.Add(new RunningApp(app.Name));
            }
        }

        return running;
    }
}
