using System.Diagnostics;
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

    /// <summary>
    /// A caller-side timeout (what a step timeout in <c>ContextSwitchService</c> looks like from
    /// inside the runner) used to propagate without killing the child, orphaning it to launchd.
    /// The marker comment makes this test's own <c>osascript</c> findable in the process table.
    /// </summary>
    [Fact]
    public async Task RunAsyncKillsProcessTreeWhenCallerCancels()
    {
        string marker = $"cs-orphan-probe-{Guid.NewGuid():N}";
        ProcessRunner runner = new();

        // A generous own-timeout, so only the caller's token can end the wait.
        ProcessStartOptions options = new("osascript", ["-e", $"delay 30 -- {marker}"], TimeSpan.FromMinutes(2));

        using CancellationTokenSource cts = new();
        Task<ProcessResult> run = runner.RunAsync(options, cts.Token);

        Assert.True(await WaitForProcessAsync(marker, shouldExist: true), "The probe process never started.");

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.True(await WaitForProcessAsync(marker, shouldExist: false), "The cancelled child was left running.");
    }

    [Fact]
    public async Task RunAsyncThrowsForExecutableOutsideAllowlist()
    {
        ProcessRunner runner = new();
        ProcessStartOptions options = new("echo", ["hello"], TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(options, CancellationToken.None));
    }

    /// <summary>
    /// Polls the process table for up to five seconds, returning whether a command line containing
    /// <paramref name="marker"/> reached the requested state.
    /// </summary>
    private static async Task<bool> WaitForProcessAsync(string marker, bool shouldExist)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (IsProcessRunning(marker) == shouldExist)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static bool IsProcessRunning(string marker)
    {
        using Process ps = new();
        ps.StartInfo.FileName = "/bin/ps";
        ps.StartInfo.ArgumentList.Add("-eo");
        ps.StartInfo.ArgumentList.Add("command");
        ps.StartInfo.UseShellExecute = false;
        ps.StartInfo.RedirectStandardOutput = true;

        ps.Start();
        string output = ps.StandardOutput.ReadToEnd();
        ps.WaitForExit();

        return output.Contains(marker, StringComparison.Ordinal);
    }
}
