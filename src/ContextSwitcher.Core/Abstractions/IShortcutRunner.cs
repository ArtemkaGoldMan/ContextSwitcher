using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>Runs a Shortcut by its name - how a Focus mode is turned on and off.</summary>
public interface IShortcutRunner
{
    /// <summary>Runs the Shortcut called <paramref name="name"/>.</summary>
    Task<ShortcutRun> RunAsync(string name, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>How running a Shortcut went.</summary>
/// <param name="Outcome">Whether it ran, wasn't there, or ran and failed.</param>
/// <param name="Reason">Why it failed, in the system's words; empty when it ran, wasn't there, or gave none.</param>
/// <param name="Output">What the system said in full, kept for the log.</param>
public sealed record ShortcutRun(ShortcutRunOutcome Outcome, string Reason, ProcessResult Output);

/// <summary>Whether a Shortcut ran.</summary>
public enum ShortcutRunOutcome
{
    Ran,

    /// <summary>No Shortcut has the name.</summary>
    Missing,

    /// <summary>The Shortcut is there, but running it failed - a Focus it names was never set up, say.</summary>
    Failed
}
