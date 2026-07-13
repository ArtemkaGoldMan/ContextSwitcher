using System.Diagnostics;
using System.Text;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Security;

namespace ContextSwitcher.Infrastructure.ProcessExecution;

/// <summary>
/// Runs external processes with argument arrays (never shell strings), captures output, and kills
/// the process tree on timeout. Never throws for a non-zero exit code; only for programmer errors
/// such as an executable outside <see cref="CommandAllowlist"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(ProcessStartOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!CommandAllowlist.IsAllowed(options.FileName))
        {
            throw new InvalidOperationException($"Executable '{options.FileName}' is not in the command allowlist.");
        }

        using Process process = new();
        process.StartInfo.FileName = options.FileName;
        foreach (string argument in options.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = options.CaptureOutput;
        process.StartInfo.RedirectStandardError = options.CaptureOutput;

        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            process.StartInfo.WorkingDirectory = options.WorkingDirectory;
        }

        if (options.Environment is not null)
        {
            foreach (KeyValuePair<string, string> variable in options.Environment)
            {
                process.StartInfo.Environment[variable.Key] = variable.Value;
            }
        }

        StringBuilder standardOutput = new();
        StringBuilder standardError = new();

        if (options.CaptureOutput)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    standardOutput.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    standardError.AppendLine(e.Data);
                }
            };
        }

        process.Start();

        if (options.CaptureOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        using CancellationTokenSource timeoutCts = new(options.Timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString(), TimedOut: false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            return new ProcessResult(-1, standardOutput.ToString(), standardError.ToString(), TimedOut: true);
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the timeout firing and the kill attempt.
        }
    }
}
