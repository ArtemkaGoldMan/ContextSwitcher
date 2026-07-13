namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Requests a switch to a target context.
/// </summary>
/// <param name="TargetContextId">The context to switch to.</param>
/// <param name="Source">What triggered the request.</param>
/// <param name="DryRun">When <see langword="true"/>, builds the automation plan but applies no changes.</param>
/// <param name="Force">When <see langword="true"/>, re-runs the switch even if the target is already active.</param>
/// <param name="CorrelationId">An optional correlation id; a new one is generated when omitted.</param>
public sealed record ContextSwitchRequest(
    string TargetContextId,
    ContextSwitchSource Source,
    bool DryRun = false,
    bool Force = false,
    string? CorrelationId = null);
