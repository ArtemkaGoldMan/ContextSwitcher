using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Runs external processes. Every OS command execution must go through this abstraction.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process to completion and returns its structured result.
    /// </summary>
    /// <param name="options">The process to start.</param>
    /// <param name="cancellationToken">A token that can cancel the run.</param>
    Task<ProcessResult> RunAsync(
        ProcessStartOptions options,
        CancellationToken cancellationToken);
}
