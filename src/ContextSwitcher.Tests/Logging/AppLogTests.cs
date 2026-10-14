using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Logging;

public sealed class AppLogTests
{
    /// <summary>Before the app has set the log up - and in a test that meets a failure on purpose - nothing is written and nothing breaks.</summary>
    [Fact]
    public void WithNoLoggerItDoesNothing()
    {
        AppLog.Warn("Test", "Nothing", "no one is listening", new InvalidOperationException("x"));
        AppLog.Critical("Test", "Nothing", "no one is listening");
    }

    [Fact]
    public void AnExceptionIsWrittenWithItsTypeMessageAndStack()
    {
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);

        AppLog.Warn("Browser", "OpenedTabsNotRemembered", "Could not remember.", Thrown("disk is full"));

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Browser", entry.Category);
        Assert.Equal("OpenedTabsNotRemembered", entry.EventId);
        Assert.Equal("System.IO.IOException", entry.Data!["exception"]);
        Assert.Equal("disk is full", entry.Data["exceptionMessage"]);
        Assert.Contains(nameof(Thrown), entry.Data["stack"], StringComparison.Ordinal);
    }

    [Fact]
    public void TheStackIsCutToItsTopFrames()
    {
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);

        AppLog.Error("Test", "Deep", "deep", Deep(30));

        Assert.True(logger.Entries[0].Data!["stack"].Split('\n').Length <= 8);
    }

    [Fact]
    public void AnInnerExceptionIsNamed()
    {
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);

        AppLog.Warn("Test", "Wrapped", "wrapped", new InvalidOperationException("outer", new IOException("inner cause")));

        Assert.Equal("System.IO.IOException: inner cause", logger.Entries[0].Data!["inner"]);
    }

    /// <summary>A macOS call this Mac lacks is said once a run for each place: it would otherwise be the same line every time it is tried.</summary>
    [Fact]
    public void AMissingMacCallIsSaidOncePerPlace()
    {
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);
        string place = "a call only this test makes " + Guid.NewGuid();

        AppLog.MissingApi(place, new EntryPointNotFoundException("no such symbol"));
        AppLog.MissingApi(place, new EntryPointNotFoundException("no such symbol"));
        AppLog.MissingApi(place + " too", new EntryPointNotFoundException("no such symbol"));

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal("MissingMacApi", entry.EventId));
    }

    /// <summary>Logging a failure must not become another one.</summary>
    [Fact]
    public void ALineThatCannotBeWrittenNeverReachesTheCaller()
    {
        using IDisposable scope = AppLog.UseForThisFlow(new ThrowingLogger());

        AppLog.Warn("Test", "Unwritable", "the disk is full");
        AppLog.Critical("Test", "Unwritable", "the disk is full");
    }

    private static IOException Thrown(string message)
    {
        try
        {
            throw new IOException(message);
        }
        catch (IOException ex)
        {
            return ex;
        }
    }

    private static Exception Deep(int levels)
    {
        if (levels > 0)
        {
            return Deep(levels - 1);
        }

        try
        {
            throw new InvalidOperationException("bottom");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default) => throw new IOException("no space left on device");
    }
}
