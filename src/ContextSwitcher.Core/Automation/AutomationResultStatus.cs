namespace ContextSwitcher.Core.Automation;

/// <summary>
/// The outcome of executing a single <see cref="AutomationStep"/>.
/// </summary>
public enum AutomationResultStatus
{
    Succeeded,
    Skipped,
    Warning,
    Failed,
    TimedOut
}
