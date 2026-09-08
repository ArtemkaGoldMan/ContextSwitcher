using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
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
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], [], false, [], Timeout),
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
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], [], false, [], Timeout),
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
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], [], true, [], Timeout),
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
            0, "https://a.example/\nhttps://b.example/\nhttps://c.example/\n", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(
                BrowserManagementMode.Urls,
                BrowserKind.Chrome,
                ["https://a.example/", "https://b.example/", "https://c.example/"],
                [], true, [], Timeout),
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
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Safari, ["https://example.com/"], [], true, [], Timeout),
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
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], [], true, [], Timeout),
            CancellationToken.None);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-a", "Google Chrome", "https://example.com/"], call.Arguments);
        Assert.Contains(outcome.Warnings, warning => warning.Contains("Google Chrome", StringComparison.Ordinal));
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
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://example.com/"], [], true, [], Timeout),
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
                [],
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
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, ["https://example.com/"], [], false, [], Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncGroupsModeSucceedsWithoutFallbackWhenGroupActivated()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Safari, ["https://fallback.example/"], ["Work-Core"], false, [], Timeout),
            CancellationToken.None);

        Assert.Empty(outcome.Warnings);
        Assert.Empty(processRunner.Calls);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncGroupsModeFallsBackToUrlsWhenGroupActivationFails()
    {
        FakeProcessRunner processRunner = new();
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "false", string.Empty, false));

        BrowserLauncher launcher = new(processRunner, scriptRunner);

        BrowserLaunchOutcome outcome = await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Chrome, ["https://fallback.example/"], ["Work-Core"], false, [], Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal(["-a", "Google Chrome", "https://fallback.example/"], call.Arguments);
    }

    [Fact]
    public async Task ManageBrowserContextAsyncGroupsModeUsesChromiumScriptForBrave()
    {
        FakeScriptRunner scriptRunner = new();
        scriptRunner.Enqueue(new ProcessResult(0, "true", string.Empty, false));
        BrowserLauncher launcher = new(new FakeProcessRunner(), scriptRunner);

        await launcher.ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Groups, BrowserKind.Brave, [], ["Personal"], false, [], Timeout),
            CancellationToken.None);

        Assert.Contains("Brave Browser", Assert.Single(scriptRunner.Scripts));
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
            new BrowserContextRequest(BrowserManagementMode.Profiles, BrowserKind.Default, [], [], false, profiles, Timeout),
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
            new BrowserContextRequest(BrowserManagementMode.Profiles, BrowserKind.Default, [], [], false, profiles, Timeout),
            CancellationToken.None);

        Assert.Single(outcome.Warnings);
    }
}
