using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.Windows;
using ContextSwitcher.Infrastructure.Windows.Updates;

namespace ContextSwitcher.Tests.Windows.Updates;

/// <summary>What the updater decides about a release, with the installer's library replaced by a fake.</summary>
public sealed class WindowsAppUpdaterTests
{
    private const string ListUrl = "https://github.com/o/r/releases/download/v0.7.0/releases.win.json";
    private static readonly ReleaseInfo Release = new(new Version(0, 7, 0), "https://github.com/o/r/releases/tag/v0.7.0", ListUrl);

    [Fact]
    public async Task ACopyThatWasNotInstalledSaysItRunsFromSource()
    {
        WindowsAppUpdater updater = new(new FakePackages { IsInstalled = false }, WindowsPlatform.Assets);

        Assert.NotNull(await updater.GetInstallBlockerAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AnInstalledCopyHasNothingInTheWay()
    {
        WindowsAppUpdater updater = new(new FakePackages { IsInstalled = true }, WindowsPlatform.Assets);

        Assert.Null(await updater.GetInstallBlockerAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ThePackageIsLookedForInTheReleasesOwnFolderAndDownloaded()
    {
        FakePackages packages = new() { Found = new PackageUpdate(new Version(0, 7, 0), "ContextSwitcher-0.7.0-full.nupkg", new object()) };
        WindowsAppUpdater updater = new(packages, WindowsPlatform.Assets);

        StagedUpdate staged = await updater.StageAsync(Release, CancellationToken.None);

        Assert.Equal("https://github.com/o/r/releases/download/v0.7.0/", packages.FolderAsked);
        Assert.Same(packages.Found, packages.Downloaded);
        Assert.Equal(new Version(0, 7, 0), staged.Version);
    }

    [Fact]
    public async Task AReleaseWithNoListOfPackagesIsNotInstalled()
    {
        WindowsAppUpdater updater = new(new FakePackages(), WindowsPlatform.Assets);

        await Assert.ThrowsAsync<UpdateException>(() => updater.StageAsync(Release with { ArchiveUrl = null }, CancellationToken.None));
    }

    [Fact]
    public async Task AReleaseThatOffersNothingNewerIsNotInstalled()
    {
        FakePackages packages = new() { Found = null };
        WindowsAppUpdater updater = new(packages, WindowsPlatform.Assets);

        await Assert.ThrowsAsync<UpdateException>(() => updater.StageAsync(Release, CancellationToken.None));
        Assert.Null(packages.Downloaded);
    }

    [Fact]
    public async Task APackageOfAnotherVersionThanTheReleaseIsNotDownloaded()
    {
        FakePackages packages = new() { Found = new PackageUpdate(new Version(0, 6, 9), "x.nupkg", new object()) };
        WindowsAppUpdater updater = new(packages, WindowsPlatform.Assets);

        await Assert.ThrowsAsync<UpdateException>(() => updater.StageAsync(Release, CancellationToken.None));
        Assert.Null(packages.Downloaded);
    }

    [Fact]
    public async Task AFailedDownloadSaysSoInPlainWords()
    {
        FakePackages packages = new()
        {
            Found = new PackageUpdate(new Version(0, 7, 0), "x.nupkg", new object()),
            DownloadFails = new HttpRequestException("no network")
        };
        WindowsAppUpdater updater = new(packages, WindowsPlatform.Assets);

        UpdateException thrown = await Assert.ThrowsAsync<UpdateException>(() => updater.StageAsync(Release, CancellationToken.None));

        Assert.Equal("Update_DownloadFailed", thrown.Detail.Key);
    }

    [Fact]
    public async Task ACancelledDownloadStaysCancelled()
    {
        FakePackages packages = new()
        {
            Found = new PackageUpdate(new Version(0, 7, 0), "x.nupkg", new object()),
            DownloadFails = new OperationCanceledException()
        };
        WindowsAppUpdater updater = new(packages, WindowsPlatform.Assets);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updater.StageAsync(Release, CancellationToken.None));
    }

    [Fact]
    public async Task InstallingHandsTheStagedPackageToTheInstaller()
    {
        FakePackages packages = new() { Found = new PackageUpdate(new Version(0, 7, 0), "x.nupkg", new object()) };
        WindowsAppUpdater updater = new(packages, WindowsPlatform.Assets);
        StagedUpdate staged = await updater.StageAsync(Release, CancellationToken.None);

        updater.InstallOnExit(staged);

        Assert.Same(packages.Found, packages.Installed);
    }

    [Fact]
    public void SomethingThatWasNotStagedHereIsNotInstalled()
    {
        WindowsAppUpdater updater = new(new FakePackages(), WindowsPlatform.Assets);

        Assert.Throws<UpdateException>(() => updater.InstallOnExit(new StagedUpdate(new Version(0, 7, 0), "x", string.Empty)));
    }

    [Fact]
    public async Task AnInstallerThatCannotStartSaysSo()
    {
        FakePackages packages = new()
        {
            Found = new PackageUpdate(new Version(0, 7, 0), "x.nupkg", new object()),
            InstallFails = new IOException("no Update.exe")
        };
        WindowsAppUpdater updater = new(packages, WindowsPlatform.Assets);
        StagedUpdate staged = await updater.StageAsync(Release, CancellationToken.None);

        UpdateException thrown = Assert.Throws<UpdateException>(() => updater.InstallOnExit(staged));

        Assert.Equal("Update_CouldNotStart", thrown.Detail.Key);
    }

    /// <summary>A running copy, which was not put there by the installer, is not one an update can replace.</summary>
    [Fact]
    public void ACopyRunFromTheTestsIsNotAnInstalledOne()
    {
        Assert.False(new VelopackPackageUpdates().IsInstalled);
    }

    private sealed class FakePackages : IPackageUpdates
    {
        public bool IsInstalled { get; init; }

        public PackageUpdate? Found { get; init; }

        public Exception? DownloadFails { get; init; }

        public Exception? InstallFails { get; init; }

        public string? FolderAsked { get; private set; }

        public PackageUpdate? Downloaded { get; private set; }

        public PackageUpdate? Installed { get; private set; }

        public Task<PackageUpdate?> FindAsync(string releaseFolderUrl, CancellationToken cancellationToken)
        {
            this.FolderAsked = releaseFolderUrl;
            return Task.FromResult(this.Found);
        }

        public Task DownloadAsync(PackageUpdate update, CancellationToken cancellationToken)
        {
            if (this.DownloadFails is not null)
            {
                throw this.DownloadFails;
            }

            this.Downloaded = update;
            return Task.CompletedTask;
        }

        public void InstallOnExit(PackageUpdate update)
        {
            if (this.InstallFails is not null)
            {
                throw this.InstallFails;
            }

            this.Installed = update;
        }
    }
}
