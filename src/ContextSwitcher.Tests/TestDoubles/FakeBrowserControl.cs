using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>
/// A browser that answers with the tab listings it was given, in turn - no tabs once they run out -
/// and records what it was asked. Nothing on this Mac is asked.
/// </summary>
public sealed class FakeBrowserControl : IBrowserControl
{
    private readonly Queue<TabListing> listings = new();

    /// <summary>Each listing asked for: of which browser, whether it could be started, and in how long.</summary>
    public List<(BrowserKind Browser, bool StartIfNeeded, TimeSpan Timeout)> Listed { get; } = [];

    public List<string> Focused { get; } = [];

    /// <summary>The tabs of each close asked for.</summary>
    public List<IReadOnlyList<BrowserTab>> Closed { get; } = [];

    public List<(BrowserKind Browser, string Url)> Opened { get; } = [];

    public List<(BrowserKind Browser, string ProfileDirectory, IReadOnlyList<string> Urls)> OpenedProfiles { get; } = [];

    /// <summary>What closing answers; by default, that every tab asked to close did.</summary>
    public TabsClosed? Closing { get; set; }

    /// <summary>Why a page or profile doesn't open; by default, each does.</summary>
    public BrowserProblem? OpenProblem { get; set; }

    /// <summary>The browser's tabs at the next look: Chrome's with an id, Safari's with none.</summary>
    public FakeBrowserControl WithTabs(params BrowserTab[] tabs)
    {
        this.listings.Enqueue(new TabListing(tabs, null));
        return this;
    }

    /// <summary>The next look fails, for this reason.</summary>
    public FakeBrowserControl Failing(BrowserProblemKind kind, string reason = "")
    {
        this.listings.Enqueue(new TabListing(null, new BrowserProblem(kind, reason)));
        return this;
    }

    public string AppName(BrowserKind browser) => MacBrowserApps.Name(browser);

    public Task<TabListing> ListTabsAsync(BrowserKind browser, bool startIfNeeded, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this.Listed.Add((browser, startIfNeeded, timeout));
        return Task.FromResult(this.listings.Count > 0 ? this.listings.Dequeue() : new TabListing([], null));
    }

    public Task FocusTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this.Focused.Add(url);
        return Task.CompletedTask;
    }

    public Task<TabsClosed> CloseTabsAsync(BrowserKind browser, IReadOnlyList<BrowserTab> tabs, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this.Closed.Add(tabs);
        return Task.FromResult(this.Closing ?? new TabsClosed(tabs.Count, null));
    }

    public Task<BrowserProblem?> OpenPageAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this.Opened.Add((browser, url));
        return Task.FromResult(this.OpenProblem);
    }

    public Task<BrowserProblem?> OpenProfileAsync(
        BrowserKind browser, string profileDirectory, IReadOnlyList<string> urls, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this.OpenedProfiles.Add((browser, profileDirectory, urls));
        return Task.FromResult(this.OpenProblem);
    }
}
