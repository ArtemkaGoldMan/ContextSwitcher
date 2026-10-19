using System.Text;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>How a step's handler reports what came of it.</summary>
internal static class StepResults
{
    /// <summary>
    /// Trims a step's budget before handing it to what the step asks of the system, so the call
    /// always returns before <c>ContextSwitchService</c>'s step-level timeout fires. Without the
    /// margin a hung command consumed the entire step and cancellation unwound past the code that
    /// turns a failure into a specific, actionable message - a browser that never answered, or a
    /// Shortcut that does not exist, both surfaced as a bare "step timed out" naming nothing.
    /// </summary>
    public static TimeSpan WithReportingHeadroom(TimeSpan budget) => budget * 0.8;

    public static AutomationResult Succeeded(AutomationStep step, string message, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        return new AutomationResult(step.Id, step.Type, AutomationResultStatus.Succeeded, message, 0, null, null, startedAt, completedAt);
    }

    public static AutomationResult Degraded(
        AutomationStep step,
        UserMessage problem,
        string standardError,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt) =>
        Degraded(step, [problem], standardError, startedAt, completedAt);

    /// <summary>A warning, or a failure for a critical step, listing each problem - the English joined for the log.</summary>
    public static AutomationResult Degraded(
        AutomationStep step,
        IReadOnlyList<UserMessage> problems,
        string standardError,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        AutomationResultStatus status = step.IsCritical ? AutomationResultStatus.Failed : AutomationResultStatus.Warning;
        return new AutomationResult(step.Id, step.Type, status, string.Join(' ', problems.Select(p => p.English)), null, null, standardError, startedAt, completedAt)
        {
            Problems = problems
        };
    }

    /// <summary>A warning, or a failure for a critical step, keeping what the system said for the log.</summary>
    public static AutomationResult WithProblem(AutomationStep step, UserMessage problem, ProcessResult result, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        AutomationResultStatus status = step.IsCritical ? AutomationResultStatus.Failed : AutomationResultStatus.Warning;
        return new AutomationResult(
            step.Id, step.Type, status, problem.English,
            result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt)
        {
            Problems = [problem]
        };
    }

    /// <summary>Adds what the system said about <paramref name="app"/> to the log's lines, when it said anything.</summary>
    public static void AppendIfPresent(StringBuilder builder, string app, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{app}: {value.Trim()}");
        }
    }
}
