using System.Diagnostics;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>
/// Ends, without asking, the apps the context being left lists to force quit - the ones that stay on in
/// the tray when asked to quit and have no work to lose. Only the apps the profile names: nothing is
/// ended that it doesn't list, and what Windows can't run without is never ended
/// (<see cref="IAppProcesses.ForceQuit"/>).
/// </summary>
public sealed class ForceQuitApplicationsHandler : IStepHandler
{
    private readonly IAppProcesses processes;
    private readonly IClock clock;
    private readonly QuitTimings timings;

    /// <summary>A handler that ends apps through <paramref name="processes"/> and watches them go.</summary>
    /// <param name="timings">How often an app is looked for while it goes, and how many looks it gets.</param>
    public ForceQuitApplicationsHandler(IAppProcesses processes, IClock clock, QuitTimings timings)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timings);

        this.processes = processes;
        this.clock = clock;
        this.timings = timings;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.ForceQuitApplications;

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;
        IReadOnlyList<string> apps = AppsArguments.From(step.Arguments).Apps;
        TimeSpan perApp = step.Timeout / Math.Max(apps.Count, 1);

        // Ended together, as quitting is: each app has its own share of the step to be seen going in.
        (string App, bool Gone)[] outcomes = await Task.WhenAll(apps.Select(async app =>
        {
            this.processes.ForceQuit(app);
            return (app, await this.WaitUntilGoneAsync(app, perApp, cancellationToken).ConfigureAwait(false));
        })).ConfigureAwait(false);

        List<UserMessage> problems = outcomes
            .Where(outcome => !outcome.Gone)
            .Select(outcome => SwitchMessages.CouldNotForceQuit.With(outcome.App))
            .ToList();
        DateTimeOffset completedAt = this.clock.UtcNow;
        return problems.Count == 0
            ? StepResults.Succeeded(step, "All applications ended.", startedAt, completedAt)
            : StepResults.Degraded(step, problems, string.Empty, startedAt, completedAt);
    }

    /// <summary>
    /// Looks for <paramref name="app"/> until it has gone or <paramref name="window"/> is up. An app
    /// that can't be told to be gone is not taken for gone.
    /// </summary>
    private async Task<bool> WaitUntilGoneAsync(string app, TimeSpan window, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int attempt = 0; attempt < this.timings.MaxLooks; attempt++)
        {
            if (this.processes.IsRunning(app) == false)
            {
                return true;
            }

            if (window - elapsed.Elapsed <= this.timings.LookEvery)
            {
                break;
            }

            await Task.Delay(this.timings.LookEvery, cancellationToken).ConfigureAwait(false);
        }

        return this.processes.IsRunning(app) == false;
    }
}
