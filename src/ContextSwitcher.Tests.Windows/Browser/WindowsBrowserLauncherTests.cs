using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Windows.Browser;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows.Browser;

/// <summary>A profile's pages, as the shared launcher sends them to Windows' browser control.</summary>
public sealed class WindowsBrowserLauncherTests
{
    private readonly FakeShell shell = new();
    private readonly TestLogger logger = new();

    /// <summary>The default browser can't be asked for its tabs, so every page is simply opened, in order, even when asked to avoid duplicates.</summary>
    [Fact]
    public async Task EveryPageOpensInOrderInTheDefaultBrowser()
    {
        BrowserLaunchOutcome outcome = await this.Launch(avoidDuplicates: true, "https://github.com/", "https://linear.app/");

        Assert.Equal(["https://github.com/", "https://linear.app/"], this.shell.Links);
        Assert.Empty(outcome.Warnings);
    }

    /// <summary>A page that won't open is a warning for the person; the others still open.</summary>
    [Fact]
    public async Task APageThatIsNotAWebAddressIsWarnedAboutAndTheRestOpen()
    {
        BrowserLaunchOutcome outcome = await this.Launch(avoidDuplicates: false, @"C:\Windows\System32\calc.exe", "https://github.com/");

        Assert.Equal(["https://github.com/"], this.shell.Links);
        Assert.Single(outcome.Warnings);
    }

    /// <summary>Closing is not there yet: nothing is closed, and the person is told why.</summary>
    [Fact]
    public async Task ClosingTabsClosesNothingAndSaysSo()
    {
        BrowserLauncher launcher = this.Create();

        TabClosingOutcome outcome = await launcher.CloseTabsAsync(
            BrowserKind.Default, ["https://github.com/"], [], "work", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(0, outcome.Closed);
        Assert.NotNull(outcome.Problem);
    }

    private Task<BrowserLaunchOutcome> Launch(bool avoidDuplicates, params string[] urls) =>
        this.Create().ManageBrowserContextAsync(
            new BrowserContextRequest(BrowserManagementMode.Urls, BrowserKind.Default, urls, avoidDuplicates, [], TimeSpan.FromSeconds(5), "work"),
            CancellationToken.None);

    private BrowserLauncher Create()
    {
        WindowsBrowserControl browsers = new(this.shell);
        return new BrowserLauncher(browsers, new FakeOpenedTabsStore(), browsers, this.logger, TabTimings.Immediate);
    }
}
