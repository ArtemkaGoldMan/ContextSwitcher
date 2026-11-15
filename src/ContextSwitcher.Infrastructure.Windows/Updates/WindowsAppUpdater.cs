using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Infrastructure.Windows.Updates;

/// <summary>
/// Replaces the installed app with a newer release: finds that release's package, downloads it - the
/// library checks it against the checksum the release lists - and has it swapped in once this copy
/// has quit. A copy that is not an installed one, such as one run from source, has nothing to replace.
/// </summary>
public sealed class WindowsAppUpdater : IAppUpdater
{
    private readonly IPackageUpdates packages;
    private readonly ReleaseAssets assets;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsAppUpdater"/> class.
    /// </summary>
    /// <param name="packages">Finds, downloads and applies a release's package.</param>
    /// <param name="assets">Names the file the release lists its packages in.</param>
    public WindowsAppUpdater(IPackageUpdates packages, ReleaseAssets assets)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(assets);

        this.packages = packages;
        this.assets = assets;
    }

    /// <inheritdoc />
    public Task<UserMessage?> GetInstallBlockerAsync(CancellationToken cancellationToken) =>
        Task.FromResult(this.packages.IsInstalled ? null : UpdateMessages.RunsFromSource.With());

    /// <inheritdoc />
    public async Task<StagedUpdate> StageAsync(ReleaseInfo release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);

        if (release.ArchiveUrl is not { } listUrl)
        {
            throw new UpdateException(UpdateMessages.NoArchive.With(ReleaseVersion.Format(release.Version), this.assets.Archive));
        }

        // The library reports a failed download or checksum with exceptions of its own, so any failure but ours is this.
        // The release's own folder: the list and the package are in it, side by side.
        string folder = listUrl[..(listUrl.LastIndexOf('/') + 1)];
        try
        {
            PackageUpdate package = await this.packages.FindAsync(folder, cancellationToken).ConfigureAwait(false)
                ?? throw new UpdateException(UpdateMessages.NoArchive.With(ReleaseVersion.Format(release.Version), this.assets.Archive));
            if (package.Version != release.Version)
            {
                throw new UpdateException(UpdateMessages.WrongVersion.With(ReleaseVersion.Format(package.Version), ReleaseVersion.Format(release.Version)));
            }

            await this.packages.DownloadAsync(package, cancellationToken).ConfigureAwait(false);
            return new StagedUpdate(package.Version, package.FileName, string.Empty) { Handle = package };
        }
        catch (Exception ex) when (ex is not (UpdateException or OperationCanceledException))
        {
            throw new UpdateException(UpdateMessages.DownloadFailed.With(), ex);
        }
    }

    /// <inheritdoc />
    public void InstallOnExit(StagedUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Handle is not PackageUpdate package)
        {
            throw new UpdateException(UpdateMessages.CouldNotStart.With());
        }

        try
        {
            this.packages.InstallOnExit(package);
        }
        catch (Exception ex)
        {
            throw new UpdateException(UpdateMessages.CouldNotStart.With(), ex);
        }
    }
}
