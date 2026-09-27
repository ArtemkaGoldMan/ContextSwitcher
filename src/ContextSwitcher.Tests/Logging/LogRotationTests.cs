using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.Tests.Logging;

public sealed class LogRotationTests
{
    /// <summary>
    /// Nothing trimmed app.log.jsonl before, so it grew for the life of the install. It now rolls at
    /// 5MB keeping one previous generation, which bounds it at roughly 10MB.
    /// </summary>
    [Fact]
    public async Task AnOversizedLogIsRolledAsideAndANewOneStarted()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "app.log.jsonl");

        try
        {
            await File.WriteAllBytesAsync(path, new byte[(5 * 1024 * 1024) + 1]);
            LocalJsonLogger logger = new(new ConfigPaths(directory));

            await logger.LogAsync(Entry("AfterRoll"));

            Assert.True(File.Exists(path + ".1"), "the oversized log was not kept as a previous generation");
            string[] lines = await File.ReadAllLinesAsync(path);
            Assert.Single(lines);
            Assert.Contains("AfterRoll", lines[0], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ALogUnderTheLimitIsLeftAlone()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "app.log.jsonl");

        try
        {
            LocalJsonLogger logger = new(new ConfigPaths(directory));
            await logger.LogAsync(Entry("First"));
            await logger.LogAsync(Entry("Second"));

            Assert.False(File.Exists(path + ".1"));
            Assert.Equal(2, (await File.ReadAllLinesAsync(path)).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
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

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cs-rotate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
