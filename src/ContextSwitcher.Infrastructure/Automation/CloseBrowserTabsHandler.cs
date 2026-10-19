using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Infrastructure.Browser;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Closes the tabs of the pages the context being left lists to close.</summary>
public sealed class CloseBrowserTabsHandler : IStepHandler
{
    private readonly IBrowserLauncher browserLauncher;
    private readonly IClock clock;

    public CloseBrowserTabsHandler(IBrowserLauncher browserLauncher, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(browserLauncher);
        ArgumentNullException.ThrowIfNull(clock);

        this.browserLauncher = browserLauncher;
        this.clock = clock;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.CloseBrowserTabs;

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;

        CloseTabsArguments arguments = CloseTabsArguments.From(step.Arguments);
        TabClosingOutcome outcome = await this.browserLauncher
            .CloseTabsAsync(
                arguments.Browser, arguments.CloseUrls, arguments.KeepUrls, arguments.ContextId, StepResults.WithReportingHeadroom(step.Timeout), cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset completedAt = this.clock.UtcNow;
        return outcome.Problem is null
            ? StepResults.Succeeded(step, $"Closed {outcome.Closed} tab(s).", startedAt, completedAt)
            : StepResults.Degraded(step, outcome.Problem, standardError: string.Empty, startedAt, completedAt);
    }
}
