namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Requests a switch to a target context - or, with no target, turning the active one off.
/// </summary>
/// <param name="TargetContextId">The context to switch to; empty to turn the active one off.</param>
/// <param name="Source">What triggered the request.</param>
/// <param name="DryRun">When <see langword="true"/>, builds the automation plan but applies no changes.</param>
/// <param name="Force">When <see langword="true"/>, re-runs the switch even if the target is already active.</param>
/// <param name="CorrelationId">An optional correlation id; a new one is generated when omitted.</param>
public sealed record ContextSwitchRequest(
    string TargetContextId,
    ContextSwitchSource Source,
    bool DryRun = false,
    bool Force = false,
    string? CorrelationId = null)
{
    /// <summary>
    /// Leaves the active context without entering another: its half of a switch runs - containers
    /// stopped, tabs and apps closed, its Focus turned off - and then no context is active.
    /// </summary>
    public static ContextSwitchRequest TurnOff(ContextSwitchSource source, bool dryRun = false) => new(string.Empty, source, dryRun);

    /// <summary>
    /// What clicking a profile in the menu bar menu or the dashboard asks for: a switch to it - or,
    /// when it is the active one, turning it off. No separate "off" control is needed: the profile
    /// that is on is the switch that turns it off.
    /// </summary>
    public static ContextSwitchRequest ForClickOn(string contextId, string? activeContextId, ContextSwitchSource source) =>
        contextId == activeContextId ? TurnOff(source) : new(contextId, source);

    /// <summary>Whether this turns the active context off rather than switching to one.</summary>
    public bool IsTurnOff => this.TargetContextId.Length == 0;
}
