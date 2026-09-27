using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakePermissionsChecker : IPermissionsChecker
{
    public bool AutomationGranted { get; set; } = true;

    public Task<bool> IsAutomationPermissionGrantedAsync(CancellationToken cancellationToken) => Task.FromResult(this.AutomationGranted);
}
