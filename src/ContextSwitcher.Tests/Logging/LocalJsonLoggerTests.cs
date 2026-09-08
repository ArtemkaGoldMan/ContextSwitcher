using System.Text.Json;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.Tests.Logging;

public sealed class LocalJsonLoggerTests
{
    [Fact]
    public async Task LogAsyncAppendsOneJsonObjectPerLine()
    {
        string directory = CreateTempDirectory();
        LocalJsonLogger logger = new(new ConfigPaths(directory));

        try
        {
            await logger.LogAsync(Entry("SwitchStarted"));
            await logger.LogAsync(Entry("SwitchCompleted"));

            string[] lines = await File.ReadAllLinesAsync(Path.Combine(directory, "app.log.jsonl"));

            Assert.Equal(2, lines.Length);
            Assert.Equal("SwitchStarted", ReadEventId(lines[0]));
            Assert.Equal("SwitchCompleted", ReadEventId(lines[1]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Each logger owns its write semaphore, so two of them race exactly the way two ContextSwitcher
    /// processes do. Before the cross-process lock this lost entries outright - the writes landed at
    /// the same offset and overwrote one another, producing a shorter file with no malformed line and
    /// no exception to give the loss away.
    /// </summary>
    [Fact]
    public async Task ConcurrentLoggersKeepEveryEntry()
    {
        const int PerLogger = 150;
        string directory = CreateTempDirectory();
        LocalJsonLogger first = new(new ConfigPaths(directory));
        LocalJsonLogger second = new(new ConfigPaths(directory));

        try
        {
            await Task.WhenAll(
                WriteManyAsync(first, "first", PerLogger),
                WriteManyAsync(second, "second", PerLogger));

            string[] lines = await File.ReadAllLinesAsync(Path.Combine(directory, "app.log.jsonl"));
            HashSet<string> written = [.. lines.Select(ReadEventId)];

            Assert.Equal(PerLogger * 2, lines.Length);
            Assert.Equal(PerLogger * 2, written.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task WriteManyAsync(LocalJsonLogger logger, string prefix, int count)
    {
        await Task.Yield();
        for (int i = 0; i < count; i++)
        {
            await logger.LogAsync(Entry($"{prefix}-{i}"));
        }
    }

    private static LogEntry Entry(string eventId) => new()
    {
        Timestamp = DateTimeOffset.UnixEpoch,
        Level = LogLevel.Information,
        Category = "ContextSwitch",
        EventId = eventId,
        Message = "test"
    };

    private static string ReadEventId(string line) =>
        JsonDocument.Parse(line).RootElement.GetProperty("eventId").GetString()!;

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cs-logger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
