using System.Text;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Opens the apps the context switched to lists, and says which didn't open, and why.</summary>
public sealed class LaunchApplicationsHandler : IStepHandler
{
    private readonly IAppLauncher launcher;
    private readonly IClock clock;

    public LaunchApplicationsHandler(IAppLauncher launcher, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(clock);

        this.launcher = launcher;
        this.clock = clock;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.LaunchApplications;

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;
        IReadOnlyList<string> apps = AppsArguments.From(step.Arguments).Apps;
        List<string> failedApps = [];
        List<string> notInstalled = [];
        StringBuilder standardError = new();

        // The same per-app split as closing: the plan builds 15s x app count, so no single slow
        // launch may spend the budget the apps after it still need.
        TimeSpan perApp = StepResults.WithReportingHeadroom(step.Timeout) / Math.Max(apps.Count, 1);

        // Launched concurrently for the same reason as the quits: each launch is independent of the
        // others, and the per-app budget means running them together costs the slowest launch rather
        // than their total.
        (string App, AppLaunch Launch)[] outcomes = await Task.WhenAll(
            apps.Select(async app => (app, await this.launcher.LaunchAsync(app, perApp, cancellationToken).ConfigureAwait(false))))
            .ConfigureAwait(false);

        foreach ((string app, AppLaunch launch) in outcomes)
        {
            if (launch.Outcome != AppLaunchOutcome.Launched)
            {
                // An app that isn't installed - a typo, or an app since removed - is said apart from a
                // launch that failed, since what to do about it is different.
                (launch.Outcome == AppLaunchOutcome.NotInstalled ? notInstalled : failedApps).Add(app);
                StepResults.AppendIfPresent(standardError, app, launch.Detail);
            }
        }

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (failedApps.Count == 0 && notInstalled.Count == 0)
        {
            return StepResults.Succeeded(step, "All applications launched.", startedAt, completedAt);
        }

        List<UserMessage> problems = [];
        if (notInstalled.Count > 0)
        {
            problems.Add(SwitchMessages.AppsNotInstalled.With(string.Join(", ", notInstalled)));
        }

        if (failedApps.Count > 0)
        {
            problems.Add(SwitchMessages.CouldNotLaunch.With(string.Join(", ", failedApps)));
        }

        return StepResults.Degraded(step, problems, standardError.ToString(), startedAt, completedAt);
    }
}
