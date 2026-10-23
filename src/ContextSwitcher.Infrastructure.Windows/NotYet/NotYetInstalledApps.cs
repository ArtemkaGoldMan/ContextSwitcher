using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// The app picker's list - the Start menu's apps - comes with the apps part of stage 1; until then it
/// is empty, and an app is added by typing its name.
/// </summary>
public sealed class NotYetInstalledApps : IInstalledAppsService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InstalledApp>>([]);
}
