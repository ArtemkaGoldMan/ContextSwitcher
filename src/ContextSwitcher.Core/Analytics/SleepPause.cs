using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Stops counting time while the Mac sleeps. A session ran from one switch to the next however long
/// the Mac slept in between, so a profile left on overnight counted the whole night. Going to sleep
/// ends the running session; waking starts a new one for the same profile, if it is still the
/// active one.
/// </summary>
public sealed class SleepPause
{
    private readonly IAnalyticsService analytics;
    private readonly Func<string> activeContextId;
    private string? paused;
    private Task goingToSleep = Task.CompletedTask;

    /// <param name="activeContextId">The profile that is on now - empty when none is.</param>
    public SleepPause(IAnalyticsService analytics, Func<string> activeContextId)
    {
        ArgumentNullException.ThrowIfNull(analytics);
        ArgumentNullException.ThrowIfNull(activeContextId);

        this.analytics = analytics;
        this.activeContextId = activeContextId;
    }

    /// <summary>The Mac is about to sleep.</summary>
    public Task OnWillSleepAsync(CancellationToken cancellationToken)
    {
        this.goingToSleep = this.PauseAsync(cancellationToken);
        return this.goingToSleep;
    }

    /// <summary>The Mac has woken.</summary>
    public async Task OnDidWakeAsync(CancellationToken cancellationToken)
    {
        // Writes started on the way to sleep can finish only after waking; the new session must not
        // be started under them, or ending the old one would clear it.
        await this.goingToSleep.ConfigureAwait(false);

        string? resume = this.paused;
        this.paused = null;
        if (resume is not null && resume == this.activeContextId())
        {
            await this.analytics.StartSessionAsync(resume, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PauseAsync(CancellationToken cancellationToken)
    {
        ContextSession? ended = await this.analytics.EndCurrentSessionAsync(SessionEndReason.Sleep, cancellationToken).ConfigureAwait(false);

        // A second "going to sleep" with nothing left running keeps the profile to resume.
        this.paused = ended?.ContextId ?? this.paused;
    }
}
