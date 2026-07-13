using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// In-memory implementation of <see cref="IAnalyticsService"/>. Tracks only the current session
/// boundary; persistence to <c>analytics.jsonl</c> is added in Phase 8.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IClock clock;
    private readonly Lock gate = new();
    private ContextSession? currentSession;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsService"/> class.
    /// </summary>
    /// <param name="clock">Supplies session start/end timestamps.</param>
    public AnalyticsService(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        this.clock = clock;
    }

    /// <inheritdoc />
    public Task StartSessionAsync(string contextId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextId);

        lock (this.gate)
        {
            this.currentSession = new ContextSession
            {
                SessionId = Guid.CreateVersion7().ToString(),
                ContextId = contextId,
                StartedAt = this.clock.UtcNow
            };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ContextSession?> EndCurrentSessionAsync(SessionEndReason reason, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            if (this.currentSession is null)
            {
                return Task.FromResult<ContextSession?>(null);
            }

            DateTimeOffset endedAt = this.clock.UtcNow;
            ContextSession completed = this.currentSession with
            {
                EndedAt = endedAt,
                DurationSeconds = (long)(endedAt - this.currentSession.StartedAt).TotalSeconds,
                EndReason = reason
            };

            this.currentSession = null;
            return Task.FromResult<ContextSession?>(completed);
        }
    }
}
