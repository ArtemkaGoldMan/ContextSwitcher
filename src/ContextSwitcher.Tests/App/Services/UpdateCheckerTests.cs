using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.Services;

/// <summary>
/// The update checks, whoever starts them - the daily one, Check now, a click on the notification -
/// and installing what they found. What Settings says about it is in UpdatesViewModelTests.
/// </summary>
public sealed class UpdateCheckerTests
{
    private static readonly ReleaseInfo NewRelease = new(
        new Version(0, 2, 0),
        "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0",
        "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip");

    private readonly FakeReleaseSource releases = new();
    private readonly FakeAppUpdater updater = new();
    private readonly FakeSystemNotifications notifications = new();
    private readonly FakeClock clock = new() { UtcNow = new DateTimeOffset(2026, 10, 10, 14, 32, 0, TimeSpan.Zero) };
    private readonly TestLogger logger = new();

    [Fact]
    public async Task ANewerReleaseIsOfferedForInstalling()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: true);

        Assert.Same(NewRelease, checker.Available);
        Assert.Null(checker.InstallBlocker);
        Assert.Equal(new UpdateStatus(UpdateStage.Available, new Version(0, 2, 0)), checker.Status);
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.0.9")]
    public async Task TheSameOrAnOlderReleaseIsUpToDateAsOfTheCheck(string latest)
    {
        ReleaseVersion.TryParse(latest, out Version version);
        this.releases.Latest = NewRelease with { Version = version };
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: true);

        Assert.Null(checker.Available);
        Assert.Equal(new UpdateStatus(UpdateStage.UpToDate, At: this.clock.UtcNow), checker.Status);
    }

    [Fact]
    public async Task NothingPublishedYetIsUpToDate()
    {
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: true);

        Assert.Null(checker.Available);
        Assert.Equal(UpdateStage.UpToDate, checker.Status.Stage);
    }

    /// <summary>A copy that cannot replace itself still hears about the update, with why it can't.</summary>
    [Fact]
    public async Task AnUpdateThisCopyCannotInstallSaysWhy()
    {
        this.releases.Latest = NewRelease;
        this.updater.Blocker = UpdateMessages.RunsFromSource.With();
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: true);

        Assert.Same(NewRelease, checker.Available);
        Assert.Equal(this.updater.Blocker, checker.InstallBlocker);
    }

    /// <summary>Offline, the daily check keeps quiet - only a check someone asked for reports a failure.</summary>
    [Fact]
    public async Task OnlyACheckSomeoneAskedForReportsAFailure()
    {
        UpdateException offline = new(UpdateMessages.GitHubUnreachable.With());
        this.releases.Failure = offline;
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: false);
        Assert.Same(UpdateStatus.NotCheckedYet, checker.Status);

        await checker.CheckAsync(userAsked: true);
        Assert.Equal(new UpdateStatus(UpdateStage.CouldNotCheck, Problem: offline.Detail), checker.Status);
    }

    /// <summary>Whoever started a check, those showing it hear when it starts and when it ends.</summary>
    [Fact]
    public async Task ACheckSaysWhenItStartsAndEnds()
    {
        UpdateChecker checker = this.Create();
        List<bool> busy = [];
        checker.Changed += (_, _) => busy.Add(checker.IsBusy);

        await checker.CheckAsync(userAsked: false);

        Assert.Equal([true, false], busy);
    }

    [Fact]
    public async Task InstallingHandsTheDownloadOverAndAsksForARestart()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();
        bool restartRequested = false;
        checker.RestartRequested += (_, _) => restartRequested = true;
        await checker.CheckAsync(userAsked: true);

        await checker.InstallAsync();

        Assert.Equal(new Version(0, 2, 0), this.updater.Installed?.Version);
        Assert.True(restartRequested);
        Assert.Equal(UpdateStage.Restarting, checker.Status.Stage);
    }

    /// <summary>
    /// A release that came out after the check is the one installed: the check had found 0.6.4, 0.6.5
    /// came out, and installing put in 0.6.4 only to offer 0.6.5 straight after.
    /// </summary>
    [Fact]
    public async Task InstallingAfterANewerReleaseCameOutInstallsTheNewest()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();
        await checker.CheckAsync(userAsked: false);
        this.releases.Latest = NewRelease with { Version = new Version(0, 3, 0) };

        await checker.InstallAsync();

        Assert.Equal(new Version(0, 3, 0), this.updater.Installed?.Version);
        Assert.Equal(2, this.releases.Calls);
    }

    /// <summary>When GitHub can't be asked again, the release already found is installed all the same.</summary>
    [Fact]
    public async Task WithGitHubOutOfReachTheReleaseFoundIsInstalled()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();
        await checker.CheckAsync(userAsked: false);
        this.releases.Failure = new UpdateException(UpdateMessages.GitHubUnreachable.With());

        await checker.InstallAsync();

        Assert.Equal(new Version(0, 2, 0), this.updater.Installed?.Version);
    }

    [Fact]
    public async Task AFailedInstallSaysWhyAndKeepsTheOffer()
    {
        this.releases.Latest = NewRelease;
        UpdateException notSigned = new(UpdateMessages.NotSigned.With());
        this.updater.StageFailure = notSigned;
        UpdateChecker checker = this.Create();
        bool restartRequested = false;
        checker.RestartRequested += (_, _) => restartRequested = true;
        await checker.CheckAsync(userAsked: true);

        await checker.InstallAsync();

        Assert.Null(this.updater.Installed);
        Assert.False(restartRequested);
        Assert.Equal(new UpdateStatus(UpdateStage.CouldNotInstall, new Version(0, 2, 0), Problem: notSigned.Detail), checker.Status);
        Assert.Same(NewRelease, checker.Available);
        Assert.False(checker.IsBusy);
    }

    /// <summary>A later check that finds nothing newer takes the offer away - the menu entry with it.</summary>
    [Fact]
    public async Task ALaterCheckWithNothingNewerWithdrawsTheOffer()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();
        await checker.CheckAsync(userAsked: false);
        int changes = 0;
        checker.Changed += (_, _) => changes++;

        this.releases.Latest = null;
        await checker.CheckAsync(userAsked: false);

        Assert.Null(checker.Available);
        Assert.NotEqual(0, changes);
    }

    /// <summary>The daily check announces what it finds in Notification Center - once per version.</summary>
    [Fact]
    public async Task TheDailyCheckAnnouncesEachNewVersionOnce()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: false);
        await checker.CheckAsync(userAsked: false);

        (string id, string title, string body) = Assert.Single(this.notifications.Shown);
        Assert.Equal(UpdateChecker.NotificationId, id);
        Assert.Equal("Context Switcher 0.2.0 is available", title);
        Assert.Equal("Click to see what's new and install it.", body);

        this.releases.Latest = NewRelease with { Version = new Version(0, 3, 0) };
        await checker.CheckAsync(userAsked: false);

        Assert.Equal(["Context Switcher 0.2.0 is available", "Context Switcher 0.3.0 is available"], this.notifications.Shown.Select(shown => shown.Title));
    }

    /// <summary>Someone who clicked Check now is reading the answer already - and it isn't repeated later.</summary>
    [Fact]
    public async Task AVersionFoundByCheckNowIsNotAnnounced()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: true);
        await checker.CheckAsync(userAsked: false);

        Assert.Empty(this.notifications.Shown);
    }

    /// <summary>Once up to date, a notice about the version now installed is taken out of Notification Center.</summary>
    [Fact]
    public async Task BeingUpToDateTakesTheNoticeAway()
    {
        UpdateChecker checker = this.Create();

        await checker.CheckAsync(userAsked: false);

        Assert.Empty(this.notifications.Shown);
        Assert.Equal([UpdateChecker.NotificationId], this.notifications.Removed);
    }

    /// <summary>
    /// The daily check says nothing to the person when it fails, so a log is the only place it is
    /// said - and it is, with why.
    /// </summary>
    [Fact]
    public async Task AFailedAutomaticCheckIsLoggedThoughNothingIsShown()
    {
        this.releases.Failure = new UpdateException(UpdateMessages.GitHubUnreachable.With());

        await this.Create().CheckAsync(userAsked: false);

        LogEntry entry = Assert.Single(this.logger.Entries, e => e.EventId == "CheckFailed");
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Update_GitHubUnreachable", entry.Data!["key"]);
        Assert.Equal("False", entry.Data["userAsked"]);
    }

    [Fact]
    public async Task ACheckLeavesANoteOfWhatItFound()
    {
        this.releases.Latest = NewRelease;

        await this.Create().CheckAsync(userAsked: false);

        LogEntry note = Assert.Single(this.logger.Entries, e => e.EventId == "CheckFinished");
        Assert.Equal(LogLevel.Information, note.Level);
        Assert.Equal("The latest release is 0.2.0; this is 0.1.0.", note.Message);
    }

    /// <summary>An update about to be installed is written whether or not anything goes wrong - it is what explains a restart.</summary>
    [Fact]
    public async Task AnInstallIsWrittenAndAFailedOneIsAnError()
    {
        this.releases.Latest = NewRelease;
        UpdateChecker checker = this.Create();
        await checker.CheckAsync(userAsked: true);

        await checker.InstallAsync();

        Assert.All(this.logger.Entries.Where(e => e.EventId is "UpdateStarted" or "UpdateStaged"), e => Assert.True(e.Keep));
        Assert.Equal(["UpdateStarted", "UpdateStaged"], this.logger.Entries.Where(e => e.Keep).Select(e => e.EventId));

        this.updater.StageFailure = new UpdateException(UpdateMessages.NotSigned.With());
        await checker.CheckAsync(userAsked: true);
        await checker.InstallAsync();

        LogEntry error = Assert.Single(this.logger.Entries, e => e.EventId == "InstallFailed");
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Equal("Update_NotSigned", error.Data!["key"]);
    }

    private UpdateChecker Create() =>
        new(this.releases, this.updater, this.notifications, new AppState(), new FakeAppInfo(new Version(0, 1, 0)), this.clock, this.logger);
}
