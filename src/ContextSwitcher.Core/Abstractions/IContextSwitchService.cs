using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Orchestrates switching the active context, in the order of docs/architecture.md, "Switching".
/// </summary>
public interface IContextSwitchService
{
    /// <summary>
    /// Runs (or previews, when <see cref="ContextSwitchRequest.DryRun"/> is set) a context switch.
    /// </summary>
    /// <param name="request">The switch to perform.</param>
    /// <param name="cancellationToken">A token that can cancel the switch.</param>
    Task<ContextSwitchResult> SwitchAsync(ContextSwitchRequest request, CancellationToken cancellationToken);
}
