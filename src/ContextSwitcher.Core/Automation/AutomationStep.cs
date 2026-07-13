namespace ContextSwitcher.Core.Automation;

/// <summary>
/// A single, self-contained automation action to run as part of a context switch.
/// </summary>
/// <param name="Id">A stable identifier, conventionally <c>{type}.{contextId}</c>.</param>
/// <param name="Type">The kind of automation action.</param>
/// <param name="DisplayName">A human-readable name shown in the dashboard and logs.</param>
/// <param name="IsCritical">When <see langword="true"/>, this step failing fails the whole switch.</param>
/// <param name="Timeout">The maximum time allowed for the step to complete.</param>
/// <param name="Arguments">Everything the executor needs to perform the step; no external lookups required.</param>
public sealed record AutomationStep(
    string Id,
    AutomationStepType Type,
    string DisplayName,
    bool IsCritical,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> Arguments);
