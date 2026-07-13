using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.ProcessExecution;

namespace ContextSwitcher.Tests.ProcessExecution;

/// <summary>
/// Exercises the real <see cref="ProcessRunner"/> against <c>osascript</c>, which is always
/// present on macOS and safe to invoke with trivial, non-destructive scripts.
/// </summary>
public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsyncCapturesStandardOutputAndZeroExitCode()
    {
        ProcessRunner runner = new();
        ProcessStartOptions options = new("osascript", ["-e", "return 1 + 1"], TimeSpan.FromSeconds(10));

        ProcessResult result = await runner.RunAsync(options, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("2", result.StandardOutput.Trim());
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsyncReturnsNonZeroExitCodeWithoutThrowingOnScriptError()
    {
        ProcessRunner runner = new();
        ProcessStartOptions options = new("osascript", ["-e", "error \"boom\""], TimeSpan.FromSeconds(10));

        ProcessResult result = await runner.RunAsync(options, CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("boom", result.StandardError);
    }

    [Fact]
    public async Task RunAsyncReportsTimedOutWhenProcessExceedsTimeout()
    {
        ProcessRunner runner = new();
        ProcessStartOptions options = new("osascript", ["-e", "delay 5"], TimeSpan.FromMilliseconds(300));

        ProcessResult result = await runner.RunAsync(options, CancellationToken.None);

        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task RunAsyncThrowsForExecutableOutsideAllowlist()
    {
        ProcessRunner runner = new();
        ProcessStartOptions options = new("echo", ["hello"], TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(options, CancellationToken.None));
    }
}
