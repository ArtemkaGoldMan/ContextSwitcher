using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Executes a single OS-level <see cref="AutomationStep"/>. Infrastructure maps the step's
/// <see cref="AutomationStepType"/> and <see cref="AutomationStep.Arguments"/> to real
/// <see cref="IProcessRunner"/>/<see cref="IScriptRunner"/> calls; Core only depends on this port.
/// </summary>
public interface IAutomationStepExecutor
{
    /// <summary>
    /// Executes the given step and returns its structured outcome. Must never throw for
    /// expected automation failures (missing app, permission denial, timeout) — those are
    /// reported through <see cref="AutomationResult.Status"/> instead.
    /// </summary>
    /// <param name="step">The step to execute.</param>
    /// <param name="cancellationToken">A token that can cancel the step.</param>
    Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken);
}
