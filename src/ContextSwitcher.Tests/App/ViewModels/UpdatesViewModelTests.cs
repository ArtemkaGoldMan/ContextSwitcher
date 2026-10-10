using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class UpdatesViewModelTests
{
    private static readonly ReleaseInfo NewRelease = new(
        new Version(0, 2, 0),
        "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0",
        "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip");

    private readonly FakeReleaseSource releases = new();
    private readonly FakeAppUpdater updater = new();
    private readonly FakeProcessRunner processRunner = new();

    [Fact]
    public async Task ANewerReleaseIsOfferedForInstalling()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: true);

        Assert.True(updates.IsUpdateAvailable);
        Assert.True(updates.CanInstall);
        Assert.Equal("Version 0.2.0 is available.", updates.StatusText);
        Assert.Equal("What's new in 0.2.0 ↗", updates.ReleasePageLinkText);
        Assert.True(updates.InstallCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.0.9")]
    public async Task TheSameOrAnOlderReleaseIsUpToDate(string latest)
    {
        ReleaseVersion.TryParse(latest, out Version version);
        this.releases.Latest = NewRelease with { Version = version };
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: true);

        Assert.False(updates.IsUpdateAvailable);
        Assert.False(updates.CanInstall);
        Assert.Equal("Up to date. Checked at 14:32.", updates.StatusText);
    }

    [Fact]
    public async Task NothingPublishedYetIsUpToDate()
    {
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: true);

        Assert.False(updates.IsUpdateAvailable);
        Assert.StartsWith("Up to date.", updates.StatusText, StringComparison.Ordinal);
    }

    /// <summary>A copy that cannot replace itself still hears about the update - as a download.</summary>
    [Fact]
    public async Task AnUpdateThisCopyCannotInstallIsOfferedAsADownload()
    {
        this.releases.Latest = NewRelease;
        this.updater.Blocker = "This copy runs from source rather than as an installed app, so it can't update itself.";
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: true);

        Assert.True(updates.IsUpdateAvailable);
        Assert.False(updates.CanInstall);
        Assert.True(updates.HasInstallBlocker);
        Assert.Equal("Download 0.2.0 from GitHub ↗", updates.ReleasePageLinkText);
        Assert.False(updates.InstallCommand.CanExecute(null));

        updates.OpenReleasePageCommand.Execute(null);
        ProcessStartOptions open = Assert.Single(this.processRunner.Calls);
        Assert.Equal(["https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0"], open.Arguments);
    }

    /// <summary>The page address comes off the network; only a GitHub page is ever handed to `open`.</summary>
    [Fact]
    public async Task OnlyAGitHubPageIsOpened()
    {
        this.releases.Latest = NewRelease with { PageUrl = "file:///Applications/Calculator.app" };
        UpdatesViewModel updates = this.Create();
        await updates.CheckAsync(userAsked: true);

        updates.OpenReleasePageCommand.Execute(null);

        Assert.Empty(this.processRunner.Calls);
    }

    /// <summary>Offline, the daily check keeps quiet - only a check someone asked for reports a failure.</summary>
    [Fact]
    public async Task OnlyACheckSomeoneAskedForReportsAFailure()
    {
        this.releases.Failure = new UpdateException("Couldn't reach GitHub. Check the internet connection.");
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: false);
        Assert.Equal("Not checked yet.", updates.StatusText);

        await updates.CheckAsync(userAsked: true);
        Assert.Equal("Couldn't check for updates. Couldn't reach GitHub. Check the internet connection.", updates.StatusText);
    }

    [Fact]
    public async Task InstallingHandsTheDownloadOverAndAsksForARestart()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();
        bool restartRequested = false;
        updates.RestartRequested += (_, _) => restartRequested = true;
        await updates.CheckAsync(userAsked: true);

        updates.InstallCommand.Execute(null);

        Assert.Equal(new Version(0, 2, 0), this.updater.Installed?.Version);
        Assert.True(restartRequested);
    }

    [Fact]
    public async Task AFailedInstallSaysWhyAndKeepsRunning()
    {
        this.releases.Latest = NewRelease;
        this.updater.StageFailure = new UpdateException("The download isn't signed with Context Switcher's certificate, so it wasn't installed.");
        UpdatesViewModel updates = this.Create();
        bool restartRequested = false;
        updates.RestartRequested += (_, _) => restartRequested = true;
        await updates.CheckAsync(userAsked: true);

        updates.InstallCommand.Execute(null);

        Assert.Null(this.updater.Installed);
        Assert.False(restartRequested);
        Assert.Equal("Couldn't install the update. The download isn't signed with Context Switcher's certificate, so it wasn't installed.", updates.StatusText);
        Assert.True(updates.InstallCommand.CanExecute(null));
    }

    /// <summary>A later check that finds nothing newer takes the offer away - the menu entry with it.</summary>
    [Fact]
    public async Task ALaterCheckWithNothingNewerWithdrawsTheOffer()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();
        await updates.CheckAsync(userAsked: false);
        List<string?> changed = [];
        updates.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        this.releases.Latest = null;
        await updates.CheckAsync(userAsked: false);

        Assert.False(updates.IsUpdateAvailable);
        Assert.Contains(nameof(UpdatesViewModel.IsUpdateAvailable), changed);
    }

    private UpdatesViewModel Create() =>
        new(this.releases, this.updater, this.processRunner, new Version(0, 1, 0), () => new DateTimeOffset(2026, 10, 10, 14, 32, 0, TimeSpan.Zero));
}
