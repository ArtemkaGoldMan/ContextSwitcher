namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// The outcome of a context switch attempt.
/// </summary>
public enum ContextSwitchStatus
{
    Succeeded,
    SucceededWithWarnings,
    Failed,
    Cancelled,
    NoOp
}
