using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Security;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.ProcessExecution;
using ContextSwitcher.Tests.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.AppleScript;

public sealed class AppleScriptRunnerTests
{
    [MacOSFact]
    public async Task RunAsyncExecutesScriptThroughOsascript()
    {
        AppleScriptRunner runner = new(new ProcessRunner(CommandAllowlist.MacOS));

        ProcessResult result = await runner.RunAsync("return 2 + 2", TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("4", result.StandardOutput.Trim());
    }

    /// <summary>The one failure every scripted step would report in its own words, or not at all: macOS saying no.</summary>
    [Theory]
    [InlineData("execution error: Not authorized to send Apple events to Google Chrome. (-1743)", "Google Chrome")]
    [InlineData("execution error: Not authorized to send Apple events to System Events. (-1743)", "System Events")]
    [InlineData("Not authorized (-1743)", "an app")]
    public async Task AnAutomationRefusalIsLoggedWithWhomItWasAbout(string standardError, string target)
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, standardError, false));
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);

        await new AppleScriptRunner(processRunner).RunAsync("tell application \"x\" to activate", TimeSpan.FromSeconds(5), CancellationToken.None);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal("AutomationDenied", entry.EventId);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(target, entry.Data!["target"]);
    }

    [Fact]
    public async Task OtherFailuresAndSuccessAreNotLoggedHere()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "execution error: Can't get window 1. (-1728)", false));
        processRunner.Enqueue(new ProcessResult(0, "ok", string.Empty, false));
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);
        AppleScriptRunner runner = new(processRunner);

        await runner.RunAsync("one", TimeSpan.FromSeconds(5), CancellationToken.None);
        await runner.RunAsync("two", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Empty(logger.Entries);
    }
}
