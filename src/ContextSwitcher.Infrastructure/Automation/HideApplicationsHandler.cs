using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>
/// Hides the apps the context being left lists, that are running. Instant, with no app able to refuse
/// the way one can refuse to quit over unsaved work, so it needs none of the waiting closing does.
/// </summary>
public sealed class HideApplicationsHandler : IStepHandler
{
    private readonly IApplicationHider hider;
    private readonly IClock clock;

    public HideApplicationsHandler(IApplicationHider hider, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(hider);
        ArgumentNullException.ThrowIfNull(clock);

        this.hider = hider;
        this.clock = clock;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.HideApplications;

    /// <inheritdoc />
    public Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;
        int asked = this.hider.Hide(StepArguments.List(step.Arguments, "apps"));
        return Task.FromResult(StepResults.Succeeded(step, $"Asked {asked} running app(s) to hide.", startedAt, this.clock.UtcNow));
    }
}
