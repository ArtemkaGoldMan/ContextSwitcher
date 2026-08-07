using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeInstalledAppsService : IInstalledAppsService
{
    public List<InstalledApp> Apps { get; } = [];

    public Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<InstalledApp>>(this.Apps.ToList());
    }
}
