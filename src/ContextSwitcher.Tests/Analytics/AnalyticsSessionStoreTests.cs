using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Infrastructure.Analytics;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Tests.Analytics;

public sealed class AnalyticsSessionStoreTests
{
    [Fact]
    public async Task ReadAllAsyncReturnsEmptyWhenLogDoesNotExist()
    {
        (AnalyticsSessionStore store, string tempDirectory) = Create();

        try
        {
            IReadOnlyList<ContextSession> sessions = await store.ReadAllAsync(CancellationToken.None);
            Assert.Empty(sessions);
        }
        finally
        {
            CleanUp(tempDirectory);
        }
    }

    [Fact]
    public async Task AppendThenReadAllRoundTripsSessions()
    {
        (AnalyticsSessionStore store, string tempDirectory) = Create();

        try
        {
            ContextSession first = SampleSession("s1", "work");
            ContextSession second = SampleSession("s2", "personal");

            await store.AppendCompletedSessionAsync(first, CancellationToken.None);
            await store.AppendCompletedSessionAsync(second, CancellationToken.None);

            IReadOnlyList<ContextSession> sessions = await store.ReadAllAsync(CancellationToken.None);

            Assert.Equal(2, sessions.Count);
            Assert.Equal("s1", sessions[0].SessionId);
            Assert.Equal("s2", sessions[1].SessionId);
        }
        finally
        {
            CleanUp(tempDirectory);
        }
    }

    [Fact]
    public async Task ReadAllAsyncSkipsMalformedLinesInsteadOfFailing()
    {
        (AnalyticsSessionStore store, string tempDirectory) = Create();

        try
        {
            ConfigPaths configPaths = new(tempDirectory);
            Directory.CreateDirectory(configPaths.BaseDirectory);
            await File.WriteAllLinesAsync(
                configPaths.AnalyticsLogPath,
                ["{ not valid json", System.Text.Json.JsonSerializer.Serialize(SampleSession("good", "work"), ContextSwitcher.Core.Serialization.ContextSwitcherJson.CompactOptions)]);

            IReadOnlyList<ContextSession> sessions = await store.ReadAllAsync(CancellationToken.None);

            ContextSession session = Assert.Single(sessions);
            Assert.Equal("good", session.SessionId);
        }
        finally
        {
            CleanUp(tempDirectory);
        }
    }

    [Fact]
    public async Task ReplaceAllAsyncOverwritesLogAtomicallyWithoutLeavingTempFiles()
    {
        (AnalyticsSessionStore store, string tempDirectory) = Create();

        try
        {
            await store.AppendCompletedSessionAsync(SampleSession("old", "work"), CancellationToken.None);
            await store.ReplaceAllAsync([SampleSession("new", "personal")], CancellationToken.None);

            IReadOnlyList<ContextSession> sessions = await store.ReadAllAsync(CancellationToken.None);
            ContextSession session = Assert.Single(sessions);
            Assert.Equal("new", session.SessionId);

            Assert.Empty(Directory.EnumerateFiles(tempDirectory, "*.tmp"));
        }
        finally
        {
            CleanUp(tempDirectory);
        }
    }

    [Fact]
    public async Task ActiveMarkerRoundTripsAndClears()
    {
        (AnalyticsSessionStore store, string tempDirectory) = Create();

        try
        {
            ContextSession session = SampleSession("marker", "work");
            await store.WriteActiveMarkerAsync(session, CancellationToken.None);

            ContextSession? read = await store.ReadActiveMarkerAsync(CancellationToken.None);
            Assert.NotNull(read);
            Assert.Equal("marker", read.SessionId);

            await store.ClearActiveMarkerAsync(CancellationToken.None);
            Assert.Null(await store.ReadActiveMarkerAsync(CancellationToken.None));
        }
        finally
        {
            CleanUp(tempDirectory);
        }
    }

    private static (AnalyticsSessionStore Store, string TempDirectory) Create()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
        ConfigPaths configPaths = new(tempDirectory);
        JsonFileStore jsonStore = new();
        return (new AnalyticsSessionStore(configPaths, jsonStore), tempDirectory);
    }

    private static void CleanUp(string tempDirectory)
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static ContextSession SampleSession(string sessionId, string contextId)
    {
        return new ContextSession
        {
            SessionId = sessionId,
            ContextId = contextId,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndedAt = DateTimeOffset.UtcNow,
            DurationSeconds = 3600,
            EndReason = SessionEndReason.Switch
        };
    }
}
