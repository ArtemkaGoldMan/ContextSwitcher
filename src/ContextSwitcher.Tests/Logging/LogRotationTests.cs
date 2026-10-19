using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.Tests.Logging;

public sealed class LogRotationTests
{
    /// <summary>
    /// A log that something repeats into without end is rolled at 1MB, keeping one previous
    /// generation, which bounds it at roughly 2MB.
    /// </summary>
    [Fact]
    public async Task AnOversizedLogIsRolledAsideAndANewOneStarted()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "app.log.jsonl");

        try
        {
            await File.WriteAllBytesAsync(path, new byte[LocalJsonLogger.MaxLogBytes + 1]);
            LocalJsonLogger logger = new(new ConfigPaths(directory), FileReplacer.Once);

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
            LocalJsonLogger logger = new(new ConfigPaths(directory), FileReplacer.Once);
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

    /// <summary>A log starts again from nothing after a week; what it held is kept as the previous one.</summary>
    [Fact]
    public async Task ALogAWeekOldStartsOverAndKeepsTheOldOneAsPrevious()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "app.log.jsonl");

        try
        {
            await File.WriteAllTextAsync(path, "{\"old\":true}\n");
            File.SetCreationTimeUtc(path, DateTime.UtcNow - LocalJsonLogger.MaxAge - TimeSpan.FromHours(1));
            LocalJsonLogger logger = new(new ConfigPaths(directory), FileReplacer.Once);

            await logger.LogAsync(Entry("New"));

            Assert.Contains("old", await File.ReadAllTextAsync(path + ".1"), StringComparison.Ordinal);
            string[] lines = await File.ReadAllLinesAsync(path);
            Assert.Single(lines);
            Assert.Contains("New", lines[0], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ALogUnderAWeekOldIsLeftAlone()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "app.log.jsonl");

        try
        {
            await File.WriteAllTextAsync(path, "{\"recent\":true}\n");
            File.SetCreationTimeUtc(path, DateTime.UtcNow - TimeSpan.FromDays(3));
            LocalJsonLogger logger = new(new ConfigPaths(directory), FileReplacer.Once);

            await logger.LogAsync(Entry("Second"));

            Assert.False(File.Exists(path + ".1"));
            Assert.Equal(2, (await File.ReadAllLinesAsync(path)).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Done at start, so a log nothing was written to for a month does not still hold the month before.</summary>
    [Fact]
    public async Task TidyingRollsAStaleLogAndLetsGoOfAStalePreviousOne()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "app.log.jsonl");

        try
        {
            await File.WriteAllTextAsync(path, "{\"old\":true}\n");
            File.SetCreationTimeUtc(path, DateTime.UtcNow - TimeSpan.FromDays(20));
            await File.WriteAllTextAsync(path + ".1", "{\"older\":true}\n");
            File.SetLastWriteTimeUtc(path + ".1", DateTime.UtcNow - TimeSpan.FromDays(20));
            LocalJsonLogger logger = new(new ConfigPaths(directory), FileReplacer.Once);

            await logger.TidyAsync();

            Assert.False(File.Exists(path));
            Assert.Contains("old", await File.ReadAllTextAsync(path + ".1"), StringComparison.Ordinal);
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
