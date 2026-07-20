using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using SharpHook.Providers;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Checks macOS Accessibility (via SharpHook's <see cref="UioHookProvider"/>) and Automation
/// (via a harmless System Events probe script) permission grants.
/// </summary>
public sealed class MacPermissionsChecker : IPermissionsChecker
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IScriptRunner scriptRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacPermissionsChecker"/> class.
    /// </summary>
    public MacPermissionsChecker(IScriptRunner scriptRunner)
    {
        ArgumentNullException.ThrowIfNull(scriptRunner);
        this.scriptRunner = scriptRunner;
    }

    /// <inheritdoc />
    public bool IsAccessibilityPermissionGranted()
    {
        return UioHookProvider.Instance.IsAxApiEnabled(promptUserIfDisabled: false);
    }

    /// <inheritdoc />
    public async Task<bool> IsAutomationPermissionGrantedAsync(CancellationToken cancellationToken)
    {
        // System Events is always running, so "count of processes" is a read-only call that only
        // succeeds if macOS has granted Automation access to it - the same access every
        // AppleScript-driven step (theme, wallpaper, app quit/launch, media) relies on.
        ProcessResult result = await this.scriptRunner
            .RunAsync("tell application \"System Events\" to return count of processes", ProbeTimeout, cancellationToken)
            .ConfigureAwait(false);

        return result.ExitCode == 0;
    }
}
