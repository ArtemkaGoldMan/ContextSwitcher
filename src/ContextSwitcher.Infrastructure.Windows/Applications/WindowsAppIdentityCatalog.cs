using ContextSwitcher.Core.Abstractions;
using ConfiguredApp = ContextSwitcher.Core.Configuration.AppIdentity;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// What Windows says of an app a profile names, for the <c>apps</c> map: the AppUserModelID the Start
/// menu opens it by and, for an app that isn't a Store app, the full path of its executable - what
/// <see cref="AppIdentities"/> would work out each time, kept so that it still holds when the Start menu
/// calls the app something else or lists it twice.
/// </summary>
public sealed class WindowsAppIdentityCatalog : IAppIdentityCatalog
{
    private readonly StartApps startApps;
    private readonly AppIdentities identities;

    public WindowsAppIdentityCatalog(StartApps startApps, AppIdentities identities)
    {
        ArgumentNullException.ThrowIfNull(startApps);
        ArgumentNullException.ThrowIfNull(identities);

        this.startApps = startApps;
        this.identities = identities;
    }

    /// <inheritdoc />
    public async Task<ConfiguredApp?> IdentifyAsync(string appName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        IReadOnlyList<StartApp> apps = await this.startApps.GetAsync(cancellationToken).ConfigureAwait(false);
        StartApp? found = apps.FirstOrDefault(candidate => string.Equals(candidate.Name, appName.Trim(), StringComparison.OrdinalIgnoreCase));
        return found is null
            ? null
            : new ConfiguredApp { WindowsAppId = found.AppId, WindowsExe = this.identities.IdentityOf(found)?.ExecutablePath };
    }
}