using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeAutomationStepExecutor : IAutomationStepExecutor
{
    private readonly Dictionary<string, AutomationResult> resultsByStepId = [];

    public List<AutomationStep> ExecutedSteps { get; } = [];

    /// <summary>
    /// When set, <see cref="ExecuteAsync"/> completes this once it starts, then waits for
    /// <see cref="Gate"/> before returning - lets tests deterministically observe "a step is
    /// currently executing" without a fixed delay.
    /// </summary>
    public TaskCompletionSource? Entered { get; set; }

    public TaskCompletionSource? Gate { get; set; }

    public void SetResult(string stepId, AutomationResult result)
    {
        this.resultsByStepId[stepId] = result;
    }

    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        this.ExecutedSteps.Add(step);
        this.Entered?.TrySetResult();

        if (this.Gate is not null)
        {
            await this.Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (this.resultsByStepId.TryGetValue(step.Id, out AutomationResult? configured))
        {
            return configured;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new AutomationResult(step.Id, step.Type, AutomationResultStatus.Succeeded, "ok", 0, null, null, now, now);
    }
}
