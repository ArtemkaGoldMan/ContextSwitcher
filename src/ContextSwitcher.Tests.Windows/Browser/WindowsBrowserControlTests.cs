using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Windows.Browser;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows.Browser;

/// <summary>What a browser is asked on Windows so far: pages, in the default browser, and nothing about its tabs.</summary>
public sealed class WindowsBrowserControlTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task APageOpensInTheDefaultBrowser()
    {
        FakeShell shell = new();

        BrowserProblem? problem = await new WindowsBrowserControl(shell).OpenPageAsync(BrowserKind.Default, "https://github.com/", Timeout, CancellationToken.None);

        Assert.Null(problem);
        Assert.Equal(["https://github.com/"], shell.Links);
    }

    /// <summary>File Explorer runs a program or opens a folder when given a path, so only a web address goes to it.</summary>
    [Theory]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("github.com")]
    public async Task ThingsThatAreNotWebAddressesAreNotOpened(string url)
    {
        FakeShell shell = new();

        BrowserProblem? problem = await new WindowsBrowserControl(shell).OpenPageAsync(BrowserKind.Default, url, Timeout, CancellationToken.None);

        Assert.NotNull(problem);
        Assert.Empty(shell.Links);
    }

    [Fact]
    public async Task APageThatFoundNoAppToOpenItIsReported()
    {
        FakeShell shell = new() { LinksOpen = false };

        BrowserProblem? problem = await new WindowsBrowserControl(shell).OpenPageAsync(BrowserKind.Default, "https://github.com/", Timeout, CancellationToken.None);

        Assert.NotNull(problem);
    }

    /// <summary>No named browser is offered on Windows yet, so one that arrives from an imported file is not guessed at.</summary>
    [Fact]
    public async Task ANamedBrowserIsNotUsed()
    {
        FakeShell shell = new();

        BrowserProblem? problem = await new WindowsBrowserControl(shell).OpenPageAsync(BrowserKind.Chrome, "https://github.com/", Timeout, CancellationToken.None);

        Assert.NotNull(problem);
        Assert.Empty(shell.Links);
    }

    /// <summary>Tabs can't be read or closed without an extension: the answer says so, and nothing is closed.</summary>
    [Fact]
    public async Task TabsCanNeitherBeReadNorClosed()
    {
        WindowsBrowserControl browsers = new(new FakeShell());

        TabListing listing = await browsers.ListTabsAsync(BrowserKind.Default, startIfNeeded: false, Timeout, CancellationToken.None);
        TabsClosed closed = await browsers.CloseTabsAsync(BrowserKind.Default, [new BrowserTab(null, "https://github.com/")], Timeout, CancellationToken.None);

        Assert.Null(listing.Tabs);
        Assert.NotNull(listing.Problem);
        Assert.Null(closed.Count);
        Assert.NotNull(closed.Problem);
        Assert.Null(browsers.Current(BrowserKind.Default));
    }
}
