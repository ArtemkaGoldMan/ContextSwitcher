using System.Diagnostics;
using System.Text;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Asks the apps the context being left lists to quit, and says which of them stayed.</summary>
public sealed class CloseApplicationsHandler : IStepHandler
{
    private readonly IAppQuitter quitter;
    private readonly IAppProcesses processes;
    private readonly IClock clock;
    private readonly QuitTimings timings;
    private readonly PlatformCapabilities capabilities;
    private readonly ILogger logger;

    /// <summary>A handler that asks apps to quit through <paramref name="quitter"/> and looks for them through <paramref name="processes"/>.</summary>
    /// <param name="timings">How an app's share of the step is spent, and how often it is looked for.</param>
    /// <param name="capabilities">Whether an app that has closed its windows and runs on is a failure to quit, or the tray.</param>
    /// <param name="logger">Records an app left running in the tray.</param>
    public CloseApplicationsHandler(
        IAppQuitter quitter,
        IAppProcesses processes,
        IClock clock,
        QuitTimings timings,
        PlatformCapabilities capabilities,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(quitter);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timings);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(logger);

        this.quitter = quitter;
        this.processes = processes;
        this.clock = clock;
        this.timings = timings;
        this.capabilities = capabilities;
        this.logger = logger;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.CloseApplications;

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;
        IReadOnlyList<string> apps = AppsArguments.From(step.Arguments).Apps;
        List<UserMessage> problems = [];
        StringBuilder standardError = new();

        // Quitting is given ten seconds an app (StepBudgets), which the plan builder encodes as
        // Timeout = 10s x app count. Split it back out: a graceful quit can block indefinitely on a
        // modal "save this document?" sheet, and handing every script the whole step budget meant
        // one such app consumed all of it. The step-level timeout then fired mid-quit, so the
        // warning below never ran and every later app in the list was never even asked to quit.
        //
        // Part of an app's share goes to the quit itself, and most of the rest to confirming it went:
        // together they stay inside the share, so the step's own timeout never cuts the report off.
        TimeSpan perApp = step.Timeout / Math.Max(apps.Count, 1);
        TimeSpan quitTimeout = perApp * this.timings.QuitShare;
        TimeSpan confirmWindow = perApp * this.timings.WatchShare;

        // The apps are quit concurrently. The budget above is already per app, so running them
        // together makes the step cost the slowest single quit instead of the sum of all of them -
        // on six apps that is the difference between about 1.2s and 0.2s. They stay one quit per
        // app rather than one for all of them precisely so an app sitting on a modal "save this
        // document?" sheet still only spends its own budget, which is what the split above exists
        // to guarantee. Task.WhenAll preserves the input order, so the message below names the apps
        // in configured order however the quits interleave.
        (string App, string Error, bool StillRunning)[] outcomes = await Task.WhenAll(
            apps.Select(async app =>
            {
                string error = await this.quitter.QuitAsync(app, quitTimeout, cancellationToken).ConfigureAwait(false);

                bool isStillRunning = await this.IsStillRunningAfterQuitAsync(app, confirmWindow, cancellationToken)
                    .ConfigureAwait(false);

                return (app, error, isStillRunning);
            })).ConfigureAwait(false);

        foreach ((string app, string error, bool isStillRunning) in outcomes)
        {
            StepResults.AppendIfPresent(standardError, app, error);

            // One line per app, for what came of it can differ: an app with a window on screen may be
            // asking to save; one with none is finishing - or staying on in the background, which
            // ClosingAppsFollowUp tells apart once it has watched a while. Every app answers "user
            // canceled" (-128) to being asked to quit when it quits its own way, so that answer says
            // nothing either way; the window does.
            if (isStillRunning)
            {
                bool noWindow = this.processes.HasWindowOnScreen(app) == false;

                // Where closing a window is not quitting, an app with none left that runs on is in
                // the tray, as it was made to be: nothing for the person to do, and a warning on
                // every switch for it would only teach them to ignore warnings. A profile can have
                // such an app ended by force instead.
                if (noWindow && this.capabilities.AppsLiveInTray)
                {
                    this.logger.Record("ContextSwitch", "AppInTray", $"{app} closed its windows and is still running, as an app in the tray does.");
                    continue;
                }

                problems.Add(noWindow ? SwitchMessages.StillClosing.With(app) : SwitchMessages.CouldNotClose.With(app));
            }
        }

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (problems.Count == 0)
        {
            return StepResults.Succeeded(step, "All applications closed.", startedAt, completedAt);
        }

        return StepResults.Degraded(step, problems, standardError.ToString(), startedAt, completedAt);
    }

    /// <summary>
    /// Looks for <paramref name="app"/> again and again until it has gone or
    /// <paramref name="window"/> is up, and says whether it is still running. Only a definite "not
    /// running" ends the wait early: a check that timed out or failed says nothing either way.
    /// </summary>
    private async Task<bool> IsStillRunningAfterQuitAsync(string app, TimeSpan window, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int attempt = 0; attempt < this.timings.MaxLooks; attempt++)
        {
            TimeSpan remaining = window - elapsed.Elapsed;
            if (attempt > 0 && remaining <= TimeSpan.Zero)
            {
                break;
            }

            TimeSpan checkTimeout = remaining < this.timings.MaxLook && remaining > TimeSpan.Zero ? remaining : this.timings.MaxLook;
            bool? running = await this.quitter.IsRunningAsync(app, checkTimeout, cancellationToken).ConfigureAwait(false);
            if (running == false)
            {
                return false;
            }

            if (window - elapsed.Elapsed <= this.timings.LookEvery)
            {
                break;
            }

            await Task.Delay(this.timings.LookEvery, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
