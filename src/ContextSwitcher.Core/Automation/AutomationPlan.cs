namespace ContextSwitcher.Core.Automation;

/// <summary>
/// The ordered set of automation steps needed to switch from one context to another.
/// </summary>
/// <param name="TargetContextId">The context being switched to.</param>
/// <param name="PreviousContextId">The context being switched from, if any.</param>
/// <param name="Steps">The steps to run, in execution order.</param>
public sealed record AutomationPlan(
    string TargetContextId,
    string? PreviousContextId,
    IReadOnlyList<AutomationStep> Steps);
