using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// The outcome of a completed (or short-circuited) context switch attempt.
/// </summary>
public sealed record ContextSwitchResult(
    string TargetContextId,
    string? PreviousContextId,
    ContextSwitchStatus Status,
    IReadOnlyList<AutomationResult> StepResults,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string CorrelationId);
