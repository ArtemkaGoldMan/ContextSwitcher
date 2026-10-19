using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.MacOS.AppleScript;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Asks apps to quit with AppleScript, as their own Quit does, and asks AppleScript whether they are
/// running - by the same name, so the look finds the app the quit was sent to.
/// </summary>
public sealed class MacAppQuitter : IAppQuitter
{
    private readonly IScriptRunner scriptRunner;

    public MacAppQuitter(IScriptRunner scriptRunner)
    {
        ArgumentNullException.ThrowIfNull(scriptRunner);

        this.scriptRunner = scriptRunner;
    }

    /// <inheritdoc />
    public async Task<string> QuitAsync(string app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.scriptRunner.RunAsync(AppleScriptBuilder.QuitApplicationIfRunning(app), timeout, cancellationToken)
            .ConfigureAwait(false);
        return result.StandardError;
    }

    /// <inheritdoc />
    public async Task<bool?> IsRunningAsync(string app, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.scriptRunner.RunAsync(AppleScriptBuilder.IsApplicationRunning(app), timeout, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return null;
        }

        string answer = result.StandardOutput.Trim();
        return answer.Equals("false", StringComparison.OrdinalIgnoreCase) ? false
            : answer.Equals("true", StringComparison.OrdinalIgnoreCase) ? true
            : null;
    }
}
