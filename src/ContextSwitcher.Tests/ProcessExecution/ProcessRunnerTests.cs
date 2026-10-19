using System.Diagnostics;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Security;
using ContextSwitcher.Infrastructure.ProcessExecution;

namespace ContextSwitcher.Tests.ProcessExecution;

/// <summary>
/// Exercises the real <see cref="ProcessRunner"/> against a shell every system has - <c>/bin/sh</c>,
/// or PowerShell on Windows - with trivial, non-destructive scripts. The app's allowlist has neither,
/// so these runners allow that one shell.
/// </summary>
public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsyncCapturesStandardOutputAndZeroExitCode()
    {
        ProcessStartOptions options = Shell("echo $((1 + 1))", "1 + 1", TimeSpan.FromSeconds(30));

        ProcessResult result = await ShellRunner().RunAsync(options, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("2", result.StandardOutput.Trim());
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsyncReturnsNonZeroExitCodeWithoutThrowingOnScriptError()
    {
        ProcessStartOptions options = Shell("echo boom >&2; exit 3", "[Console]::Error.WriteLine('boom'); exit 3", TimeSpan.FromSeconds(30));

        ProcessResult result = await ShellRunner().RunAsync(options, CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("boom", result.StandardError);
    }

    [Fact]
    public async Task RunAsyncReportsTimedOutWhenProcessExceedsTimeout()
    {
        ProcessStartOptions options = Shell("sleep 5", "Start-Sleep -Seconds 5", TimeSpan.FromMilliseconds(300));

        ProcessResult result = await ShellRunner().RunAsync(options, CancellationToken.None);

        Assert.True(result.TimedOut);
    }

    /// <summary>
    /// A caller-side timeout (what a step timeout in <c>ContextSwitchService</c> looks like from
    /// inside the runner) used to propagate without killing the child, orphaning it to launchd.
    /// The child writes its process id to a file, so this test can find it.
    /// </summary>
    [Fact]
    public async Task RunAsyncKillsProcessTreeWhenCallerCancels()
    {
        string pidFile = Path.Combine(Path.GetTempPath(), $"cs-orphan-probe-{Guid.NewGuid():N}.pid");

        // A generous own-timeout, so only the caller's token can end the wait.
        ProcessStartOptions options = Shell(
            "echo $$ > \"$PROBE_PID_FILE\"; sleep 30",
            "Set-Content -LiteralPath $env:PROBE_PID_FILE -Value $PID; Start-Sleep -Seconds 30",
            TimeSpan.FromMinutes(2),
            new Dictionary<string, string> { ["PROBE_PID_FILE"] = pidFile });

        try
        {
            using CancellationTokenSource cts = new();
            Task<ProcessResult> run = ShellRunner().RunAsync(options, cts.Token);

            int? probe = await ReadProcessIdAsync(pidFile);
            Assert.True(probe is not null && IsRunning(probe.Value), "The probe process never started.");

            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

            Assert.True(await WaitUntilAsync(() => !IsRunning(probe.Value)), "The cancelled child was left running.");
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    [Fact]
    public async Task RunAsyncThrowsForExecutableOutsideAllowlist()
    {
        ProcessRunner runner = new(CommandAllowlist.MacOS);
        ProcessStartOptions options = new("echo", ["hello"], TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(options, CancellationToken.None));
    }

    /// <summary>The shell this system has, running the script written for it.</summary>
    private static ProcessStartOptions Shell(string sh, string powerShell, TimeSpan timeout, IReadOnlyDictionary<string, string>? environment = null) =>
        OperatingSystem.IsWindows()
            ? new("powershell", ["-NoProfile", "-NonInteractive", "-Command", powerShell], timeout, environment)
            : new("/bin/sh", ["-c", sh], timeout, environment);

    /// <summary>A shell every system has, which no platform's own allowlist has a reason to allow.</summary>
    private static ProcessRunner ShellRunner() => new(new CommandAllowlist(["/bin/sh", "powershell"]));

    /// <summary>
    /// The process id the probe wrote, once it has - waiting up to fifteen seconds, as PowerShell can
    /// take a while to start on a busy machine.
    /// </summary>
    private static async Task<int?> ReadProcessIdAsync(string pidFile)
    {
        for (int attempt = 0; attempt < 150; attempt++)
        {
            try
            {
                if (int.TryParse(await File.ReadAllTextAsync(pidFile), out int processId))
                {
                    return processId;
                }
            }
            catch (IOException)
            {
                // Not there yet, or still being written.
            }

            await Task.Delay(100);
        }

        return null;
    }

    /// <summary>Polls for up to five seconds, returning whether <paramref name="condition"/> came true in time.</summary>
    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // No process has that id any more.
            return false;
        }
        catch (InvalidOperationException)
        {
            // It exited while being looked at.
            return false;
        }
    }
}
