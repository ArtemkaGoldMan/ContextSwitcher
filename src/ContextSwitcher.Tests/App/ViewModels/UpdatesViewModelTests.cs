using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Tests.TestDoubles;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Tests.App.ViewModels;

/// <summary>What Settings says about updates. The checks and installing themselves are in UpdateCheckerTests.</summary>
public sealed class UpdatesViewModelTests
{
    private static readonly ReleaseInfo NewRelease = new(
        new Version(0, 2, 0),
        "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0",
        "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip");

    private readonly FakeReleaseSource releases = new();
    private readonly FakeAppUpdater updater = new();
    private readonly FakeShell shell = new();

    /// <summary>14:32 where the tests run: the page gives the time of the check in the Mac's own time.</summary>
    private readonly FakeClock clock = new() { UtcNow = new DateTimeOffset(new DateTime(2026, 10, 10, 14, 32, 0, DateTimeKind.Local)) };

    private readonly UpdateChecker checker;

    public UpdatesViewModelTests()
    {
        this.checker = new UpdateChecker(
            this.releases, this.updater, new FakeSystemNotifications(), new AppState(), new FakeAppInfo(new Version(0, 1, 0)), this.clock, new TestLogger());
    }

    [Fact]
    public async Task ANewerReleaseIsOfferedForInstalling()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();

        await updates.CheckNowCommand.ExecuteAsync(null);

        Assert.True(updates.IsUpdateAvailable);
        Assert.True(updates.CanInstall);
        Assert.Equal("Version 0.2.0 is available.", updates.StatusText);
        Assert.Equal("What's new in 0.2.0 ↗", updates.ReleasePageLinkText);
        Assert.True(updates.InstallCommand.CanExecute(null));
    }

    [Fact]
    public async Task UpToDateSaysWhenItWasChecked()
    {
        this.releases.Latest = NewRelease with { Version = new Version(0, 1, 0) };
        UpdatesViewModel updates = this.Create();

        await updates.CheckNowCommand.ExecuteAsync(null);

        Assert.False(updates.IsUpdateAvailable);
        Assert.False(updates.CanInstall);
        Assert.Equal("Up to date. Checked at 14:32.", updates.StatusText);
    }

    [Fact]
    public void BeforeAnyCheckItSaysSo()
    {
        UpdatesViewModel updates = this.Create();

        Assert.Equal("Version 0.1.0", updates.CurrentVersionText);
        Assert.Equal("Not checked yet.", updates.StatusText);
    }

    /// <summary>The daily check, or a click on the notification, shows on the page as Check now does.</summary>
    [Fact]
    public async Task ACheckStartedElsewhereShowsHereToo()
    {
        this.releases.Latest = NewRelease;
        UpdatesViewModel updates = this.Create();
        List<string?> changed = [];
        updates.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await this.checker.CheckAsync(userAsked: false);

        Assert.True(updates.IsUpdateAvailable);
        Assert.Equal("Version 0.2.0 is available.", updates.StatusText);
        Assert.Contains(nameof(UpdatesViewModel.IsUpdateAvailable), changed);
        Assert.Contains(nameof(UpdatesViewModel.StatusText), changed);
    }

    /// <summary>What an update brings is shown before it's installed; the link then leads to the rest.</summary>
    [Fact]
    public async Task AnUpdatesNotesAreOfferedWithIt()
    {
        this.releases.Latest = NewRelease with { Notes = ["Update notifications.", "Turn a profile off."] };
        UpdatesViewModel updates = this.Create();

        await updates.CheckNowCommand.ExecuteAsync(null);

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

        await updates.CheckNowCommand.ExecuteAsync(null);

        Assert.True(updates.IsUpdateAvailable);
        Assert.False(updates.CanInstall);
        Assert.True(updates.HasInstallBlocker);
        Assert.Equal("Download 0.2.0 from GitHub ↗", updates.ReleasePageLinkText);
        Assert.False(updates.InstallCommand.CanExecute(null));

        updates.OpenReleasePageCommand.Execute(null);
        Assert.Equal(["https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0"], this.shell.Links);
    }

    /// <summary>The page address comes off the network; only a GitHub page is ever handed to `open`.</summary>
    [Fact]
    public async Task OnlyAGitHubPageIsOpened()
    {
        this.releases.Latest = NewRelease with { PageUrl = "file:///Applications/Calculator.app" };
        UpdatesViewModel updates = this.Create();
        await updates.CheckNowCommand.ExecuteAsync(null);

        updates.OpenReleasePageCommand.Execute(null);

        Assert.Empty(this.shell.Links);
    }

    /// <summary>Offline, the daily check keeps quiet - only a check someone asked for reports a failure.</summary>
    [Fact]
    public async Task OnlyACheckSomeoneAskedForReportsAFailure()
    {
        this.releases.Failure = new UpdateException(UpdateMessages.GitHubUnreachable.With());
        UpdatesViewModel updates = this.Create();

        await this.checker.CheckAsync(userAsked: false);
        Assert.Equal("Not checked yet.", updates.StatusText);

        await updates.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal("Couldn't check for updates. Couldn't reach GitHub. Check the internet connection.", updates.StatusText);
    }

    [Fact]
    public async Task AFailedInstallSaysWhyAndCanBeTriedAgain()
    {
        this.releases.Latest = NewRelease;
        this.updater.StageFailure = new UpdateException(UpdateMessages.NotSigned.With());
        UpdatesViewModel updates = this.Create();
        await updates.CheckNowCommand.ExecuteAsync(null);

        await updates.InstallCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't install the update. The download isn't signed with Context Switcher's certificate, so it wasn't installed.", updates.StatusText);
        Assert.True(updates.InstallCommand.CanExecute(null));
    }

    private UpdatesViewModel Create() => new(this.checker, this.shell);
}
