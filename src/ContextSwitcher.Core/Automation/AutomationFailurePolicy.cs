namespace ContextSwitcher.Core.Automation;

/// <summary>
/// How a failed <see cref="AutomationStep"/> should affect the overall switch outcome.
/// </summary>
public enum AutomationFailurePolicy
{
    /// <summary>The whole switch is marked <c>Failed</c>.</summary>
    FailSwitch,

    /// <summary>The switch continues and is marked <c>SucceededWithWarnings</c>.</summary>
    WarnOnly
}
