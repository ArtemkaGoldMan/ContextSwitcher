using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakePermissionsChecker : IPermissionsChecker
{
    public bool AccessibilityGranted { get; set; } = true;

    public bool AutomationGranted { get; set; } = true;

    public bool IsAccessibilityPermissionGranted() => this.AccessibilityGranted;

    public Task<bool> IsAutomationPermissionGrantedAsync(CancellationToken cancellationToken) => Task.FromResult(this.AutomationGranted);
}
