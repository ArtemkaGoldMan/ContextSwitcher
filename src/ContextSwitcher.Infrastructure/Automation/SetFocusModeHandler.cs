using System.Globalization;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>Turns on the Focus mode of the context switched to, or turns off the one of the context left.</summary>
public sealed class SetFocusModeHandler : IStepHandler
{
    private readonly IShortcutRunner shortcuts;
    private readonly IClock clock;

    public SetFocusModeHandler(IShortcutRunner shortcuts, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(clock);

        this.shortcuts = shortcuts;
        this.clock = clock;
    }

    /// <inheritdoc />
    public AutomationStepType Type => AutomationStepType.SetFocusMode;

    /// <inheritdoc />
    public async Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        DateTimeOffset startedAt = this.clock.UtcNow;
        bool enabled = step.Arguments.GetValueOrDefault("enabled") == "True";
        string modeName = step.Arguments.GetValueOrDefault("modeName", string.Empty);
        string previousModeName = step.Arguments.GetValueOrDefault("previousModeName", string.Empty);

        // Turning a mode on has one Shortcut. Turning one off prefers that mode's own Off Shortcut
        // and falls back to the original single "Focus Off" - which people built by hand from the
        // docs - only when the mode's own does not exist.
        List<string> candidates = enabled && !string.IsNullOrWhiteSpace(modeName)
            ? [FocusModes.ShortcutName(modeName)]
            : string.IsNullOrWhiteSpace(previousModeName)
                ? [FocusModes.LegacyOffShortcutName]
                : [FocusModes.OffShortcutName(previousModeName), FocusModes.LegacyOffShortcutName];

        TimeSpan budget = StepResults.WithReportingHeadroom(step.Timeout) / candidates.Count;
        string shortcutName = candidates[0];
        ShortcutRun run = await this.shortcuts.RunAsync(shortcutName, budget, cancellationToken).ConfigureAwait(false);
        for (int next = 1; next < candidates.Count && run.Outcome == ShortcutRunOutcome.Missing; next++)
        {
            shortcutName = candidates[next];
            run = await this.shortcuts.RunAsync(shortcutName, budget, cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (run.Outcome == ShortcutRunOutcome.Ran)
        {
            return StepResults.Succeeded(step, $"Ran Shortcut '{shortcutName}'.", startedAt, completedAt);
        }

        // Say what actually went wrong. This used to tell the user to create a Shortcut that was
        // right there in their list, when it had run and failed - a Focus it names was never set up.
        UserMessage message = run.Outcome == ShortcutRunOutcome.Missing
            ? SwitchMessages.ShortcutMissing.With(candidates[0])
            : run.Reason is { Length: > 0 } reason
                ? SwitchMessages.ShortcutFailed.With(shortcutName, reason)
                : SwitchMessages.ShortcutFailedWithExitCode.With(shortcutName, run.Output.ExitCode.ToString(CultureInfo.InvariantCulture));

        return StepResults.WithProblem(step, message, run.Output, startedAt, completedAt);
    }
}
