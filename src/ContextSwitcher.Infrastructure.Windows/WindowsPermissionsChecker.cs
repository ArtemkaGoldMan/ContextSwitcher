using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// Windows doesn't ask the person before one app drives another, as macOS's Automation permission
/// does, so there is nothing to grant. Settings shows no permission there.
/// </summary>
public sealed class WindowsPermissionsChecker : IPermissionsChecker
{
    /// <inheritdoc />
    public Task<bool> IsAutomationPermissionGrantedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
