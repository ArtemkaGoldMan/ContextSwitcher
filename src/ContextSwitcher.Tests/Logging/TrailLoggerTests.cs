using System.Text.Json;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.Tests.Logging;

public sealed class TrailLoggerTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cs-trail-" + Guid.NewGuid().ToString("N"));
    private readonly string logPath;

    public TrailLoggerTests()
    {
        Directory.CreateDirectory(this.directory);
        this.logPath = Path.Combine(this.directory, "app.log.jsonl");
    }

    public void Dispose() => Directory.Delete(this.directory, recursive: true);

    /// <summary>A run in which nothing goes wrong leaves nothing behind - not even the file.</summary>
    [Fact]
    public async Task ARunWhereNothingGoesWrongWritesNothing()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("SwitchStarted", LogLevel.Information, 0, "a"));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Information, 1, "a"));
        await logger.LogAsync(Entry("SwitchCompleted", LogLevel.Information, 2, "a"));

        Assert.False(File.Exists(this.logPath));
    }

    /// <summary>
    /// A problem is written with what led to it: the run's header first, then every entry of its own
    /// switch in order, then itself.
    /// </summary>
    [Fact]
    public async Task AProblemIsWrittenWithTheWholeOfItsSwitch()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("SwitchStarted", LogLevel.Information, 0, "a"));
        await logger.LogAsync(Entry("SwitchPlanned", LogLevel.Information, 1, "a"));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Information, 2, "a"));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 3, "a", "Could not close: Slack."));

        List<Written> lines = this.Read();
        Assert.Equal(["RunStarted", "SwitchStarted", "SwitchPlanned", "StepCompleted", "StepCompleted"], lines.Select(line => line.EventId));
        Assert.Equal(["true", "true", "true"], lines.Skip(1).Take(3).Select(line => line.Trail));
        Assert.Null(lines[^1].Trail);
        Assert.Equal("warning", lines[^1].Level);
    }

    [Fact]
    public async Task TheHeaderSaysWhichAppOnWhichMac()
    {
        await this.NewLogger().LogAsync(Entry("Boom", LogLevel.Error, 0, null));

        Written header = this.Read()[0];

        Assert.Equal("ContextSwitcher 0.6.0 on macOS 26.6 (arm64)", header.Message);
    }

    [Fact]
    public async Task TheHeaderIsWrittenOncePerRun()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("First", LogLevel.Error, 0, null));
        await logger.LogAsync(Entry("Second", LogLevel.Error, 1, null));

        Assert.Single(this.Read(), line => line.EventId == "RunStarted");
    }

    /// <summary>
    /// How a switch ended - its status, how long it took - comes after its problem. It is written too,
    /// or the log would stop at the problem and never say what became of the switch.
    /// </summary>
    [Fact]
    public async Task WhatHappensAfterAProblemInTheSameSwitchIsWritten()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("SwitchStarted", LogLevel.Information, 0, "a"));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 1, "a"));
        await logger.LogAsync(Entry("SwitchCompleted", LogLevel.Information, 2, "a"));
        await logger.LogAsync(Entry("SwitchStarted", LogLevel.Information, 3, "b"));
        await logger.LogAsync(Entry("SwitchCompleted", LogLevel.Information, 4, "b"));

        Assert.Equal(["RunStarted", "SwitchStarted", "StepCompleted", "SwitchCompleted"], this.Read().Select(line => line.EventId));
    }

    /// <summary>Another switch's steps are not part of this one's story, and a trail is told once.</summary>
    [Fact]
    public async Task OnlyTheProblemsOwnSwitchIsWrittenAndNothingTwice()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("Other", LogLevel.Information, 0, "b"));
        await logger.LogAsync(Entry("Mine", LogLevel.Information, 1, "a"));
        await logger.LogAsync(Entry("Problem1", LogLevel.Warning, 2, "a"));
        await logger.LogAsync(Entry("Problem2", LogLevel.Warning, 3, "a"));

        Assert.Equal(["RunStarted", "Mine", "Problem1", "Problem2"], this.Read().Select(line => line.EventId));
    }

    /// <summary>The start of the app and the like - nothing's own - come with a problem, a few at most.</summary>
    [Fact]
    public async Task EntriesBelongingToNoSwitchComeAlongUpToAPoint()
    {
        TrailLogger logger = this.NewLogger();

        for (int i = 0; i < TrailLogger.StrayEntries + 10; i++)
        {
            await logger.LogAsync(Entry($"Background{i}", LogLevel.Information, i, null));
        }

        await logger.LogAsync(Entry("Problem", LogLevel.Warning, 100, "a"));

        List<Written> lines = this.Read();
        Assert.Equal(1 + TrailLogger.StrayEntries + 1, lines.Count);
        Assert.Equal("Background10", lines[1].EventId);
    }

    /// <summary>A crash belongs to no switch, so it takes the last entries of whatever kind - a switch under way too.</summary>
    [Fact]
    public async Task AProblemOutsideAnySwitchTakesTheLastEntriesOfAnyKind()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("SwitchStarted", LogLevel.Information, 0, "a"));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Information, 1, "a"));
        await logger.LogAsync(Entry("Crash", LogLevel.Critical, 2, null));

        Assert.Equal(["RunStarted", "SwitchStarted", "StepCompleted", "Crash"], this.Read().Select(line => line.EventId));
    }

    [Fact]
    public async Task OnlyTheLastEntriesAreRemembered()
    {
        TrailLogger logger = this.NewLogger();

        for (int i = 0; i < TrailLogger.TrailCapacity + 50; i++)
        {
            await logger.LogAsync(Entry($"Step{i}", LogLevel.Information, i, "a"));
        }

        await logger.LogAsync(Entry("Problem", LogLevel.Warning, 1000, "a"));

        List<Written> lines = this.Read();
        Assert.Equal(1 + TrailLogger.TrailCapacity + 1, lines.Count);
        Assert.Equal("Step50", lines[1].EventId);
    }

    /// <summary>A problem that repeats on every switch is said once, then counted.</summary>
    [Fact]
    public async Task AProblemThatRepeatsIsWrittenOnceAnHourAndCounted()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 0, "a", "Did not quit: Slack."));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 600, "b", "Did not quit: Slack."));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 1200, "c", "Did not quit: Slack."));
        Assert.Single(this.Read(), line => line.EventId == "StepCompleted");

        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 3700, "d", "Did not quit: Slack."));

        List<Written> steps = this.Read().Where(line => line.EventId == "StepCompleted").ToList();
        Assert.Equal(2, steps.Count);
        Assert.Equal("2", steps[1].Repeated);
    }

    [Fact]
    public async Task ADifferentProblemIsNotACountedRepeat()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 0, "a", "Did not quit: Slack."));
        await logger.LogAsync(Entry("StepCompleted", LogLevel.Warning, 10, "b", "Did not quit: Zoom."));

        Assert.Equal(2, this.Read().Count(line => line.EventId == "StepCompleted"));
    }

    /// <summary>The rare notable event that is no problem - an update about to be installed - is still found later.</summary>
    [Fact]
    public async Task AnEntryMarkedToKeepIsWrittenThoughNothingWentWrong()
    {
        await this.NewLogger().LogAsync(Entry("UpdateInstalling", LogLevel.Information, 0, null) with { Keep = true });

        Assert.Equal(["RunStarted", "UpdateInstalling"], this.Read().Select(line => line.EventId));
    }

    /// <summary>"Detailed log" is a week of everything, as it happens - for looking into what is not a failure.</summary>
    [Fact]
    public async Task EverythingIsWrittenWhileTheLogIsDetailed()
    {
        bool detailed = true;
        TrailLogger logger = this.NewLogger(() => detailed);

        await logger.LogAsync(Entry("SwitchStarted", LogLevel.Information, 0, "a"));
        await logger.LogAsync(Entry("SwitchCompleted", LogLevel.Information, 1, "a"));
        detailed = false;
        await logger.LogAsync(Entry("Quiet", LogLevel.Information, 2, "b"));

        Assert.Equal(["RunStarted", "SwitchStarted", "SwitchCompleted"], this.Read().Select(line => line.EventId));
    }

    [Fact]
    public async Task EntriesFromTheChosenLevelAreWrittenAsTheyHappen()
    {
        TrailLogger logger = this.NewLogger(writeFrom: LogLevel.Information);

        await logger.LogAsync(Entry("SwitchStarted", LogLevel.Information, 0, "a"));
        await logger.LogAsync(Entry("Fine", LogLevel.Debug, 1, "a"));

        Assert.Equal(["RunStarted", "SwitchStarted"], this.Read().Select(line => line.EventId));
    }

    /// <summary>A log is something a person is asked to send; what is private in it is taken out first.</summary>
    [Fact]
    public async Task WhatIsPrivateIsTakenOutBeforeAnythingIsKept()
    {
        TrailLogger logger = this.NewLogger();

        await logger.LogAsync(Entry("Step", LogLevel.Information, 0, "a", "Opened https://linear.app/team/secret?token=abc for me@example.com"));
        await logger.LogAsync(Entry("Problem", LogLevel.Warning, 1, "a", "Could not read /Users/artem/Documents/x.json") with
        {
            Data = new Dictionary<string, string> { ["stderr"] = "profile ann@work.org at https://github.com/pulls" }
        });

        string text = await File.ReadAllTextAsync(this.logPath);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("artem", text, StringComparison.Ordinal);
        Assert.DoesNotContain("work.org", text, StringComparison.Ordinal);
        Assert.Contains("https://linear.app/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("token", text, StringComparison.Ordinal);
        Assert.Contains("~/Documents/x.json", text, StringComparison.Ordinal);
    }

    private TrailLogger NewLogger(Func<bool>? detailed = null, LogLevel? writeFrom = null) => new(
        new LocalJsonLogger(new ConfigPaths(this.directory)),
        () => new RunInfo("ContextSwitcher", "0.6.0", "26.6", "arm64", "release", 1),
        detailed,
        writeFrom);

    private static LogEntry Entry(string eventId, LogLevel level, int secondsIn, string? correlation, string message = "message") => new()
    {
        Timestamp = Start.AddSeconds(secondsIn),
        Level = level,
        Category = "Test",
        EventId = eventId,
        Message = message,
        CorrelationId = correlation
    };

    private List<Written> Read() => File.ReadAllLines(this.logPath)
        .Select(line =>
        {
            using JsonDocument json = JsonDocument.Parse(line);
            JsonElement root = json.RootElement;
            JsonElement data = root.TryGetProperty("data", out JsonElement found) ? found : default;
            return new Written(
                root.GetProperty("eventId").GetString()!,
                root.GetProperty("level").GetString()!,
                root.GetProperty("message").GetString()!,
                data.ValueKind == JsonValueKind.Object && data.TryGetProperty("trail", out JsonElement trail) ? trail.GetString() : null,
                data.ValueKind == JsonValueKind.Object && data.TryGetProperty("repeated", out JsonElement repeated) ? repeated.GetString() : null);
        })
        .ToList();

    private sealed record Written(string EventId, string Level, string Message, string? Trail, string? Repeated);
}
