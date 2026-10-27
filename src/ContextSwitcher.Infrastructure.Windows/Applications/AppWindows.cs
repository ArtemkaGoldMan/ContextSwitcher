namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// An app's main windows - the ones on its taskbar button - and minimising and restoring them: what
/// hiding an app is on Windows, which has no Hide as the Mac has, and what entering a profile again
/// undoes. A main window is shown, owned by no other window, and not a palette; a dialog belongs to
/// the app that asked it. Windows of the processes the app started count as its own.
/// </summary>
public sealed class AppWindows
{
    private readonly WindowsAppProcesses processes;
    private readonly ISystemWindows windows;
    private readonly ISystemProcesses system;

    public AppWindows(WindowsAppProcesses processes, ISystemWindows windows, ISystemProcesses system)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(system);

        this.processes = processes;
        this.windows = windows;
        this.system = system;
    }

    /// <summary>Whether <paramref name="window"/> is one a close button, or a minimise button, belongs to.</summary>
    public static bool IsMain(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return window.IsVisible && !window.HasOwner && !window.IsToolWindow;
    }

    /// <summary>
    /// The main windows of <paramref name="app"/>; empty when it isn't running, null when that can't
    /// be told.
    /// </summary>
    public IReadOnlyList<WindowInfo>? Of(string app)
    {
        if (this.processes.Look(app) is not { } look || this.windows.List() is not { } all)
        {
            return null;
        }

        if (look.Roots.Count == 0)
        {
            return [];
        }

        HashSet<int> pids = AppProcessTree.WithChildren(look.Roots, look.All, this.system.CurrentProcessId);
        return all.Where(window => pids.Contains(window.Pid) && IsMain(window)).ToList();
    }

    /// <summary>
    /// Minimises the main windows of <paramref name="app"/> that are on screen. One on another virtual
    /// desktop is left, as is one already minimised. Returns whether the app is running.
    /// </summary>
    public bool Minimize(string app)
    {
        if (this.Of(app) is not { Count: > 0 } open)
        {
            return false;
        }

        foreach (WindowInfo window in open.Where(window => !window.IsMinimized && !window.IsCloaked))
        {
            this.windows.Minimize(window.Handle);
        }

        return true;
    }

    /// <summary>Brings back the minimised main windows of <paramref name="app"/>. Returns how many were asked.</summary>
    public int Restore(string app)
    {
        if (this.Of(app) is not { } open)
        {
            return 0;
        }

        int asked = 0;
        foreach (WindowInfo window in open.Where(window => window.IsMinimized))
        {
            if (this.windows.Restore(window.Handle))
            {
                asked++;
            }
        }

        return asked;
    }
}
