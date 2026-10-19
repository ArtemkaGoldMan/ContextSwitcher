using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>
/// Runs a step by handing it to the <see cref="IStepHandler"/> for its type. A type with no handler -
/// open-links, write-state and analytics-boundary steps, which the plan never holds - is reported as
/// <c>Skipped</c> rather than throwing, so the pipeline stays inspectable end to end.
/// </summary>
public sealed class AutomationStepExecutor : IAutomationStepExecutor
{
    private readonly Dictionary<AutomationStepType, IStepHandler> handlers;
    private readonly IClock clock;

    public AutomationStepExecutor(IEnumerable<IStepHandler> handlers, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(clock);

        // Two handlers for one type is a mistake in the registrations, which this says at once.
        this.handlers = handlers.ToDictionary(handler => handler.Type);
        this.clock = clock;
    }

    /// <inheritdoc />
    public Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        return this.handlers.TryGetValue(step.Type, out IStepHandler? handler)
            ? handler.ExecuteAsync(step, cancellationToken)
            : this.NotImplemented(step);
    }

    private Task<AutomationResult> NotImplemented(AutomationStep step)
    {
        DateTimeOffset now = this.clock.UtcNow;
        return Task.FromResult(new AutomationResult(
            step.Id, step.Type, AutomationResultStatus.Skipped,
            $"{step.Type} automation is not implemented yet.", null, null, null, now, now));
    }
}
