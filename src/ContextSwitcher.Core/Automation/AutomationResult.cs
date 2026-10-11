using ContextSwitcher.Core.Messages;

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
    DateTimeOffset CompletedAt)
{
    /// <summary>
    /// What went wrong, one message each, in a form the app's window can translate; empty for a step
    /// that succeeded or was skipped. <see cref="Message"/> is the same in English, for the log.
    /// </summary>
    public IReadOnlyList<UserMessage> Problems { get; init; } = [];
}
