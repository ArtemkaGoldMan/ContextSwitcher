using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Browser;

namespace ContextSwitcher.Infrastructure.Windows.Browser;

/// <summary>
/// What a browser can be asked on Windows so far: to open a page, in the default browser, which shows
/// it as a new tab of the window already open. Reading, focusing and closing tabs, and opening a
/// browser's own profile, need a browser extension (<c>docs/windows-plan.md</c>), so a browser asked
/// for them says that it can't - and the platform's capabilities name no browser but the default one,
/// so Profile Setup doesn't offer them.
/// </summary>
public sealed class WindowsBrowserControl : IBrowserControl, IBrowserRuns
{
    private static readonly BrowserProblem NotHere = new(BrowserProblemKind.Failed, "Tabs can't be read or closed on Windows yet.");

    private readonly IShell shell;

    public WindowsBrowserControl(IShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        this.shell = shell;
    }

    /// <inheritdoc />
    public string AppName(BrowserKind browser) => KnownBrowsers.Name(browser);

    /// <inheritdoc />
    public Task<TabListing> ListTabsAsync(BrowserKind browser, bool startIfNeeded, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(new TabListing(null, NotHere));

    /// <inheritdoc />
    public Task FocusTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<TabsClosed> CloseTabsAsync(BrowserKind browser, IReadOnlyList<BrowserTab> tabs, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(new TabsClosed(null, NotHere));

    /// <inheritdoc />
    public async Task<BrowserProblem?> OpenPageAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (browser != BrowserKind.Default)
        {
            return new BrowserProblem(BrowserProblemKind.Failed, "Only the default browser can open pages on Windows yet.");
        }

        // File Explorer would run a program or open a folder given a path; a page is only ever a web address.
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? address) || address.Scheme is not ("http" or "https"))
        {
            return new BrowserProblem(BrowserProblemKind.Failed, "Only web addresses (http or https) can be opened.");
        }

        return await this.shell.OpenLinkAsync(address.AbsoluteUri, cancellationToken).ConfigureAwait(false)
            ? null
            : new BrowserProblem(BrowserProblemKind.NotAnswering, string.Empty);
    }

    /// <inheritdoc />
    public Task<BrowserProblem?> OpenProfileAsync(
        BrowserKind browser, string profileDirectory, IReadOnlyList<string> urls, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult<BrowserProblem?>(NotHere);

    /// <inheritdoc />
    public string? Current(BrowserKind browser) => null;
}
