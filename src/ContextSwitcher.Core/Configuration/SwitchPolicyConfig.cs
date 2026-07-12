namespace ContextSwitcher.Core.Configuration;

/// <summary>
/// Defines how context switching should behave when automation steps fail.
/// </summary>
public sealed record SwitchPolicyConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether the switch should continue after non-critical failures.
    /// </summary>
    public bool ContinueOnNonCriticalFailure { get; init; } = true;

    /// <summary>
    /// Gets or sets the automation steps that must succeed for the switch to be considered successful.
    /// </summary>
    public IReadOnlyList<string> CriticalSteps { get; init; } = [];
}
