using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Runs AppleScript source. Every AppleScript execution must go through this abstraction.
/// </summary>
public interface IScriptRunner
{
    /// <summary>
    /// Compiles and runs the given AppleScript source through <c>osascript</c>.
    /// </summary>
    /// <param name="script">The already-escaped AppleScript source, typically built by <c>AppleScriptBuilder</c>.</param>
    /// <param name="timeout">The maximum time allowed for the script to complete.</param>
    /// <param name="cancellationToken">A token that can cancel the run.</param>
    Task<ProcessResult> RunAsync(
        string script,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
