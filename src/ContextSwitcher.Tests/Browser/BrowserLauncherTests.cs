using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Browser;

/// <summary>
/// What a switch decides in the browser: which pages to open, which tabs to focus, remember and close.
/// How the Mac's browsers are asked is in MacBrowserControlTests.
/// </summary>
public sealed class BrowserLauncherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly FakeBrowserControl browser = new();
    private readonly FakeOpenedTabsStore store = new();
    private readonly FakeBrowserRuns runs = new();
    private readonly TestLogger logger = new();

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeOpensInTheDefaultBrowser()
    {
        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], false, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Equal([(BrowserKind.Default, "https://example.com/")], this.browser.Opened);
        Assert.Empty(this.browser.Listed);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeTargetsNamedBrowser()
    {
        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], false, [], Timeout),
            CancellationToken.None);

        Assert.Equal([(BrowserKind.Chrome, "https://example.com/")], this.browser.Opened);
    }

    /// <summary>
    /// The tab listing is one look for the whole context, so a URL that is already open costs that
    /// listing plus a single focus - never an `open`, and never a sweep per URL.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeSkipsOpenWhenExistingTabFocused()
    {
        this.browser.WithTabs(new BrowserTab(null, "https://other.example/"), new BrowserTab(null, "https://example.com/"));

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Empty(this.browser.Opened);
        Assert.Single(this.browser.Listed);
        Assert.Equal(["https://example.com/"], this.browser.Focused);
    }

    /// <summary>
    /// Three URLs that are all already open still cost exactly one listing plus one focus, rather
    /// than growing a tab sweep at a time the way the per-URL probe did.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeChecksExistingTabsWithASingleListing()
    {
        this.browser.WithTabs(new BrowserTab(1, "https://a.example/"), new BrowserTab(2, "https://b.example/"), new BrowserTab(3, "https://c.example/"));

        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls,
                BrowserKind.Chrome,
                ["https://a.example/", "https://b.example/", "https://c.example/"],
                true, [], Timeout),
            CancellationToken.None);

        Assert.Empty(this.browser.Opened);
        Assert.Single(this.browser.Listed);
        Assert.Equal(["https://c.example/"], this.browser.Focused);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeOpensWhenNoExistingTabFound()
    {
        this.browser.WithTabs(new BrowserTab(null, "https://unrelated.example/"));

        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Single(this.browser.Opened);
        Assert.Empty(this.browser.Focused);
    }

    /// <summary>
    /// The regression behind the "no URL opened at all" bug: a browser that does not answer fails
    /// the duplicate-tab check, and docs/architecture.md requires falling back to a plain open rather than
    /// giving up on the URL.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeStillOpensWhenTabInspectionFails()
    {
        this.browser.Failing(BrowserProblemKind.NotAnswering);

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Equal([(BrowserKind.Chrome, "https://example.com/")], this.browser.Opened);
        Assert.Contains(outcome.Warnings, warning => warning.English.Contains("Google Chrome", StringComparison.Ordinal));
    }

    /// <summary>
    /// Checking is best-effort, so it may never spend the whole step budget - that is what starved
    /// the open fallback when a browser hung.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeGivesTabInspectionOnlyASliceOfTheStepTimeout()
    {
        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.True(Assert.Single(this.browser.Listed).Timeout < Timeout);
    }

    /// <summary>
    /// Once inspection has failed once the browser can't be asked, so asking again for every
    /// remaining URL would only burn the step's budget to reach the same answer.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeStopsProbingAfterInspectionFailsOnce()
    {
        this.browser.Failing(BrowserProblemKind.NotAnswering);

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls,
                BrowserKind.Chrome,
                ["https://example.com/", "https://example.org/", "https://example.net/"],
                true,
                [],
                Timeout),
            CancellationToken.None);

        Assert.Single(this.browser.Listed);
        Assert.Equal(3, this.browser.Opened.Count);
        Assert.Single(outcome.Warnings);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeReportsWarningWhenAPageDoesNotOpen()
    {
        this.browser.OpenProblem = new BrowserProblem(BrowserProblemKind.Failed, "no such app");

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], false, [], Timeout),
            CancellationToken.None);

        Assert.Equal("Could not open 'https://example.com/': no such app", Assert.Single(outcome.Warnings).English);
    }

    /// <summary>
    /// Tab groups were dropped - no browser lets another app open one. A request still saying
    /// "groups" (the plan never makes one now) touches nothing rather than trying.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncLegacyGroupsModeDoesNothing()
    {
        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Safari, ["https://fallback.example/"], false, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Empty(this.browser.Opened);
        Assert.Empty(this.browser.Listed);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncProfilesModeOpensEachProfileWithItsPages()
    {
        BrowserProfileConfig[] profiles =
        [
            new() { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1", Urls = ["https://mail.example/"] },
            new() { Browser = BrowserKind.Brave, ProfileDirectory = "Default", Urls = [] }
        ];

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Profiles, BrowserKind.Default, [], false, profiles, Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Equal(
            [(BrowserKind.Chrome, "Profile 1", "https://mail.example/"), (BrowserKind.Brave, "Default", "")],
            this.browser.OpenedProfiles.Select(opened => (opened.Browser, opened.ProfileDirectory, string.Join(' ', opened.Urls))));
    }

    [Fact]
    public async Task ManageBrowserContextAsyncProfilesModeReportsWarningOnFailure()
    {
        this.browser.OpenProblem = new BrowserProblem(BrowserProblemKind.Failed, "profile locked");
        BrowserProfileConfig[] profiles = [new() { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1" }];

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Profiles, BrowserKind.Default, [], false, profiles, Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
    }

    /// <summary>
    /// The tab Gmail redirected to is still Gmail: it is focused by its own address, and no second
    /// tab is opened.
    /// </summary>
    [Fact]
    public async Task ADuplicateIsFoundUnderTheAddressThePageMovedTo()
    {
        this.browser.WithTabs(new BrowserTab(4, "https://mail.google.com/mail/u/0/#inbox"));

        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://mail.google.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Empty(this.browser.Opened);
        Assert.Equal(["https://mail.google.com/mail/u/0/#inbox"], this.browser.Focused);
    }

    /// <summary>
    /// Only the profile's pages close, and a page the next profile opens stays. A browser that isn't
    /// running isn't started to be asked.
    /// </summary>
    [Fact]
    public async Task ClosingTabsClosesTheProfilesPagesAndNothingElse()
    {
        this.browser.WithTabs(
            new BrowserTab(11, "https://linear.app/team/issues"), new BrowserTab(12, "https://news.example/"), new BrowserTab(13, "https://mail.google.com/mail/u/0/"));

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(
            BrowserKind.Chrome, ["https://linear.app/", "https://mail.google.com/"], ["https://mail.google.com/"], null, Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(1, null), outcome);
        Assert.False(Assert.Single(this.browser.Listed).StartIfNeeded);
        Assert.Equal([new BrowserTab(11, "https://linear.app/team/issues")], Assert.Single(this.browser.Closed));
    }

    /// <summary>Safari's tabs have no id, so the tab to close is the one at the address Safari reported.</summary>
    [Fact]
    public async Task SafariTabsCloseByTheirAddress()
    {
        this.browser.WithTabs(new BrowserTab(null, "https://linear.app/team/issues"), new BrowserTab(null, "https://news.example/"));

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Safari, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(1, null), outcome);
        Assert.Equal([new BrowserTab(null, "https://linear.app/team/issues")], Assert.Single(this.browser.Closed));
    }

    /// <summary>
    /// The friend's report: Fusion's tabs stayed open. A page often moves on from the address it was
    /// opened at - to a sign-in page, or the app's own domain - and wasn't recognised by address any
    /// more. The tab opened for a page marked Close is remembered by its id when the profile is
    /// entered, and closed by it when the profile is left, wherever it has gone.
    /// </summary>
    [Fact]
    public async Task ATabOpenedForAPageThatClosesIsClosedWhereverItWent()
    {
        BrowserLauncher launcher = this.Create();

        // Entering: Gmail is already open; Fusion opens, and its new tab is 7.
        this.browser
            .WithTabs(new BrowserTab(3, "https://mail.google.com/mail/u/0/"))
            .WithTabs(new BrowserTab(3, "https://mail.google.com/mail/u/0/"), new BrowserTab(7, "https://fusion.online.autodesk.com/"));
        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://mail.google.com/", "https://fusion.online.autodesk.com/"], true, [], Timeout,
                "work", ["https://fusion.online.autodesk.com/"]),
            CancellationToken.None);

        OpenedTabs remembered = this.store.Tabs["work"];
        Assert.Equal((BrowserKind.Chrome, "run-1"), (remembered.Browser, remembered.BrowserRun));
        Assert.Equal([7], remembered.TabIds);
        Assert.Single(this.browser.Opened);

        // Leaving: tab 7 has gone to Autodesk's sign-in, another site; a tab the user opened stays.
        this.browser.WithTabs(
            new BrowserTab(3, "https://mail.google.com/mail/u/0/"), new BrowserTab(7, "https://signin.autodesk.com/idmgr/"), new BrowserTab(9, "https://www.youtube.com/"));
        TabClosingOutcome outcome = await launcher.CloseTabsAsync(
            BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], [], "work", Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(1, null), outcome);
        Assert.Equal([new BrowserTab(7, "https://signin.autodesk.com/idmgr/")], Assert.Single(this.browser.Closed));
        Assert.Empty(this.store.Tabs);
    }

    /// <summary>
    /// Only pages marked Close are remembered; one that only opens is left to the user. Tabs appear
    /// in the order they were opened, each numbered above the last.
    /// </summary>
    [Fact]
    public async Task OnlyTabsOpenedForPagesThatCloseAreRemembered()
    {
        this.browser
            .WithTabs()
            .WithTabs(new BrowserTab(20, "https://docs.example/"), new BrowserTab(21, "https://fusion.online.autodesk.com/"));

        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://docs.example/", "https://fusion.online.autodesk.com/"], true, [], Timeout,
                "work", ["https://fusion.online.autodesk.com/"]),
            CancellationToken.None);

        Assert.Equal([21], this.store.Tabs["work"].TabIds);
    }

    /// <summary>
    /// Mixed in: a tab the user opened while the profile's pages were opening is never taken for
    /// the profile's. With more new tabs than pages, only one still showing a page counts.
    /// </summary>
    [Fact]
    public async Task ATabSomeoneElseOpenedMeanwhileIsNotRemembered()
    {
        this.browser
            .WithTabs(new BrowserTab(1, "https://news.example/"))
            .WithTabs(new BrowserTab(1, "https://news.example/"), new BrowserTab(5, "https://www.youtube.com/"), new BrowserTab(6, "https://fusion.online.autodesk.com/"));

        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], true, [], Timeout,
                "work", ["https://fusion.online.autodesk.com/"]),
            CancellationToken.None);

        Assert.Equal([6], this.store.Tabs["work"].TabIds);
    }

    /// <summary>Remembering is a nicety: when it can't be written, the tabs still close by address, and the log says so.</summary>
    [Fact]
    public async Task TabsThatCouldNotBeRememberedAreLogged()
    {
        this.store.Failure = new IOException("disk full");
        this.browser
            .WithTabs()
            .WithTabs(new BrowserTab(21, "https://fusion.online.autodesk.com/"));

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], true, [], Timeout,
                "work", ["https://fusion.online.autodesk.com/"]),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Equal("OpenedTabsNotRemembered", Assert.Single(this.logger.Entries).EventId);
    }

    /// <summary>
    /// A browser numbers its tabs afresh each time it starts, so after a restart a remembered id may
    /// be any tab at all. Those are dropped, and only tabs showing the profile's pages close.
    /// </summary>
    [Fact]
    public async Task IdsFromAnEarlierRunOfTheBrowserAreNotUsed()
    {
        this.store.Tabs["work"] = new OpenedTabs(BrowserKind.Chrome, "run-1", [7]);
        this.runs.Run = "run-2";
        this.browser.WithTabs(new BrowserTab(7, "https://www.youtube.com/"), new BrowserTab(8, "https://fusion.online.autodesk.com/home"));

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(
            BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], [], "work", Timeout, CancellationToken.None);

        Assert.Equal(1, outcome.Closed);
        Assert.Equal([new BrowserTab(8, "https://fusion.online.autodesk.com/home")], Assert.Single(this.browser.Closed));
        Assert.Empty(this.store.Tabs);
    }

    /// <summary>A remembered tab showing a page the next profile opens is left open, like any other such tab.</summary>
    [Fact]
    public async Task ARememberedTabTheNextProfileOpensStays()
    {
        this.store.Tabs["work"] = new OpenedTabs(BrowserKind.Chrome, "run-1", [7, 8]);
        this.browser.WithTabs(new BrowserTab(7, "https://calendar.google.com/r/week"), new BrowserTab(8, "https://signin.autodesk.com/"));

        await this.Create().CloseTabsAsync(
            BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], ["https://calendar.google.com/"], "work", Timeout, CancellationToken.None);

        Assert.Equal([new BrowserTab(8, "https://signin.autodesk.com/")], Assert.Single(this.browser.Closed));
    }

    /// <summary>
    /// Nothing to close later means nothing to remember: no second listing, and the switch is no slower.
    /// </summary>
    [Fact]
    public async Task APageThatOnlyOpensCostsNoExtraListing()
    {
        this.browser.WithTabs(new BrowserTab(1, "https://news.example/"));

        await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://docs.example/"], true, [], Timeout, "work", []),
            CancellationToken.None);

        Assert.Single(this.browser.Listed);
        Assert.Empty(this.store.Tabs);
    }

    [Fact]
    public async Task WithNoTabToCloseTheBrowserIsOnlyAsked()
    {
        this.browser.WithTabs(new BrowserTab(null, "https://news.example/"));

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Safari, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(0, null), outcome);
        Assert.Single(this.browser.Listed);
        Assert.Empty(this.browser.Closed);
    }

    /// <summary>
    /// A default browser that can't be scripted - the plan leaves Default only for those - can't be
    /// asked for its tabs: nothing is tried, and the switch says why.
    /// </summary>
    [Fact]
    public async Task AnUnscriptableDefaultBrowserIsReportedAndLeftAlone()
    {
        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Default, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(0, outcome.Closed);
        Assert.Equal("Switch_CloseTabsNeedsBrowser", outcome.Problem?.Key);
        Assert.Empty(this.browser.Listed);
    }

    [Fact]
    public async Task ABrowserThatWontAnswerIsReported()
    {
        this.browser.Failing(BrowserProblemKind.NotAllowed, "Not authorized to send Apple events");

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Brave, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(0, outcome.Closed);
        Assert.Equal("Switch_BrowserNotAllowed", outcome.Problem?.Key);
    }

    /// <summary>
    /// "Check Automation permissions" used to be the answer to every failure, and sent people to
    /// settings that were fine when the browser had only been slow. Each reason now says what it is.
    /// </summary>
    [Fact]
    public async Task ARefusedPermissionSaysWhereToTurnItOn()
    {
        this.browser.Failing(BrowserProblemKind.NotAllowed, "execution error: Not authorized to send Apple events to Google Chrome. (-1743)");

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Chrome, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Contains("System Settings → Privacy & Security → Automation", outcome.Problem?.English, StringComparison.Ordinal);
        Assert.Contains("Google Chrome", outcome.Problem?.English, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASlowBrowserIsNotBlamedOnPermissions()
    {
        this.browser.Failing(BrowserProblemKind.NotAnswering);

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Chrome, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal("Switch_BrowserNotAnswering", outcome.Problem?.Key);
        Assert.DoesNotContain("Automation", outcome.Problem?.English, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnyOtherFailureQuotesItsReason()
    {
        this.browser.Failing(BrowserProblemKind.Failed, "execution error: Google Chrome got an error: Can't get window 1. (-1728)\nsecond line");

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Chrome, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal("Could not control Google Chrome: execution error: Google Chrome got an error: Can't get window 1. (-1728)", outcome.Problem?.English);
    }

    /// <summary>A failure without a word has nothing to quote, so the general message stands.</summary>
    [Fact]
    public async Task AFailureWithNoReasonKeepsTheGeneralMessage()
    {
        this.browser.Failing(BrowserProblemKind.Failed);

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Chrome, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal("Switch_CouldNotCloseTabs", outcome.Problem?.Key);
    }

    /// <summary>The same goes for the check for tabs already open, when a profile is entered.</summary>
    [Fact]
    public async Task ARefusedPermissionIsSaidWhenCheckingForOpenTabsToo()
    {
        this.browser.Failing(BrowserProblemKind.NotAllowed, "Not authorized to send Apple events to Google Chrome. (-1743)");

        BrowserLaunchOutcome outcome = await this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Contains(outcome.Warnings, warning => warning.Key == "Switch_BrowserNotAllowed");
        Assert.Single(this.browser.Opened);
    }

    /// <summary>
    /// A tab that stays open - it asked to confirm leaving the page, say - used to go unreported: the
    /// switch said nothing and the page was still there. It now says how many, and which.
    /// </summary>
    [Fact]
    public async Task ATabThatStayedOpenIsNamed()
    {
        this.browser
            .WithTabs(new BrowserTab(11, "https://linear.app/team"), new BrowserTab(12, "https://www.github.com/pulls"), new BrowserTab(13, "https://example.com/"))
            .WithTabs(new BrowserTab(12, "https://www.github.com/pulls"), new BrowserTab(13, "https://example.com/"));
        this.browser.Closing = new TabsClosed(1, null);

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(
            BrowserKind.Chrome, ["https://linear.app/", "https://github.com/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(1, outcome.Closed);
        Assert.Equal("Closed 1 of 2 tabs in Google Chrome; still open: github.com.", outcome.Problem?.English);
    }

    [Fact]
    public async Task EveryTabClosedIsNotAProblem()
    {
        this.browser.WithTabs(new BrowserTab(11, "https://linear.app/team"), new BrowserTab(12, "https://github.com/pulls"));

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(
            BrowserKind.Chrome, ["https://linear.app/", "https://github.com/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(2, outcome.Closed);
        Assert.Null(outcome.Problem);
        Assert.Single(this.browser.Listed);
    }

    /// <summary>When the look at what is left can't be made, the count still says some stayed open.</summary>
    [Fact]
    public async Task ATabThatStayedOpenIsCountedWhenWhichCannotBeSaid()
    {
        this.browser
            .WithTabs(new BrowserTab(11, "https://linear.app/team"), new BrowserTab(12, "https://github.com/pulls"))
            .Failing(BrowserProblemKind.Failed, "AppleEvent timed out");
        this.browser.Closing = new TabsClosed(1, null);

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(
            BrowserKind.Chrome, ["https://linear.app/", "https://github.com/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal("Closed 1 of 2 tabs in Google Chrome.", outcome.Problem?.English);
    }

    /// <summary>A browser that closed the tabs without saying how many is taken at its word: nothing to report.</summary>
    [Fact]
    public async Task ACloseThatGivesNoCountIsNotAProblem()
    {
        this.browser.WithTabs(new BrowserTab(11, "https://linear.app/team"));
        this.browser.Closing = new TabsClosed(null, null);

        TabClosingOutcome outcome = await this.Create().CloseTabsAsync(BrowserKind.Chrome, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(0, null), outcome);
    }

    [Theory]
    [InlineData("https://linear.app/team/x?token=abc#frag", "linear.app")]
    [InlineData("https://www.github.com/pulls", "github.com")]
    [InlineData("not an address", "not an address")]
    public void APageIsNamedByItsSite(string address, string expected)
    {
        Assert.Equal(expected, BrowserLauncher.DescribePages([address]));
    }

    [Fact]
    public void MorePagesThanThreeAreCounted()
    {
        string line = BrowserLauncher.DescribePages(["https://a.com/", "https://b.com/", "https://a.com/again", "https://c.com/", "https://d.com/", "https://e.com/"]);

        Assert.Equal("a.com, b.com, c.com, +2", line);
    }

    /// <summary>No wait between looks for the tabs just opened, so the tests don't sit through the real one.</summary>
    private BrowserLauncher Create() => new(this.browser, this.store, this.runs, this.logger, TabTimings.Immediate);
}
