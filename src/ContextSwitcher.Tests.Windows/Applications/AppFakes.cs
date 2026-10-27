using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Windows.Applications;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>A PC with the processes a test gives it, which ends only what the test says Windows would.</summary>
internal sealed class FakeSystemProcesses : ISystemProcesses
{
    /// <summary>Process ids that were ended.</summary>
    public List<int> Terminated { get; } = [];

    /// <summary>Process ids Windows refuses to end.</summary>
    public HashSet<int> Unendable { get; } = [];

    public List<ProcessInfo>? Running { get; set; } = [];

    public int CurrentProcessId { get; set; } = 1;

    public IReadOnlyList<ProcessInfo>? List() => this.Running;

    public bool Terminate(int pid)
    {
        if (this.Unendable.Contains(pid))
        {
            return false;
        }

        this.Terminated.Add(pid);
        return true;
    }
}

/// <summary>A desktop with the windows a test gives it, which records the ones asked to close.</summary>
internal sealed class FakeSystemWindows : ISystemWindows
{
    public List<nint> AskedToClose { get; } = [];

    public List<nint> Minimized { get; } = [];

    public List<nint> Restored { get; } = [];

    public HashSet<nint> Unreachable { get; } = [];

    public List<WindowInfo>? Open { get; set; } = [];

    public IReadOnlyList<WindowInfo>? List() => this.Open;

    public bool RequestClose(nint handle)
    {
        if (this.Unreachable.Contains(handle))
        {
            return false;
        }

        this.AskedToClose.Add(handle);
        return true;
    }

    public bool Minimize(nint handle)
    {
        this.Minimized.Add(handle);
        return !this.Unreachable.Contains(handle);
    }

    public bool Restore(nint handle)
    {
        this.Restored.Add(handle);
        return !this.Unreachable.Contains(handle);
    }
}

/// <summary>Known folders a test names.</summary>
internal sealed class FakeKnownFolders : IKnownFolders
{
    public Dictionary<Guid, string> Folders { get; } = [];

    public string? PathOf(Guid folder) => this.Folders.GetValueOrDefault(folder);
}

/// <summary>A Start menu, listed through a fake PowerShell, and everything that is built on it.</summary>
internal sealed class StartMenuRig
{
    public static readonly StartAppsOptions Options = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), StartAppsFilter.Default);

    public StartMenuRig(params StartApp[] apps)
    {
        string listing = "[" + string.Join(",", apps.Select(app => $$"""{"n":"{{Encode(app.Name)}}","i":"{{Encode(app.AppId)}}","t":"{{Encode(app.Target ?? string.Empty)}}"}""")) + "]";
        this.Runner.Enqueue(new ProcessResult(0, listing, string.Empty, TimedOut: false));
        this.StartApps = new StartApps(this.Runner, this.Clock, this.Logger, Options);
        this.Identities = new AppIdentities(this.StartApps, this.Folders);
        this.Processes = new WindowsAppProcesses(this.Identities, this.Pc, this.Windows, AppProcessesOptions.Default, this.Logger);
        this.Quitter = new WindowsAppQuitter(this.Identities, this.Processes, this.Windows, this.Pc);
        this.AppWindows = new AppWindows(this.Processes, this.Windows, this.Pc);
        this.Hider = new WindowsApplicationHider(this.AppWindows);
        this.Running = new WindowsRunningApplications(this.StartApps, this.Identities, this.Pc, this.Windows);
        this.Launcher = new WindowsAppLauncher(this.Runner, this.StartApps, this.AppWindows);
    }

    public FakeProcessRunner Runner { get; } = new();

    public FakeClock Clock { get; } = new();

    public TestLogger Logger { get; } = new();

    public FakeKnownFolders Folders { get; } = new();

    public FakeSystemProcesses Pc { get; } = new();

    public FakeSystemWindows Windows { get; } = new();

    public StartApps StartApps { get; }

    public AppIdentities Identities { get; }

    public WindowsAppProcesses Processes { get; }

    public WindowsAppQuitter Quitter { get; }

    public AppWindows AppWindows { get; }

    public WindowsApplicationHider Hider { get; }

    public WindowsRunningApplications Running { get; }

    public WindowsAppLauncher Launcher { get; }

    /// <summary>Lists the Start menu, as opening or quitting an app does before anything else.</summary>
    public Task ListAsync() => this.StartApps.GetAsync(CancellationToken.None);

    public static string Encode(string text) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));

    public static ProcessInfo Process(int pid, int parent, string? image, string? package = null, DateTime? started = null) =>
        new(pid, parent, image, package, started ?? new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc).AddSeconds(pid));

    public static WindowInfo Window(nint handle, int pid, bool visible = true, bool cloaked = false, bool minimized = false, bool owned = false, bool tool = false, int width = 800, int height = 600) =>
        new(handle, pid, visible, cloaked, minimized, owned, tool, width, height);
}
