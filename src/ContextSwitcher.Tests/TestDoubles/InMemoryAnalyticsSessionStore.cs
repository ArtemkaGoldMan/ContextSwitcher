using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class InMemoryAnalyticsSessionStore : IAnalyticsSessionStore
{
    private readonly List<ContextSession> sessions = [];
    private ContextSession? activeMarker;

    public IReadOnlyList<ContextSession> Sessions => this.sessions;

    public Task AppendCompletedSessionAsync(ContextSession session, CancellationToken cancellationToken)
    {
        this.sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ContextSession>> ReadAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ContextSession>>(this.sessions.ToList());
    }

    public Task ReplaceAllAsync(IReadOnlyList<ContextSession> sessions, CancellationToken cancellationToken)
    {
        this.sessions.Clear();
        this.sessions.AddRange(sessions);
        return Task.CompletedTask;
    }

    public Task<ContextSession?> ReadActiveMarkerAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(this.activeMarker);
    }

    public Task WriteActiveMarkerAsync(ContextSession session, CancellationToken cancellationToken)
    {
        this.activeMarker = session;
        return Task.CompletedTask;
    }

    public Task ClearActiveMarkerAsync(CancellationToken cancellationToken)
    {
        this.activeMarker = null;
        return Task.CompletedTask;
    }
}
