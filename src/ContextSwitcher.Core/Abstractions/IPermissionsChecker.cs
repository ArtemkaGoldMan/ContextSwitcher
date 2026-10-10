namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Reports the macOS permission grants that context switching automation depends on, so the UI
/// can surface actionable status instead of users discovering missing grants via failed steps.
/// </summary>
public interface IPermissionsChecker
{
    /// <summary>
    /// Probes whether macOS Automation access to System Events is granted, required for the
    /// AppleScript-driven app and media steps (agent.md section 9). This runs a
    /// harmless read-only script and inspects the result, since macOS exposes no direct query API.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the probe.</param>
    Task<bool> IsAutomationPermissionGrantedAsync(CancellationToken cancellationToken);
}
