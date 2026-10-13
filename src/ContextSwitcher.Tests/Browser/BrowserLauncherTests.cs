using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Browser;

public sealed class BrowserLauncherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeUsesPlainOpenForDefaultBrowser()
    {
        FakeProcessRunner processRunner = new();
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], false, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal("open", call.FileName);
        Assert.Equal(["https://example.com/"], call.Arguments);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeTargetsNamedBrowser()
    {
        FakeProcessRunner processRunner = new();
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], false, [], Timeout),
            CancellationToken.None);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-a", "Google Chrome", "https://example.com/"], call.Arguments);
    }

    /// <summary>
    /// The tab listing is one script for the whole context, so a URL that is already open costs
    /// that listing plus a single focus - never an `open`, and never a sweep per URL.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeSkipsOpenWhenExistingTabFocused()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "https://other.example/\nhttps://example.com/\n", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Empty(processRunner.Calls);
        Assert.Equal(2, scriptRunner.Scripts.Count);
    }

    /// <summary>
    /// Three URLs that are all already open still cost exactly one listing plus one focus, rather
    /// than growing a tab sweep at a time the way the per-URL probe did.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeChecksExistingTabsWithASingleListing()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(
            0, ChromeTabs((1, "https://a.example/"), (2, "https://b.example/"), (3, "https://c.example/")), string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls,
                BrowserKind.Chrome,
                ["https://a.example/", "https://b.example/", "https://c.example/"],
                true, [], Timeout),
            CancellationToken.None);

        Assert.Empty(processRunner.Calls);
        Assert.Equal(2, scriptRunner.Scripts.Count);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeOpensWhenNoExistingTabFound()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "https://unrelated.example/\n", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Single(processRunner.Calls);
    }

    /// <summary>
    /// The regression behind the "no URL opened at all" bug: a browser that does not answer
    /// AppleEvents fails the duplicate-tab probe, and section 9.3 requires falling back to a plain
    /// <c>open</c> rather than giving up on the URL.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeStillOpensWhenTabInspectionFails()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(-1, string.Empty, string.Empty, TimedOut: true));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-a", "Google Chrome", "https://example.com/"], call.Arguments);
        Assert.Contains(outcome.Warnings, warning => warning.English.Contains("Google Chrome", StringComparison.Ordinal));
    }

    /// <summary>
    /// Probing is best-effort, so it may never spend the whole step budget - that is what starved
    /// the <c>open</c> fallback when a browser hung.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeGivesTabInspectionOnlyASliceOfTheStepTimeout()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "false", string.Empty, false));

        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.True(Assert.Single(scriptRunner.Timeouts) < Timeout);
    }

    /// <summary>
    /// Once inspection has failed once the browser is not scriptable, so re-probing every remaining
    /// URL would only burn the step's budget to reach the same answer.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeStopsProbingAfterInspectionFailsOnce()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(-1, string.Empty, string.Empty, TimedOut: true));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls,
                BrowserKind.Chrome,
                ["https://example.com/", "https://example.org/", "https://example.net/"],
                true,
                [],
                Timeout),
            CancellationToken.None);

        Assert.Single(scriptRunner.Scripts);
        Assert.Equal(3, processRunner.Calls.Count);
        Assert.Single(outcome.Warnings);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncUrlsModeReportsWarningOnNonZeroExit()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "no such app", false));
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], false, [], Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
    }

    /// <summary>
    /// Tab groups were dropped - no browser lets another app open one. A request still saying
    /// "groups" (the plan never makes one now) touches nothing rather than trying.
    /// </summary>
    [Fact]
    public async Task ManageBrowserContextAsyncLegacyGroupsModeDoesNothing()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Safari, ["https://fallback.example/"], false, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Empty(processRunner.Calls);
        Assert.Empty(scriptRunner.Scripts);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncProfilesModeLaunchesEachProfileWithProfileDirectoryArgument()
    {
        FakeProcessRunner processRunner = new();
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserProfileConfig[] profiles =
        [
            new() { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1", Urls = ["https://mail.example/"] },
            new() { Browser = BrowserKind.Brave, ProfileDirectory = "Default", Urls = [] }
        ];

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Profiles, BrowserKind.Default, [], false, profiles, Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Equal(2, processRunner.Calls.Count);

        ProcessStartOptions chromeCall = processRunner.Calls[0];
        Assert.Equal("open", chromeCall.FileName);
        Assert.Equal(["-na", "Google Chrome", "--args", "--profile-directory=Profile 1", "https://mail.example/"], chromeCall.Arguments);

        ProcessStartOptions braveCall = processRunner.Calls[1];
        Assert.Equal(["-na", "Brave Browser", "--args", "--profile-directory=Default"], braveCall.Arguments);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncProfilesModeReportsWarningOnFailure()
    {
        FakeProcessRunner processRunner = new();
        processRunner.Enqueue(new ProcessResult(1, string.Empty, "profile locked", false));
        BrowserLauncher launcher = new(processRunner, new FakeScriptRunner());

        BrowserProfileConfig[] profiles = [new() { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1" }];

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
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
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((4, "https://mail.google.com/mail/u/0/#inbox")), string.Empty, false));
        BrowserLauncher launcher = new(processRunner, scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://mail.google.com/"], true, [], Timeout),
            CancellationToken.None);

        Assert.Empty(processRunner.Calls);
        Assert.Contains("https://mail.google.com/mail/u/0/#inbox", scriptRunner.Scripts[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Only the profile's pages close - in Chrome each tab by its id, so one whose address changes
    /// between being listed and being closed still closes - and a page the next profile opens stays.
    /// </summary>
    [Fact]
    public async Task ClosingTabsClosesTheProfilesPagesAndNothingElse()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(
            0,
            ChromeTabs((11, "https://linear.app/team/issues"), (12, "https://news.example/"), (13, "https://mail.google.com/mail/u/0/")),
            string.Empty,
            false));
        scriptRunner.Enqueue(new ProcessResult(0, "1", string.Empty, false));
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        TabClosingOutcome outcome = await launcher.CloseTabsAsync(
            BrowserKind.Chrome, ["https://linear.app/", "https://mail.google.com/"], ["https://mail.google.com/"], null, Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(1, null), outcome);
        Assert.Equal(2, scriptRunner.Scripts.Count);
        Assert.Contains("is not running then return \"\"", scriptRunner.Scripts[0], StringComparison.Ordinal);
        Assert.Contains("set doomed to {\"11\"}", scriptRunner.Scripts[1], StringComparison.Ordinal);
    }

    /// <summary>Safari's tabs have no id, so they close by the address Safari reported.</summary>
    [Fact]
    public async Task SafariTabsCloseByTheirAddress()
    {
        FakeScriptRunner scriptRunner = new();
        char separator = AppleScriptBuilder.OpenTabFieldSeparator;
        scriptRunner.Enqueue(new ProcessResult(
            0,
            $"https://linear.app/team/issues{separator}Issues\nhttps://news.example/{separator}News\n",
            string.Empty,
            false));
        scriptRunner.Enqueue(new ProcessResult(0, "1", string.Empty, false));
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        TabClosingOutcome outcome = await launcher.CloseTabsAsync(BrowserKind.Safari, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(1, null), outcome);
        Assert.Contains("set doomed to {\"https://linear.app/team/issues\"}", scriptRunner.Scripts[1], StringComparison.Ordinal);
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
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        FakeOpenedTabsStore store = new();
        BrowserLauncher launcher = new(processRunner, scriptRunner, store, new FakeBrowserRuns(), TimeSpan.Zero);

        // Entering: Gmail is already open; Fusion opens, and its new tab is 7.
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((3, "https://mail.google.com/mail/u/0/")), string.Empty, false));
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((3, "https://mail.google.com/mail/u/0/"), (7, "https://fusion.online.autodesk.com/")), string.Empty, false));
        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://mail.google.com/", "https://fusion.online.autodesk.com/"], true, [], Timeout,
                "work", ["https://fusion.online.autodesk.com/"]),
            CancellationToken.None);

        OpenedTabs remembered = store.Tabs["work"];
        Assert.Equal((BrowserKind.Chrome, "run-1"), (remembered.Browser, remembered.BrowserRun));
        Assert.Equal([7], remembered.TabIds);
        Assert.Single(processRunner.Calls);

        // Leaving: tab 7 has gone to Autodesk's sign-in, another site; a tab the user opened stays.
        scriptRunner.Enqueue(new ProcessResult(
            0,
            ChromeTabs((3, "https://mail.google.com/mail/u/0/"), (7, "https://signin.autodesk.com/idmgr/"), (9, "https://www.youtube.com/")),
            string.Empty,
            false));
        scriptRunner.Enqueue(new ProcessResult(0, "1", string.Empty, false));
        TabClosingOutcome outcome = await launcher.CloseTabsAsync(
            BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], [], "work", Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(1, null), outcome);
        Assert.Contains("set doomed to {\"7\"}", scriptRunner.Scripts[^1], StringComparison.Ordinal);
        Assert.Empty(store.Tabs);
    }

    /// <summary>
    /// Only pages marked Close are remembered; one that only opens is left to the user. Tabs appear
    /// in the order they were opened, each numbered above the last.
    /// </summary>
    [Fact]
    public async Task OnlyTabsOpenedForPagesThatCloseAreRemembered()
    {
        FakeScriptRunner scriptRunner = new();
        FakeOpenedTabsStore store = new();
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner, store, new FakeBrowserRuns(), TimeSpan.Zero);
        scriptRunner.Enqueue(new ProcessResult(0, string.Empty, string.Empty, false));
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((20, "https://docs.example/"), (21, "https://fusion.online.autodesk.com/")), string.Empty, false));

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://docs.example/", "https://fusion.online.autodesk.com/"], true, [], Timeout,
                "work", ["https://fusion.online.autodesk.com/"]),
            CancellationToken.None);

        Assert.Equal([21], store.Tabs["work"].TabIds);
    }

    /// <summary>
    /// Mixed in: a tab the user opened while the profile's pages were opening is never taken for
    /// the profile's. With more new tabs than pages, only one still showing a page counts.
    /// </summary>
    [Fact]
    public async Task ATabSomeoneElseOpenedMeanwhileIsNotRemembered()
    {
        FakeScriptRunner scriptRunner = new();
        FakeOpenedTabsStore store = new();
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner, store, new FakeBrowserRuns(), TimeSpan.Zero);
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((1, "https://news.example/")), string.Empty, false));
        scriptRunner.Enqueue(new ProcessResult(
            0,
            ChromeTabs((1, "https://news.example/"), (5, "https://www.youtube.com/"), (6, "https://fusion.online.autodesk.com/")),
            string.Empty,
            false));

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], true, [], Timeout,
                "work", ["https://fusion.online.autodesk.com/"]),
            CancellationToken.None);

        Assert.Equal([6], store.Tabs["work"].TabIds);
    }

    /// <summary>
    /// A browser numbers its tabs afresh each time it starts, so after a restart a remembered id may
    /// be any tab at all. Those are dropped, and only tabs showing the profile's pages close.
    /// </summary>
    [Fact]
    public async Task IdsFromAnEarlierRunOfTheBrowserAreNotUsed()
    {
        FakeScriptRunner scriptRunner = new();
        FakeOpenedTabsStore store = new();
        store.Tabs["work"] = new OpenedTabs(BrowserKind.Chrome, "run-1", [7]);
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner, store, new FakeBrowserRuns { Run = "run-2" }, TimeSpan.Zero);
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((7, "https://www.youtube.com/"), (8, "https://fusion.online.autodesk.com/home")), string.Empty, false));
        scriptRunner.Enqueue(new ProcessResult(0, "1", string.Empty, false));

        TabClosingOutcome outcome = await launcher.CloseTabsAsync(
            BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], [], "work", Timeout, CancellationToken.None);

        Assert.Equal(1, outcome.Closed);
        Assert.Contains("set doomed to {\"8\"}", scriptRunner.Scripts[^1], StringComparison.Ordinal);
        Assert.Empty(store.Tabs);
    }

    /// <summary>A remembered tab showing a page the next profile opens is left open, like any other such tab.</summary>
    [Fact]
    public async Task ARememberedTabTheNextProfileOpensStays()
    {
        FakeScriptRunner scriptRunner = new();
        FakeOpenedTabsStore store = new();
        store.Tabs["work"] = new OpenedTabs(BrowserKind.Chrome, "run-1", [7, 8]);
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner, store, new FakeBrowserRuns(), TimeSpan.Zero);
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((7, "https://calendar.google.com/r/week"), (8, "https://signin.autodesk.com/")), string.Empty, false));
        scriptRunner.Enqueue(new ProcessResult(0, "1", string.Empty, false));

        await launcher.CloseTabsAsync(
            BrowserKind.Chrome, ["https://fusion.online.autodesk.com/"], ["https://calendar.google.com/"], "work", Timeout, CancellationToken.None);

        Assert.Contains("set doomed to {\"8\"}", scriptRunner.Scripts[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing to close later means nothing to remember: no second listing, and the switch is no slower.
    /// </summary>
    [Fact]
    public async Task APageThatOnlyOpensCostsNoExtraListing()
    {
        FakeScriptRunner scriptRunner = new();
        FakeOpenedTabsStore store = new();
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner, store, new FakeBrowserRuns(), TimeSpan.Zero);
        scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((1, "https://news.example/")), string.Empty, false));

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://docs.example/"], true, [], Timeout, "work", []),
            CancellationToken.None);

        Assert.Single(scriptRunner.Scripts);
        Assert.Empty(store.Tabs);
    }

    [Fact]
    public async Task WithNoTabToCloseTheBrowserIsOnlyAsked()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "https://news.example/\n", string.Empty, false));
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        TabClosingOutcome outcome = await launcher.CloseTabsAsync(BrowserKind.Safari, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(new TabClosingOutcome(0, null), outcome);
        Assert.Single(scriptRunner.Scripts);
    }

    /// <summary>
    /// A default browser that can't be scripted - the plan leaves Default only for those - can't be
    /// asked for its tabs: nothing is tried, and the switch says why.
    /// </summary>
    [Fact]
    public async Task AnUnscriptableDefaultBrowserIsReportedAndLeftAlone()
    {
        FakeScriptRunner scriptRunner = new();
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        TabClosingOutcome outcome = await launcher.CloseTabsAsync(BrowserKind.Default, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(0, outcome.Closed);
        Assert.Equal("Switch_CloseTabsNeedsBrowser", outcome.Problem?.Key);
        Assert.Empty(scriptRunner.Scripts);
    }

    [Fact]
    public async Task ABrowserThatWontAnswerIsReported()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(1, string.Empty, "Not authorized to send Apple events", false));
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        TabClosingOutcome outcome = await launcher.CloseTabsAsync(BrowserKind.Brave, ["https://linear.app/"], [], null, Timeout, CancellationToken.None);

        Assert.Equal(0, outcome.Closed);
        Assert.Equal("Could not close the profile's tabs in Brave Browser. Check Automation permissions.", outcome.Problem?.English);
    }

    /// <summary>A Chromium tab listing as <see cref="AppleScriptBuilder.ListChromiumTabs"/> prints it.</summary>
    private static string ChromeTabs(params (int Id, string Url)[] tabs) =>
        string.Concat(tabs.Select(tab => $"{tab.Id}{AppleScriptBuilder.OpenTabFieldSeparator}{tab.Url}\n"));
}
