using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Asks about an app's processes by the name a profile stores - the Start menu's - as
/// <c>MacAppProcesses</c> does by the Dock's:
/// <list type="bullet">
/// <item>whether it still runs: any process that is its own, which <see cref="AppIdentity"/> tells;</item>
/// <item>whether it has a window on screen: a shown window of its processes or of those they started,
/// as Steam draws its windows in a helper it started. A window Windows cloaks - on another virtual
/// desktop, or a suspended Store app's - doesn't count, nor does a palette or a tray helper, nor
/// one under 100 pixels. A minimised window does: it is on the taskbar for the person to open;</item>
/// <item>and ending it: its processes and what they started, at once, other than the ones
/// <see cref="AppProcessesOptions.NeverEnded"/> lists and this app's own.</item>
/// </list>
/// What the Start menu said of the app is what the last listing found, so these answer "can't be
/// told" until something has listed it - which opening or quitting an app does first.
/// </summary>
public sealed class WindowsAppProcesses : IAppProcesses
{
    private readonly AppIdentities identities;
    private readonly ISystemProcesses processes;
    private readonly ISystemWindows windows;
    private readonly AppProcessesOptions options;
    private readonly ILogger logger;

    public WindowsAppProcesses(
        AppIdentities identities,
        ISystemProcesses processes,
        ISystemWindows windows,
        AppProcessesOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.identities = identities;
        this.processes = processes;
        this.windows = windows;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc />
    public bool? IsRunning(string app)
    {
        return this.Look(app) is { } look ? look.Roots.Count > 0 : null;
    }

    /// <inheritdoc />
    public bool? HasWindowOnScreen(string app)
    {
        if (this.Look(app) is not { } look)
        {
            return null;
        }

        if (look.Roots.Count == 0)
        {
            return false;
        }

        if (this.windows.List() is not { } all)
        {
            return null;
        }

        HashSet<int> pids = AppProcessTree.WithChildren(look.Roots, look.All, this.processes.CurrentProcessId);
        return all.Any(window => pids.Contains(window.Pid) && this.IsOnScreen(window));
    }

    /// <inheritdoc />
    public bool ForceQuit(string app)
    {
        if (this.Look(app) is not { } look || look.Roots.Count == 0)
        {
            return false;
        }

        HashSet<int> pids = AppProcessTree.WithChildren(look.Roots, look.All, this.processes.CurrentProcessId);
        foreach (ProcessInfo process in look.All.Where(process => pids.Contains(process.Pid)))
        {
            if (this.IsProtected(process))
            {
                this.logger.Warn("App", "ForceQuitSkipped", $"{app}: left {FileName(process)} running, as ending it would stop Windows.");
                continue;
            }

            if (!this.processes.Terminate(process.Pid))
            {
                this.logger.Warn("App", "ForceQuitFailed", $"{app}: Windows wouldn't end {FileName(process)} ({process.Pid}).");
            }
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="window"/> is one a person sees and works in: shown, not cloaked, not
    /// a palette, and big enough - or on the taskbar minimised.
    /// </summary>
    public bool IsOnScreen(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return window.IsVisible
            && !window.IsCloaked
            && !window.IsToolWindow
            && (window.IsMinimized || (window.Width >= this.options.SmallestWindow && window.Height >= this.options.SmallestWindow));
    }

    /// <summary>The app's own processes among all of them; null when the app can't be told or Windows can't be asked.</summary>
    internal AppLook? Look(string app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        if (this.identities.Find(app) is not { } identity || this.processes.List() is not { } all)
        {
            return null;
        }

        return new AppLook(all, AppProcessTree.Of(identity, all, this.processes.CurrentProcessId));
    }

    private static string FileName(ProcessInfo process) => process.ImagePath is null ? "a process" : Path.GetFileName(process.ImagePath);

    private bool IsProtected(ProcessInfo process) =>
        process.ImagePath is { } path
            && this.options.NeverEnded.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Every process, and those that are the app's.</summary>
    internal sealed record AppLook(IReadOnlyList<ProcessInfo> All, IReadOnlyList<ProcessInfo> Roots);
}
