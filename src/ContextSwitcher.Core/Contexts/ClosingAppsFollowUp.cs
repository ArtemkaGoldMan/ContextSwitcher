using System.Diagnostics;
using System.Globalization;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Keeps an eye on the apps a switch asked to quit that were still running when it finished. Some
/// take half a minute to go - Autodesk Fusion does - and a switch does not wait that long; some close
/// their windows and stay on in the background - Steam, at times. Each one that goes has its line
/// taken off the switch's warnings. Each still running when <see cref="WatchFor"/> is up gets a line
/// saying which it is: with a window, it may be asking to save; without, it stayed in the background,
/// and the person can have it ended by force (<see cref="ForceQuitAsync"/>). Nothing is ever ended
/// without being asked.
/// </summary>
public sealed class ClosingAppsFollowUp
{
    /// <summary>How long the apps are watched after the switch.</summary>
    public static readonly TimeSpan DefaultWatchFor = TimeSpan.FromMinutes(2);

    /// <summary>How often they are looked at.</summary>
    public static readonly TimeSpan DefaultPollEvery = TimeSpan.FromSeconds(2);

    /// <summary>How long a force quit is given to take effect before it counts as not having worked.</summary>
    public static readonly TimeSpan DefaultForceQuitWait = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ForceQuitPoll = TimeSpan.FromMilliseconds(100);

    private readonly ContextSwitchService switches;
    private readonly IAppProcesses processes;
    private readonly IClock clock;
    private readonly TimeSpan pollEvery;
    private readonly TimeSpan forceQuitWait;
    private readonly Lock gate = new();
    private CancellationTokenSource? watching;

    /// <param name="watchFor">Overrides <see cref="DefaultWatchFor"/>, for tests.</param>
    /// <param name="pollEvery">Overrides <see cref="DefaultPollEvery"/>, for tests.</param>
    /// <param name="forceQuitWait">Overrides <see cref="DefaultForceQuitWait"/>, for tests.</param>
    public ClosingAppsFollowUp(
        ContextSwitchService switches,
        IAppProcesses processes,
        IClock clock,
        TimeSpan? watchFor = null,
        TimeSpan? pollEvery = null,
        TimeSpan? forceQuitWait = null)
    {
        ArgumentNullException.ThrowIfNull(switches);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(clock);

        this.switches = switches;
        this.processes = processes;
        this.clock = clock;
        this.WatchFor = watchFor ?? DefaultWatchFor;
        this.pollEvery = pollEvery ?? DefaultPollEvery;
        this.forceQuitWait = forceQuitWait ?? DefaultForceQuitWait;
    }

    /// <summary>Raised, on a background thread, with the state as written each time a line changed.</summary>
    public event EventHandler<CurrentContextState>? StateAmended;

    public TimeSpan WatchFor { get; }

    /// <summary>The watch going on now, finished when there is none - for a test to wait on.</summary>
    public Task Watching { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Starts watching the apps <paramref name="state"/>'s switch left running, in place of whatever
    /// an earlier switch left: its lines are gone with it.
    /// </summary>
    public void Watch(CurrentContextState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // A newer switch ends the watch on an older one even with nothing of its own to watch: the
        // older one's lines are gone with it.
        lock (this.gate)
        {
            this.watching?.Cancel();
            this.watching = null;
            if (state.LastSwitchCompletedAt is not { } completedAt || ClosingApps.StillRunning(state.LastErrors) is not { Count: > 0 } apps)
            {
                return;
            }

            CancellationTokenSource next = new();
            this.watching = next;
            this.Watching = Task.Run(() => this.WatchAsync(completedAt, apps, state.LastErrors, next.Token));
        }
    }

    /// <summary>
    /// Ends <paramref name="app"/> by force, as the person asked, and changes its line to match:
    /// gone when it went, "could not" when it is still there after a few seconds.
    /// </summary>
    /// <returns>Whether it is gone.</returns>
    public async Task<bool> ForceQuitAsync(DateTimeOffset completedAt, string app, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        bool asked = this.processes.ForceQuit(app);
        Stopwatch waited = Stopwatch.StartNew();
        while (asked && this.processes.IsRunning(app) != false && waited.Elapsed < this.forceQuitWait)
        {
            await Task.Delay(ForceQuitPoll, cancellationToken).ConfigureAwait(false);
        }

        bool gone = this.processes.IsRunning(app) == false;
        AppLog.Record(
            "ContextSwitch",
            gone ? "AppForceQuit" : "AppForceQuitFailed",
            gone ? $"{app} was force quit, as asked." : $"{app} was still running after being force quit.");

        await this.AmendAsync(
            completedAt,
            errors => gone ? ClosingApps.Quit(errors, app) : ClosingApps.ForceQuitFailed(errors, app),
            cancellationToken).ConfigureAwait(false);
        return gone;
    }

    private async Task WatchAsync(DateTimeOffset completedAt, IReadOnlyList<string> apps, IReadOnlyList<StateError> errors, CancellationToken cancellationToken)
    {
        // Only an app found running now can be seen going. One the switch said was still closing was
        // found then - its windows were counted - so not finding it now means it has quit. One it said
        // didn't quit may simply be called something the running apps don't go by, and is left as is.
        List<string> pending = apps
            .Where(app => this.processes.IsRunning(app) == true || ClosingApps.WasClosing(errors, app))
            .ToList();
        Stopwatch watched = Stopwatch.StartNew();
        try
        {
            while (pending.Count > 0 && watched.Elapsed < this.WatchFor)
            {
                await Task.Delay(this.pollEvery, cancellationToken).ConfigureAwait(false);
                foreach (string app in pending.ToList())
                {
                    if (this.processes.IsRunning(app) == false)
                    {
                        pending.Remove(app);
                        await this.QuitAsync(completedAt, app, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            foreach (string app in pending)
            {
                if (this.processes.IsRunning(app) == false)
                {
                    await this.QuitAsync(completedAt, app, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Unknown counts as a window: "may be asking to save" is the safer thing to say, and
                // offers nothing to end by force.
                bool hasWindow = this.processes.HasWindow(app) ?? true;
                AppLog.Record(
                    "ContextSwitch",
                    "AppStillRunning",
                    hasWindow
                        ? $"{app} was still running {this.SecondsSince(completedAt)} seconds after the switch, with a window open."
                        : $"{app} was still running {this.SecondsSince(completedAt)} seconds after the switch, with no window: it stayed in the background.");
                await this.AmendAsync(completedAt, errors => ClosingApps.Stayed(errors, app, hasWindow), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Not logged: a newer switch took over, and its apps are the ones watched now.
        }
    }

    private async Task QuitAsync(DateTimeOffset completedAt, string app, CancellationToken cancellationToken)
    {
        AppLog.Record("ContextSwitch", "AppQuitLater", $"{app} quit {this.SecondsSince(completedAt)} seconds after the switch.");
        await this.AmendAsync(completedAt, errors => ClosingApps.Quit(errors, app), cancellationToken).ConfigureAwait(false);
    }

    private async Task AmendAsync(
        DateTimeOffset completedAt,
        Func<IReadOnlyList<StateError>, IReadOnlyList<StateError>> amend,
        CancellationToken cancellationToken)
    {
        if (await this.switches.AmendLastSwitchAsync(completedAt, amend, cancellationToken).ConfigureAwait(false) is { } amended)
        {
            this.StateAmended?.Invoke(this, amended);
        }
    }

    private string SecondsSince(DateTimeOffset moment) =>
        Math.Max(0, (int)(this.clock.UtcNow - moment).TotalSeconds).ToString(CultureInfo.InvariantCulture);
}
