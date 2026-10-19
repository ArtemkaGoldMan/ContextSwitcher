using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>Runs Shortcuts with <c>shortcuts run</c>.</summary>
public sealed class MacShortcutRunner : IShortcutRunner
{
    private readonly IProcessRunner processRunner;

    public MacShortcutRunner(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);

        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public async Task<ShortcutRun> RunAsync(string name, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.processRunner.RunAsync(new ProcessStartOptions("shortcuts", ["run", name], timeout), cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            return new ShortcutRun(ShortcutRunOutcome.Ran, string.Empty, result);
        }

        // What `shortcuts run` says, and exits 1 with, when no Shortcut has the name.
        return result.StandardError.Contains("find shortcut", StringComparison.OrdinalIgnoreCase)
            ? new ShortcutRun(ShortcutRunOutcome.Missing, string.Empty, result)
            : new ShortcutRun(ShortcutRunOutcome.Failed, FirstLine(result.StandardError), result);
    }

    /// <summary>The first line of what it said, without the "Error: " it starts with.</summary>
    private static string FirstLine(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.StartsWith("Error: ", StringComparison.Ordinal))
        {
            trimmed = trimmed["Error: ".Length..];
        }

        int newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].TrimEnd();
    }
}
