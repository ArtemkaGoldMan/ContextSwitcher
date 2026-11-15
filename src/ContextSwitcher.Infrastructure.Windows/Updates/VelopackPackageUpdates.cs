using Velopack;
using Velopack.Sources;

namespace ContextSwitcher.Infrastructure.Windows.Updates;

/// <summary>
/// <see cref="IPackageUpdates"/> through Velopack, the library that made the installer. Reads the
/// release's <c>releases.win.json</c> from the release's own download folder rather than from "the
/// latest release", which on GitHub may be the Mac's.
/// </summary>
public sealed class VelopackPackageUpdates : IPackageUpdates
{
    /// <inheritdoc />
    public bool IsInstalled
    {
        get
        {
            try
            {
                return new UpdateManager(new SimpleWebSource("https://localhost/")).IsInstalled;
            }
            catch (InvalidOperationException)
            {
                // Not logged: the library has no record of an installation to read - the app's start-up
                // sets it - and a copy it knows nothing about is not an installed one.
                return false;
            }
        }
    }

    /// <inheritdoc />
    public async Task<PackageUpdate?> FindAsync(string releaseFolderUrl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseFolderUrl);

        UpdateManager manager = new(new SimpleWebSource(releaseFolderUrl));
        UpdateInfo? info = await manager.CheckForUpdatesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            return null;
        }

        VelopackAsset target = info.TargetFullRelease;
        return new PackageUpdate(
            new Version(target.Version.Major, target.Version.Minor, target.Version.Patch),
            target.FileName,
            new Found(manager, info));
    }

    /// <inheritdoc />
    public async Task DownloadAsync(PackageUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        Found found = (Found)update.Handle;
        await found.Manager.DownloadUpdatesAsync(found.Info, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void InstallOnExit(PackageUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        Found found = (Found)update.Handle;
        found.Manager.WaitExitThenApplyUpdates(found.Info.TargetFullRelease, silent: false, restart: true);
    }

    private sealed record Found(UpdateManager Manager, UpdateInfo Info);
}
