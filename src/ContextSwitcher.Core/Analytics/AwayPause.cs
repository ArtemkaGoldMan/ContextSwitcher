using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Core.Analytics;

/// <summary>Why the person can't be using the Mac.</summary>
public enum AwayReason
{
    /// <summary>The Mac is asleep.</summary>
    Sleep,

    /// <summary>The screen is locked.</summary>
    ScreenLocked,

    /// <summary>Another user's session is in front - fast user switching.</summary>
    OtherUser
}

/// <summary>
/// Stops counting time while nobody can be using the Mac. A session ran from one switch to the next
/// whatever happened in between, so a profile left on overnight counted the whole night, and a lunch
/// with the screen locked counted as work.
///
/// The first reason to be away ends the running session; the last one to clear starts a new one for
/// the same profile, if it is still the one on. Reasons overlap - a Mac that sleeps also locks, and
/// wakes to a locked screen - so counting resumes only once the Mac is awake <em>and</em> unlocked,
/// whatever order macOS reports them in.
/// </summary>
public sealed class AwayPause
{
    private readonly IAnalyticsService analytics;
    private readonly Func<string> activeContextId;
    private readonly HashSet<AwayReason> away = [];
    private string? paused;
    private Task pending = Task.CompletedTask;

    /// <param name="activeContextId">The profile that is on now - empty when none is.</param>
    public AwayPause(IAnalyticsService analytics, Func<string> activeContextId)
    {
        ArgumentNullException.ThrowIfNull(analytics);
        ArgumentNullException.ThrowIfNull(activeContextId);

        this.analytics = analytics;
        this.activeContextId = activeContextId;
    }

    /// <summary>Whether time is paused for any reason now.</summary>
    public bool IsAway => this.away.Count > 0;

    /// <summary>The person went away for <paramref name="reason"/>.</summary>
    public Task OnAwayAsync(AwayReason reason, CancellationToken cancellationToken)
    {
        bool first = this.away.Count == 0;
        if (!this.away.Add(reason) || !first)
        {
            return this.pending;
        }

        this.pending = this.PauseAsync(reason, cancellationToken);
        return this.pending;
    }

    /// <summary><paramref name="reason"/> no longer holds.</summary>
    public async Task OnBackAsync(AwayReason reason, CancellationToken cancellationToken)
    {
        // Writes started on the way out can finish only after coming back, the Mac having slept
        // through them; the new session must not be started under them, or ending the old one
        // would clear it.
        await this.pending.ConfigureAwait(false);

        if (!this.away.Remove(reason) || this.away.Count > 0)
        {
            return;
        }

        string? resume = this.paused;
        this.paused = null;
        if (resume is not null && resume == this.activeContextId())
        {
            await this.analytics.StartSessionAsync(resume, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PauseAsync(AwayReason reason, CancellationToken cancellationToken)
    {
        SessionEndReason endReason = reason == AwayReason.Sleep ? SessionEndReason.Sleep : SessionEndReason.Away;
        ContextSession? ended = await this.analytics.EndCurrentSessionAsync(endReason, cancellationToken).ConfigureAwait(false);

        // Nothing running - none was on - keeps whatever was there to resume.
        this.paused = ended?.ContextId ?? this.paused;
    }
}
