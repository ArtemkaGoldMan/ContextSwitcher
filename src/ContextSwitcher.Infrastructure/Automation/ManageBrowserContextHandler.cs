using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Browser;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Opens the pages, or the browser profiles, of the context switched to.</summary>
public sealed class ManageBrowserContextHandler : IStepHandler
{
    private readonly BrowserLauncher browserLauncher;
    private readonly IClock clock;

    public ManageBrowserContextHandler(BrowserLauncher browserLauncher, IClock clock)
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

        BrowserManagementMode mode = Enum.Parse<BrowserManagementMode>(step.Arguments.GetValueOrDefault("mode", nameof(BrowserManagementMode.None)));
        BrowserKind browser = Enum.Parse<BrowserKind>(step.Arguments.GetValueOrDefault("browser", nameof(BrowserKind.Default)));
        string[] urls = StepArguments.List(step.Arguments, "urls");
        bool avoidDuplicateTabs = step.Arguments.GetValueOrDefault("avoidDuplicateTabs") == "True";
        IReadOnlyList<BrowserProfileConfig> profiles = StepArguments.Profiles(step.Arguments.GetValueOrDefault("profilesJson", "[]"));

        BrowserContextRequest request = new(
            mode,
            browser,
            urls,
            avoidDuplicateTabs,
            profiles,
            StepResults.WithReportingHeadroom(step.Timeout),
            step.Arguments.GetValueOrDefault("contextId"),
            StepArguments.Strings(step.Arguments.GetValueOrDefault("rememberUrlsJson", "[]")));
        BrowserLaunchOutcome outcome = await this.browserLauncher.ManageBrowserContextAsync(request, cancellationToken).ConfigureAwait(false);

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (outcome.Warnings.Count == 0)
        {
            return StepResults.Succeeded(step, "Browser context managed.", startedAt, completedAt);
        }

        return StepResults.Degraded(step, outcome.Warnings, standardError: string.Empty, startedAt, completedAt);
    }
}
