using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Tests.TestDoubles;

public sealed class FakeInstalledAppsService : IInstalledAppsService
{
    public List<InstalledApp> Apps { get; } = [];

    /// <summary>How many times the installed apps were read.</summary>
    public int Reads { get; private set; }

    public Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken)
    {
        this.Reads++;
        return Task.FromResult<IReadOnlyList<InstalledApp>>(this.Apps.ToList());
    }
}
