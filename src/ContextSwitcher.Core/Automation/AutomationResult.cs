namespace ContextSwitcher.Core.Automation;

/// <summary>
/// The recorded outcome of executing (or skipping) a single <see cref="AutomationStep"/>.
/// </summary>
public sealed record AutomationResult(
    string StepId,
    AutomationStepType Type,
    AutomationResultStatus Status,
    string Message,
    int? ExitCode,
    string? StandardOutput,
    string? StandardError,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
