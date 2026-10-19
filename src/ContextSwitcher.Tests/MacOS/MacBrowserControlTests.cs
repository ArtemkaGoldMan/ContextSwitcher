using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>How the Mac's browsers are asked: AppleScript for their tabs, <c>open</c> for pages and profiles.</summary>
public sealed class MacBrowserControlTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly FakeProcessRunner processRunner = new();
    private readonly FakeScriptRunner scriptRunner = new();

    [Fact]
    public async Task APageOpensInTheDefaultBrowserWithPlainOpen()
    {
        BrowserProblem? problem = await this.Create().OpenPageAsync(BrowserKind.Default, "https://example.com/", Timeout, CancellationToken.None);

        Assert.Null(problem);
        ProcessStartOptions call = Assert.Single(this.processRunner.Calls);
        Assert.Equal("open", call.FileName);
        Assert.Equal(["https://example.com/"], call.Arguments);
    }

    [Fact]
    public async Task APageOpensInANamedBrowserByItsApp()
    {
        await this.Create().OpenPageAsync(BrowserKind.Chrome, "https://example.com/", Timeout, CancellationToken.None);

        Assert.Equal(["-a", "Google Chrome", "https://example.com/"], Assert.Single(this.processRunner.Calls).Arguments);
    }

    [Fact]
    public async Task APageThatDoesNotOpenSaysWhy()
    {
        this.processRunner.Enqueue(new ProcessResult(1, string.Empty, "no such app\n", false));

        BrowserProblem? problem = await this.Create().OpenPageAsync(BrowserKind.Default, "https://example.com/", Timeout, CancellationToken.None);

        Assert.Equal(new BrowserProblem(BrowserProblemKind.Failed, "no such app"), problem);
    }

    [Fact]
    public async Task AProfileOpensInANewInstanceWithItsPages()
    {
        MacBrowserControl browsers = this.Create();

        await browsers.OpenProfileAsync(BrowserKind.Chrome, "Profile 1", ["https://mail.example/"], Timeout, CancellationToken.None);
        await browsers.OpenProfileAsync(BrowserKind.Brave, "Default", [], Timeout, CancellationToken.None);

        Assert.Equal(["-na", "Google Chrome", "--args", "--profile-directory=Profile 1", "https://mail.example/"], this.processRunner.Calls[0].Arguments);
        Assert.Equal(["-na", "Brave Browser", "--args", "--profile-directory=Default"], this.processRunner.Calls[1].Arguments);
    }

    [Fact]
    public async Task ChromesTabsAreListedWithTheirIds()
    {
        this.scriptRunner.Enqueue(new ProcessResult(0, ChromeTabs((4, "https://mail.google.com/mail/u/0/#inbox"), (7, "https://linear.app/")), string.Empty, false));

        TabListing listing = await this.Create().ListTabsAsync(BrowserKind.Chrome, startIfNeeded: true, Timeout, CancellationToken.None);

        Assert.Equal([new BrowserTab(4, "https://mail.google.com/mail/u/0/#inbox"), new BrowserTab(7, "https://linear.app/")], listing.Tabs);
        Assert.Null(listing.Problem);
    }

    [Fact]
    public async Task SafarisTabsHaveNoIds()
    {
        char separator = AppleScriptBuilder.OpenTabFieldSeparator;
        this.scriptRunner.Enqueue(new ProcessResult(0, $"https://linear.app/team/issues{separator}Issues\nhttps://news.example/{separator}News\n", string.Empty, false));

        TabListing listing = await this.Create().ListTabsAsync(BrowserKind.Safari, startIfNeeded: false, Timeout, CancellationToken.None);

        Assert.Equal([new BrowserTab(null, "https://linear.app/team/issues"), new BrowserTab(null, "https://news.example/")], listing.Tabs);
    }

    /// <summary>Closing tabs never starts a browser that isn't running just to find it has none.</summary>
    [Fact]
    public async Task ABrowserThatIsNotRunningIsNotStartedToBeAsked()
    {
        await this.Create().ListTabsAsync(BrowserKind.Chrome, startIfNeeded: false, Timeout, CancellationToken.None);

        Assert.Contains("is not running then return \"\"", Assert.Single(this.scriptRunner.Scripts), StringComparison.Ordinal);
    }

    /// <summary>
    /// Chrome closes by id, so a tab whose address changes between being listed and being closed - a
    /// page still redirecting, an app rewriting its URL - is still closed.
    /// </summary>
    [Fact]
    public async Task ChromesTabsCloseByTheirIds()
    {
        this.scriptRunner.Enqueue(new ProcessResult(0, "2", string.Empty, false));

        TabsClosed closed = await this.Create().CloseTabsAsync(
            BrowserKind.Chrome, [new BrowserTab(4, "https://a.example/"), new BrowserTab(5, "https://b.example/")], Timeout, CancellationToken.None);

        Assert.Equal(new TabsClosed(2, null), closed);
        Assert.Contains("set doomed to {\"4\", \"5\"}", Assert.Single(this.scriptRunner.Scripts), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SafarisTabsCloseByTheirAddress()
    {
        this.scriptRunner.Enqueue(new ProcessResult(0, "1", string.Empty, false));

        await this.Create().CloseTabsAsync(BrowserKind.Safari, [new BrowserTab(null, "https://linear.app/team/issues")], Timeout, CancellationToken.None);

        Assert.Contains("set doomed to {\"https://linear.app/team/issues\"}", Assert.Single(this.scriptRunner.Scripts), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACloseThatSaysNoCountHasNone()
    {
        this.scriptRunner.Enqueue(new ProcessResult(0, string.Empty, string.Empty, false));

        TabsClosed closed = await this.Create().CloseTabsAsync(BrowserKind.Chrome, [new BrowserTab(4, "https://a.example/")], Timeout, CancellationToken.None);

        Assert.Equal(new TabsClosed(null, null), closed);
    }

    [Fact]
    public async Task FocusingATabNamesItsAddress()
    {
        await this.Create().FocusTabAsync(BrowserKind.Chrome, "https://mail.google.com/mail/u/0/#inbox", Timeout, CancellationToken.None);

        string script = Assert.Single(this.scriptRunner.Scripts);
        Assert.Contains("Google Chrome", script, StringComparison.Ordinal);
        Assert.Contains("https://mail.google.com/mail/u/0/#inbox", script, StringComparison.Ordinal);
    }

    /// <summary>What the script said decides what the person is told: wait, allow it, or what went wrong.</summary>
    [Theory]
    [InlineData(1, "execution error: Not authorized to send Apple events to Google Chrome. (-1743)", false, BrowserProblemKind.NotAllowed)]
    [InlineData(1, "Not authorized to send Apple events", false, BrowserProblemKind.NotAllowed)]
    [InlineData(-1, "", true, BrowserProblemKind.NotAnswering)]
    [InlineData(1, "execution error: Google Chrome got an error: Can't get window 1. (-1728)", false, BrowserProblemKind.Failed)]
    public async Task AFailureSaysWhatKindItWas(int exitCode, string error, bool timedOut, BrowserProblemKind expected)
    {
        this.scriptRunner.Enqueue(new ProcessResult(exitCode, string.Empty, error, timedOut));

        TabListing listing = await this.Create().ListTabsAsync(BrowserKind.Chrome, startIfNeeded: false, Timeout, CancellationToken.None);

        Assert.Null(listing.Tabs);
        Assert.Equal(new BrowserProblem(expected, error), listing.Problem);
    }

    /// <summary>A Chromium tab listing as <see cref="AppleScriptBuilder.ListChromiumTabs"/> prints it.</summary>
    private static string ChromeTabs(params (int Id, string Url)[] tabs) =>
        string.Concat(tabs.Select(tab => $"{tab.Id}{AppleScriptBuilder.OpenTabFieldSeparator}{tab.Url}\n"));

    private MacBrowserControl Create() => new(this.processRunner, this.scriptRunner);
}
