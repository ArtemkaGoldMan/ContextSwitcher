using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// What a browser is asked: its tabs, to bring one forward, to close some, to open a page or one of
/// its profiles. Which tabs and pages is decided by the caller; this only asks.
/// </summary>
public interface IBrowserControl
{
    /// <summary>The name of <paramref name="browser"/>'s app, as the system shows it - for messages.</summary>
    string AppName(BrowserKind browser);

    /// <summary>
    /// <paramref name="browser"/>'s tabs. Chrome and Brave give each tab's id as well as its address;
    /// Safari's tabs have no id. Without <paramref name="startIfNeeded"/>, a browser that isn't
    /// running has no tabs, rather than being started to say so.
    /// </summary>
    Task<TabListing> ListTabsAsync(BrowserKind browser, bool startIfNeeded, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Brings forward the tab showing <paramref name="url"/>, if there is one. A browser that can't be
    /// asked is left as it is: this is a nicety, and nothing waits on it.
    /// </summary>
    Task FocusTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Closes <paramref name="tabs"/>: by id where they have one, so a tab whose address changed since
    /// it was listed still closes, and otherwise by address.
    /// </summary>
    Task<TabsClosed> CloseTabsAsync(BrowserKind browser, IReadOnlyList<BrowserTab> tabs, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Opens <paramref name="url"/> in <paramref name="browser"/> - in the system's default browser for
    /// <see cref="BrowserKind.Default"/>. Returns why it didn't open, or null when it did.
    /// </summary>
    Task<BrowserProblem?> OpenPageAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a new window of <paramref name="browser"/>'s profile in <paramref name="profileDirectory"/>,
    /// showing <paramref name="urls"/>. Returns why it didn't open, or null when it did.
    /// </summary>
    Task<BrowserProblem?> OpenProfileAsync(
        BrowserKind browser, string profileDirectory, IReadOnlyList<string> urls, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>A tab: its id where the browser gives one (Chrome, Brave), and its address.</summary>
public sealed record BrowserTab(int? Id, string Url);

/// <summary>A browser's tabs, or - when it couldn't be asked - why not.</summary>
public sealed record TabListing(IReadOnlyList<BrowserTab>? Tabs, BrowserProblem? Problem);

/// <summary>How many tabs closed - null when the browser didn't say - or why none could be.</summary>
public sealed record TabsClosed(int? Count, BrowserProblem? Problem);

/// <summary>Why a browser didn't do what it was asked.</summary>
/// <param name="Kind">What kind of failure it was, as far as can be told.</param>
/// <param name="Reason">What the system said; empty when it said nothing.</param>
public sealed record BrowserProblem(BrowserProblemKind Kind, string Reason);

/// <summary>What kind of failure a browser's was.</summary>
public enum BrowserProblemKind
{
    /// <summary>It didn't answer in time.</summary>
    NotAnswering,

    /// <summary>The app isn't allowed to control it: the person turned it off, or refused when asked.</summary>
    NotAllowed,

    /// <summary>Anything else; the reason says what.</summary>
    Failed
}
