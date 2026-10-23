using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Browser;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// Browsers on Windows come in stage 2; until then a profile has no pages there, and a browser asked
/// all the same answers that it can't.
/// </summary>
public sealed class NotYetBrowserControl : IBrowserControl, IBrowserRuns
{
    private static readonly BrowserProblem NotYetHere = new(BrowserProblemKind.Failed, NotYetReason.Browser);

    /// <inheritdoc />
    public string AppName(BrowserKind browser) => KnownBrowsers.Name(browser);

    /// <inheritdoc />
    public Task<TabListing> ListTabsAsync(BrowserKind browser, bool startIfNeeded, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(new TabListing(null, NotYetHere));

    /// <inheritdoc />
    public Task FocusTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<TabsClosed> CloseTabsAsync(BrowserKind browser, IReadOnlyList<BrowserTab> tabs, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult(new TabsClosed(null, NotYetHere));

    /// <inheritdoc />
    public Task<BrowserProblem?> OpenPageAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult<BrowserProblem?>(NotYetHere);

    /// <inheritdoc />
    public Task<BrowserProblem?> OpenProfileAsync(
        BrowserKind browser, string profileDirectory, IReadOnlyList<string> urls, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult<BrowserProblem?>(NotYetHere);

    /// <inheritdoc />
    public string? Current(BrowserKind browser) => null;
}
