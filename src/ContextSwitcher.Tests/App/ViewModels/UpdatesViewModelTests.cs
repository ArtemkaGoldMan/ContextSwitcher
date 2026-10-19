using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Tests.TestDoubles;
using ContextSwitcher.Core.Messages;

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
    private readonly FakeSystemNotifications notifications = new();

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

    /// <summary>What an update brings is shown before it's installed; the link then leads to the rest.</summary>
    [Fact]
    public async Task AnUpdatesNotesAreOfferedWithIt()
    {
        this.releases.Latest = NewRelease with { Notes = ["Update notifications.", "Turn a profile off."] };
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: true);

        Assert.True(updates.HasNotes);
        Assert.Equal("What's new in 0.2.0", updates.WhatsNewHeading);
        Assert.Equal(["Update notifications.", "Turn a profile off."], updates.AvailableNotes);
        Assert.Equal("Release notes on GitHub ↗", updates.ReleasePageLinkText);
    }

    /// <summary>A copy that cannot replace itself still hears about the update - as a download.</summary>
    [Fact]
    public async Task AnUpdateThisCopyCannotInstallIsOfferedAsADownload()
    {
        this.releases.Latest = NewRelease;
        this.updater.Blocker = UpdateMessages.RunsFromSource.With();
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
        this.releases.Failure = new UpdateException(UpdateMessages.GitHubUnreachable.With());
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

    /// <summary>
    /// A release that came out after the check is the one installed: the check had found 0.6.4, 0.6.5
    /// came out, and installing put in 0.6.4 only to offer 0.6.5 straight after.
    /// </summary>
    [Fact]
    public async Task InstallingAfterANewerReleaseCameOutInstallsTheNewest()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();
        await updates.CheckAsync(userAsked: false);
        this.releases.Latest = NewRelease with { Version = new Version(0, 3, 0) };

        await updates.InstallCommand.ExecuteAsync(null);

        Assert.Equal(new Version(0, 3, 0), this.updater.Installed?.Version);
        Assert.Equal(2, this.releases.Calls);
    }

    /// <summary>When GitHub can't be asked again, the release already found is installed all the same.</summary>
    [Fact]
    public async Task WithGitHubOutOfReachTheReleaseFoundIsInstalled()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();
        await updates.CheckAsync(userAsked: false);
        this.releases.Failure = new UpdateException(UpdateMessages.GitHubUnreachable.With());

        await updates.InstallCommand.ExecuteAsync(null);

        Assert.Equal(new Version(0, 2, 0), this.updater.Installed?.Version);
    }

    [Fact]
    public async Task AFailedInstallSaysWhyAndKeepsRunning()
    {
        this.releases.Latest = NewRelease;
        this.updater.StageFailure = new UpdateException(UpdateMessages.NotSigned.With());
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

    /// <summary>The daily check announces what it finds in Notification Center - once per version.</summary>
    [Fact]
    public async Task TheDailyCheckAnnouncesEachNewVersionOnce()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: false);
        await updates.CheckAsync(userAsked: false);

        (string id, string title, string body) = Assert.Single(this.notifications.Shown);
        Assert.Equal(UpdatesViewModel.NotificationId, id);
        Assert.Equal("Context Switcher 0.2.0 is available", title);
        Assert.Equal("Click to see what's new and install it.", body);

        this.releases.Latest = NewRelease with { Version = new Version(0, 3, 0) };
        await updates.CheckAsync(userAsked: false);

        Assert.Equal(["Context Switcher 0.2.0 is available", "Context Switcher 0.3.0 is available"], this.notifications.Shown.Select(shown => shown.Title));
    }

    /// <summary>Someone who clicked Check now is reading the answer already - and it isn't repeated later.</summary>
    [Fact]
    public async Task AVersionFoundByCheckNowIsNotAnnounced()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: true);
        await updates.CheckAsync(userAsked: false);

        Assert.Empty(this.notifications.Shown);
    }

    /// <summary>Once up to date, a notice about the version now installed is taken out of Notification Center.</summary>
    [Fact]
    public async Task BeingUpToDateTakesTheNoticeAway()
    {
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: false);

        Assert.Empty(this.notifications.Shown);
        Assert.Equal([UpdatesViewModel.NotificationId], this.notifications.Removed);
    }

    /// <summary>
    /// The daily check says nothing to the person when it fails, so a log is the only place it is
    /// said - and it is, with why.
    /// </summary>
    [Fact]
    public async Task AFailedAutomaticCheckIsLoggedThoughNothingIsShown()
    {
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);
        this.releases.Failure = new UpdateException(UpdateMessages.GitHubUnreachable.With());
        UpdatesViewModel updates = this.Create();

        await updates.CheckAsync(userAsked: false);

        LogEntry entry = Assert.Single(logger.Entries, e => e.EventId == "CheckFailed");
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Update_GitHubUnreachable", entry.Data!["key"]);
        Assert.Equal("False", entry.Data["userAsked"]);
    }

    [Fact]
    public async Task ACheckLeavesANoteOfWhatItFound()
    {
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);
        this.releases.Latest = NewRelease;

        await this.Create().CheckAsync(userAsked: false);

        LogEntry note = Assert.Single(logger.Entries, e => e.EventId == "CheckFinished");
        Assert.Equal(LogLevel.Information, note.Level);
        Assert.Equal("The latest release is 0.2.0; this is 0.1.0.", note.Message);
    }

    /// <summary>An update about to be installed is written whether or not anything goes wrong - it is what explains a restart.</summary>
    [Fact]
    public async Task AnInstallIsWrittenAndAFailedOneIsAnError()
    {
        TestLogger logger = new();
        using IDisposable scope = AppLog.UseForThisFlow(logger);
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();
        await updates.CheckAsync(userAsked: true);

        await updates.InstallCommand.ExecuteAsync(null);

        Assert.All(logger.Entries.Where(e => e.EventId is "UpdateStarted" or "UpdateStaged"), e => Assert.True(e.Keep));
        Assert.Equal(["UpdateStarted", "UpdateStaged"], logger.Entries.Where(e => e.Keep).Select(e => e.EventId));

        TestLogger failing = new();
        using IDisposable inner = AppLog.UseForThisFlow(failing);
        this.updater.StageFailure = new UpdateException(UpdateMessages.NotSigned.With());
        await updates.CheckAsync(userAsked: true);
        await updates.InstallCommand.ExecuteAsync(null);

        LogEntry error = Assert.Single(failing.Entries, e => e.EventId == "InstallFailed");
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Equal("Update_NotSigned", error.Data!["key"]);
    }

    private UpdatesViewModel Create() =>
        new(new AppState(), this.releases, this.updater, this.processRunner, this.notifications, new Version(0, 1, 0), () => new DateTimeOffset(2026, 10, 10, 14, 32, 0, TimeSpan.Zero));
}
