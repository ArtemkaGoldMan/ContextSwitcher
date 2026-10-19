using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Infrastructure.Browser;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Opens the pages, or the browser profiles, of the context switched to.</summary>
public sealed class ManageBrowserContextHandler : IStepHandler
{
    private readonly IBrowserLauncher browserLauncher;
    private readonly IClock clock;

    public ManageBrowserContextHandler(IBrowserLauncher browserLauncher, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(browserLauncher);
        ArgumentNullException.ThrowIfNull(clock);

        this.browserLauncher = browserLauncher;
        this.clock = clock;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.ManageBrowserContext;

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;

        BrowserContextArguments arguments = BrowserContextArguments.From(step.Arguments);
        BrowserContextRequest request = new(
            arguments.Mode,
            arguments.Browser,
            arguments.Urls,
            arguments.AvoidDuplicateTabs,
            arguments.Profiles,
            StepResults.WithReportingHeadroom(step.Timeout),
            arguments.ContextId,
            arguments.RememberUrls);
        BrowserLaunchOutcome outcome = await this.browserLauncher.ManageBrowserContextAsync(request, cancellationToken).ConfigureAwait(false);

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (outcome.Warnings.Count == 0)
        {
            return StepResults.Succeeded(step, "Browser context managed.", startedAt, completedAt);
        }

        return StepResults.Degraded(step, outcome.Warnings, standardError: string.Empty, startedAt, completedAt);
    }
}
