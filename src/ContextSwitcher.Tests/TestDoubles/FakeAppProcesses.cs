using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>
/// Apps that run, quit after a number of looks, have windows or not, and give way to a force quit or
/// don't - nothing on this Mac is looked at or ended. An app it was told nothing about is unknown.
/// </summary>
public sealed class FakeAppProcesses : IAppProcesses
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, bool> running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> quitsAfterLooks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ignoresForceQuit = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> forceQuit = [];

    public IReadOnlyList<string> ForceQuitAsked
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.forceQuit];
            }
        }
    }

    /// <summary>Runs, with a window or not, and goes after <paramref name="quitsAfterLooks"/> looks if given.</summary>
    public FakeAppProcesses Running(string app, bool hasWindow = false, int? quitsAfterLooks = null, bool ignoresForceQuit = false)
    {
        lock (this.gate)
        {
            this.running[app] = true;
            this.windows[app] = hasWindow;
            if (quitsAfterLooks is { } looks)
            {
                this.quitsAfterLooks[app] = looks;
            }

            if (ignoresForceQuit)
            {
                this.ignoresForceQuit.Add(app);
            }
        }

        return this;
    }

    /// <summary>Was asked about and isn't running.</summary>
    public FakeAppProcesses Gone(string app)
    {
        lock (this.gate)
        {
            this.running[app] = false;
        }

        return this;
    }

    public bool? IsRunning(string app)
    {
        lock (this.gate)
        {
            if (!this.running.TryGetValue(app, out bool isRunning))
            {
                return null;
            }

            if (isRunning && this.quitsAfterLooks.TryGetValue(app, out int looks))
            {
                this.quitsAfterLooks[app] = looks - 1;
                if (looks <= 1)
                {
                    this.running[app] = false;
                }
            }

            return isRunning;
        }
    }

    public bool? HasWindowOnScreen(string app)
    {
        lock (this.gate)
        {
            return this.windows.TryGetValue(app, out bool hasWindow) ? hasWindow : null;
        }
    }

    public bool ForceQuit(string app)
    {
        lock (this.gate)
        {
            this.forceQuit.Add(app);
            bool wasRunning = this.running.TryGetValue(app, out bool isRunning) && isRunning;
            if (wasRunning && !this.ignoresForceQuit.Contains(app))
            {
                this.running[app] = false;
            }

            return wasRunning;
        }
    }
}
