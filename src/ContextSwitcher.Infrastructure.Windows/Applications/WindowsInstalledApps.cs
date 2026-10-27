using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// The app picker's list on Windows: the Start menu's apps. Their icons come later in stage 1, so the
/// picker shows names alone until then.
/// </summary>
public sealed class WindowsInstalledApps : IInstalledAppsService
{
    private readonly StartApps startApps;

    public WindowsInstalledApps(StartApps startApps)
    {
        ArgumentNullException.ThrowIfNull(startApps);

        this.startApps = startApps;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StartApp> apps = await this.startApps.GetAsync(cancellationToken).ConfigureAwait(false);
        return apps.Select(app => new InstalledApp(app.Name, IconPath: null)).ToList();
    }
}
