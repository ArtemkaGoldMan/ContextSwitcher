using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>
/// Runs the steps of one <see cref="AutomationStepType"/>. <see cref="AutomationStepExecutor"/> hands
/// each step to the handler for its type, so a new type of step is a new handler, registered with the
/// others, and no change to the executor.
/// </summary>
public interface IStepHandler
{
    /// <summary>The type of step this runs.</summary>
    AutomationStepType Type { get; }

    /// <summary>Runs <paramref name="step"/>, as <see cref="IAutomationStepExecutor.ExecuteAsync"/> promises.</summary>
    Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken);
}
