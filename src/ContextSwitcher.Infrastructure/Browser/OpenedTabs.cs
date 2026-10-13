using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// The tabs a profile opened, in Chrome or Brave, for pages it closes when it is left - by the
/// browser's own tab id, which a tab keeps wherever it goes. A page often moves on from the address
/// it was opened at to another site entirely (a sign-in page, the app's own domain), and then only
/// its id still says it is the profile's.
/// </summary>
/// <param name="Browser">Chrome or Brave.</param>
/// <param name="BrowserRun">
/// Which run of the browser the ids belong to (<see cref="IBrowserRuns"/>). A browser numbers its
/// tabs afresh each time it starts, so after a restart the same id can be some other tab.
/// </param>
/// <param name="TabIds">The tabs' ids.</param>
public sealed record OpenedTabs(BrowserKind Browser, string BrowserRun, IReadOnlyList<int> TabIds);

/// <summary>Remembers <see cref="OpenedTabs"/> per profile from entering it to leaving it.</summary>
public interface IOpenedTabsStore
{
    /// <summary>What <paramref name="contextId"/> opened, or <see langword="null"/>.</summary>
    Task<OpenedTabs?> GetAsync(string contextId, CancellationToken cancellationToken);

    /// <summary>Replaces what <paramref name="contextId"/> opened.</summary>
    Task SetAsync(string contextId, OpenedTabs tabs, CancellationToken cancellationToken);

    /// <summary>Forgets what <paramref name="contextId"/> opened.</summary>
    Task RemoveAsync(string contextId, CancellationToken cancellationToken);
}

/// <summary>
/// Identifies the running copy of a browser, so tab ids from an earlier run are never taken for
/// this one's.
/// </summary>
public interface IBrowserRuns
{
    /// <summary>The current run of <paramref name="browser"/>, or <see langword="null"/> when it isn't running or can't be told apart.</summary>
    string? Current(BrowserKind browser);
}
