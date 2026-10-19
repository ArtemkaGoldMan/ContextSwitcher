using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// What a switch does in the browser: opens the pages or profiles of the profile being entered, and
/// closes the tabs of the one being left.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>Manages the browser for a switch according to <paramref name="request"/>'s mode.</summary>
    Task<BrowserLaunchOutcome> ManageBrowserContextAsync(BrowserContextRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the tabs in <paramref name="browser"/> that show any of <paramref name="pages"/> - and,
    /// in Chrome and Brave, the tabs <paramref name="contextId"/> opened for them, wherever they have
    /// gone since - except those showing one of <paramref name="keep"/>: the pages the profile being
    /// entered is about to open, which would only be closed to be loaded again. No other tab is
    /// touched, and a browser that isn't running is not started.
    /// </summary>
    Task<TabClosingOutcome> CloseTabsAsync(
        BrowserKind browser,
        IReadOnlyList<string> pages,
        IReadOnlyList<string> keep,
        string? contextId,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
