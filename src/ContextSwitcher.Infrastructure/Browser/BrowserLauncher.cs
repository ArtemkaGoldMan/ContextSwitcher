using System.Globalization;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Implements the browser management modes from agent.md section 9.3 and 9.5: opening URLs (with
/// best-effort duplicate-tab avoidance) and launching Chromium browser profiles - and closing the
/// tabs a profile being left lists to close. Tab groups are not among them: no browser lets
/// another app open one (docs/browser-control.md). It decides which tabs and pages; the browser is
/// asked through <see cref="IBrowserControl"/>.
/// </summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    /// <summary>
    /// Duplicate-tab inspection is a best-effort nicety, so it gets a small slice of the step's
    /// budget rather than all of it. A browser that answers AppleEvents at all answers in
    /// milliseconds; one that does not (no Automation permission, or a wedged process) blocks until
    /// the script is killed. Handing it the whole step timeout used to starve the plain
    /// <c>open</c> fallback that agent.md section 9.3 requires, so no URL opened at all.
    /// </summary>
    private static readonly TimeSpan TabProbeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How many times to look for the tabs just opened before settling for what has appeared.</summary>
    private const int OpenedTabLooks = 8;

    private readonly IBrowserControl browsers;
    private readonly IOpenedTabsStore openedTabs;
    private readonly IBrowserRuns browserRuns;
    private readonly ILogger logger;
    private readonly TimeSpan openedTabPollInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserLauncher"/> class.
    /// </summary>
    /// <param name="openedTabs">Where the tabs a profile opened are remembered.</param>
    /// <param name="browserRuns">Tells one run of Chrome or Brave from the next, so remembered tab ids are never another run's.</param>
    /// <param name="openedTabPollInterval">How long to wait between looks for the tabs just opened.</param>
    public BrowserLauncher(
        IBrowserControl browsers,
        IOpenedTabsStore openedTabs,
        IBrowserRuns browserRuns,
        ILogger logger,
        TimeSpan? openedTabPollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(browsers);
        ArgumentNullException.ThrowIfNull(openedTabs);
        ArgumentNullException.ThrowIfNull(browserRuns);
        ArgumentNullException.ThrowIfNull(logger);

        this.browsers = browsers;
        this.openedTabs = openedTabs;
        this.browserRuns = browserRuns;
        this.logger = logger;
        this.openedTabPollInterval = openedTabPollInterval ?? TimeSpan.FromMilliseconds(250);
    }

    /// <inheritdoc />
    public Task<BrowserLaunchOutcome> ManageBrowserContextAsync(BrowserContextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Mode switch
        {
            BrowserManagementMode.Urls => this.OpenUrlsAsync(request, cancellationToken),
            BrowserManagementMode.Profiles => this.LaunchProfilesAsync(request.Profiles, request.Timeout, cancellationToken),
            _ => Task.FromResult(BrowserLaunchOutcome.Empty)
        };
    }

    private async Task<BrowserLaunchOutcome> OpenUrlsAsync(BrowserContextRequest request, CancellationToken cancellationToken)
    {
        List<UserMessage> warnings = [];
        BrowserKind browser = request.Browser;
        TimeSpan timeout = request.Timeout;
        IReadOnlyList<string> closeLater = request.RememberUrls ?? [];

        // The default browser is unknown ahead of time, so it cannot be asked for its tabs.
        bool probeTabs = request.AvoidDuplicateTabs && browser != BrowserKind.Default;
        TimeSpan probeTimeout = timeout < TabProbeTimeout ? timeout : TabProbeTimeout;

        // A page that closes on leaving is remembered by the tab it opened in - see
        // RememberOpenedTabsAsync. The plan asks for it only in a browser whose tabs keep an id
        // wherever they navigate.
        bool remember = request.ContextId is not null
            && request.Urls.Any(url => closeLater.Contains(url, StringComparer.Ordinal));

        // One sweep of the browser's tabs for the whole context. This used to be a tab sweep per
        // URL, which is what made a three-URL context spend about 490ms deciding what to open even
        // when every tab was already there; a single listing costs about 130ms and does not grow
        // with the URL count.
        IReadOnlyList<BrowserTab> openTabs = [];
        if (probeTabs || remember)
        {
            // A browser that couldn't be asked - it timed out, Automation was refused, or it has no
            // tab scripting - is different from "no matching tab". Section 9.3 says to fall back to
            // a plain `open` for everything.
            TabListing listing = await this.browsers.ListTabsAsync(browser, startIfNeeded: true, probeTimeout, cancellationToken).ConfigureAwait(false);
            if (listing.Tabs is { } tabs)
            {
                openTabs = tabs;
            }
            else
            {
                remember = false;
                if (probeTabs)
                {
                    probeTabs = false;
                    warnings.Add(this.WhyNotAsked(browser, listing.Problem, SwitchMessages.CouldNotCheckTabs));
                }
            }
        }

        // The last configured URL that was already open with nothing opened after it. Focusing just
        // that one at the end leaves the browser where the old per-URL probe left it, which focused
        // every match in turn and so ended on the last action, without paying for the ones whose
        // focus a later `open` would immediately have replaced.
        string? tabToFocus = null;
        List<string> opened = [];

        foreach (string url in request.Urls)
        {
            // A tab that has moved on from the address it was opened at - redirected, or clicked
            // around in - is still that page; see PageMatch. Focusing goes by the tab's own address.
            if (probeTabs && openTabs.FirstOrDefault(tab => PageMatch.Matches(url, tab.Url)) is { } openTab)
            {
                tabToFocus = openTab.Url;
                continue;
            }

            BrowserProblem? problem = await this.browsers.OpenPageAsync(browser, url, timeout, cancellationToken).ConfigureAwait(false);
            tabToFocus = null;

            if (problem is not null)
            {
                warnings.Add(SwitchMessages.CouldNotOpenUrl.With(url, problem.Reason));
            }
            else
            {
                opened.Add(url);
            }
        }

        if (tabToFocus is not null)
        {
            await this.browsers.FocusTabAsync(browser, tabToFocus, probeTimeout, cancellationToken).ConfigureAwait(false);
        }

        if (remember)
        {
            await this.RememberOpenedTabsAsync(request.ContextId!, browser, openTabs, opened, closeLater, probeTimeout, cancellationToken)
                .ConfigureAwait(false);
        }

        return new BrowserLaunchOutcome(warnings);
    }

    /// <summary>
    /// Remembers which tabs were just opened for pages that close on leaving, by the ids the browser
    /// gives them. Pages move on from the address they were opened at - a sign-in page, the app's own
    /// domain - and a tab found by its address alone was then left open when the profile was left.
    ///
    /// Only tabs that weren't there a moment ago are candidates. When exactly as many appeared as
    /// pages were opened, they are those pages, in order: they were opened one after another and a
    /// browser numbers each new tab above the last. When the count differs - the browser restored a
    /// session, or something else opened a tab meanwhile - only a new tab that still shows one of the
    /// pages is taken to be it, so a tab opened by anyone else is never remembered.
    /// </summary>
    private async Task RememberOpenedTabsAsync(
        string contextId,
        BrowserKind browser,
        IReadOnlyList<BrowserTab> before,
        IReadOnlyList<string> opened,
        IReadOnlyList<string> closeLater,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        HashSet<int> known = before.Where(tab => tab.Id is not null).Select(tab => tab.Id!.Value).ToHashSet();
        List<BrowserTab> fresh = [];

        // `open` returns once the browser has the address; its tab appears a moment later.
        for (int look = 0; look < OpenedTabLooks && opened.Count > 0 && fresh.Count < opened.Count; look++)
        {
            await Task.Delay(this.openedTabPollInterval, cancellationToken).ConfigureAwait(false);
            TabListing listing = await this.browsers.ListTabsAsync(browser, startIfNeeded: false, timeout, cancellationToken).ConfigureAwait(false);
            if (listing.Tabs is not { } tabs)
            {
                return;
            }

            fresh = tabs.Where(tab => tab.Id is not null && !known.Contains(tab.Id.Value)).OrderBy(tab => tab.Id).ToList();
        }

        List<int> ids = [];
        if (fresh.Count == opened.Count)
        {
            for (int i = 0; i < opened.Count; i++)
            {
                if (closeLater.Contains(opened[i], StringComparer.Ordinal))
                {
                    ids.Add(fresh[i].Id!.Value);
                }
            }
        }
        else
        {
            ids.AddRange(fresh
                .Where(tab => opened.Any(page => closeLater.Contains(page, StringComparer.Ordinal) && PageMatch.Matches(page, tab.Url)))
                .Select(tab => tab.Id!.Value));
        }

        // Without knowing which run of the browser these ids are from, they can't be trusted later.
        string? run = this.browserRuns.Current(browser);
        try
        {
            if (ids.Count > 0 && run is not null)
            {
                await this.openedTabs.SetAsync(contextId, new OpenedTabs(browser, run, ids), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.openedTabs.RemoveAsync(contextId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            this.logger.Warn("Browser", "OpenedTabsNotRemembered", "Could not remember the tabs just opened, so they will be closed by address only.", e);
            // Remembering is a nicety: the tabs still close by address when the profile is left.
        }
    }

    /// <inheritdoc />
    public async Task<TabClosingOutcome> CloseTabsAsync(
        BrowserKind browser,
        IReadOnlyList<string> pages,
        IReadOnlyList<string> keep,
        string? contextId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(keep);

        // Leaving the profile uses up what it opened, whatever happens next.
        OpenedTabs? mine = await this.TakeOpenedTabsAsync(contextId, browser, cancellationToken).ConfigureAwait(false);

        if (pages.Count == 0 && mine is null)
        {
            return new TabClosingOutcome(0, null);
        }

        // Which tab is which is only known by asking the browser itself; a default browser that can't
        // be scripted (Firefox, Arc…) can't be asked.
        if (browser == BrowserKind.Default)
        {
            return new TabClosingOutcome(0, SwitchMessages.CloseTabsNeedsBrowser.With());
        }

        // The matching happens here rather than in the browser, where it would be unreadable and
        // untested: list the tabs, pick the ones to close, then close exactly those.
        TabListing listing = await this.browsers.ListTabsAsync(browser, startIfNeeded: false, timeout / 2, cancellationToken).ConfigureAwait(false);
        if (listing.Tabs is not { } tabs)
        {
            return new TabClosingOutcome(0, this.WhyNotAsked(browser, listing.Problem, SwitchMessages.CouldNotCloseTabs));
        }

        List<BrowserTab> doomed = tabs
            .Where(tab => (PageMatch.MatchesAny(pages, tab.Url) || (tab.Id is { } id && mine?.TabIds.Contains(id) == true))
                && !PageMatch.MatchesAny(keep, tab.Url))
            .ToList();
        if (doomed.Count == 0)
        {
            return new TabClosingOutcome(0, null);
        }

        TabsClosed closing = await this.browsers.CloseTabsAsync(browser, doomed, timeout / 2, cancellationToken).ConfigureAwait(false);
        if (closing.Problem is not null)
        {
            return new TabClosingOutcome(0, this.WhyNotAsked(browser, closing.Problem, SwitchMessages.CouldNotCloseTabs));
        }

        // The browser says how many it closed. Fewer than were meant to close is a tab that stayed
        // open - it asked to confirm leaving, say - and that was never reported: the switch said
        // nothing, and the page was still there. Look again to say which.
        HashSet<TabKey> meant = doomed.Select(TabKey.Of).ToHashSet();
        if (closing.Count is not { } closed || closed >= meant.Count)
        {
            return new TabClosingOutcome(closing.Count ?? 0, null);
        }

        TimeSpan lookTimeout = timeout / 4 < TabProbeTimeout ? timeout / 4 : TabProbeTimeout;
        TabListing after = await this.browsers.ListTabsAsync(browser, startIfNeeded: false, lookTimeout, cancellationToken).ConfigureAwait(false);
        List<BrowserTab> remaining = (after.Tabs ?? []).Where(tab => meant.Contains(TabKey.Of(tab))).ToList();

        string app = this.browsers.AppName(browser);
        UserMessage partial = remaining.Count > 0
            ? SwitchMessages.SomeTabsStayedOpen.With(
                closed.ToString(CultureInfo.InvariantCulture),
                meant.Count.ToString(CultureInfo.InvariantCulture),
                app,
                DescribePages(remaining.Select(tab => tab.Url)))
            : SwitchMessages.SomeTabsNotClosed.With(
                closed.ToString(CultureInfo.InvariantCulture),
                meant.Count.ToString(CultureInfo.InvariantCulture),
                app);
        return new TabClosingOutcome(closed, partial);
    }

    /// <summary>
    /// The sites of some tabs, for a message: "linear.app, github.com, +2". A site rather than the
    /// address - a message is read by a person, and an address can run to a hundred characters of
    /// query - and no more than three, so the line stays one line.
    /// </summary>
    public static string DescribePages(IEnumerable<string> addresses)
    {
        const int Shown = 3;
        List<string> sites = addresses
            .Select(address => Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Host.Length > 0
                ? (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host)
                : (address.Length > 40 ? address[..40] + "…" : address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return sites.Count <= Shown
            ? string.Join(", ", sites)
            : $"{string.Join(", ", sites.Take(Shown))}, +{sites.Count - Shown}";
    }

    /// <summary>
    /// What <paramref name="contextId"/> opened in this run of <paramref name="browser"/>, forgetting it.
    /// Ids from another browser, or an earlier run of this one - which numbers its tabs afresh - are
    /// dropped: they may now be any tab at all.
    /// </summary>
    private async Task<OpenedTabs?> TakeOpenedTabsAsync(string? contextId, BrowserKind browser, CancellationToken cancellationToken)
    {
        if (contextId is null)
        {
            return null;
        }

        try
        {
            OpenedTabs? remembered = await this.openedTabs.GetAsync(contextId, cancellationToken).ConfigureAwait(false);
            if (remembered is null)
            {
                return null;
            }

            await this.openedTabs.RemoveAsync(contextId, cancellationToken).ConfigureAwait(false);
            return remembered.Browser == browser && this.browserRuns.Current(browser) == remembered.BrowserRun
                ? remembered
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            this.logger.Warn("Browser", "OpenedTabsNotRead", "Could not read the tabs this profile opened, so they are closed by address only.", e);
            return null;
        }
    }

    /// <summary>
    /// Says why a browser couldn't be asked about its tabs. "Check Automation permissions" was the
    /// answer to every failure, and sent people to settings that were fine when the browser had merely
    /// been slow. <paramref name="unknown"/> is for when the browser said nothing to go on.
    /// </summary>
    private UserMessage WhyNotAsked(BrowserKind browser, BrowserProblem? problem, MessageTemplate unknown)
    {
        string app = this.browsers.AppName(browser);
        string reason = problem?.Reason ?? string.Empty;

        return problem?.Kind switch
        {
            BrowserProblemKind.NotAnswering => SwitchMessages.BrowserNotAnswering.With(app),
            BrowserProblemKind.NotAllowed => SwitchMessages.BrowserNotAllowed.With(app),
            _ => reason.Length == 0
                ? unknown.With(app)
                : SwitchMessages.CouldNotControlBrowser.With(app, reason.Split('\n')[0].Trim())
        };
    }

    private async Task<BrowserLaunchOutcome> LaunchProfilesAsync(
        IReadOnlyList<BrowserProfileConfig> profiles,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        List<UserMessage> warnings = [];

        foreach (BrowserProfileConfig profile in profiles)
        {
            BrowserProblem? problem = await this.browsers
                .OpenProfileAsync(profile.Browser, profile.ProfileDirectory, profile.Urls, timeout, cancellationToken)
                .ConfigureAwait(false);

            if (problem is not null)
            {
                warnings.Add(SwitchMessages.CouldNotLaunchBrowserProfile.With(profile.Browser.ToString(), profile.ProfileDirectory, problem.Reason));
            }
        }

        return new BrowserLaunchOutcome(warnings);
    }

    /// <summary>
    /// What tells one tab from another: its id where the browser gives one - which it keeps however its
    /// address changes - and otherwise its address.
    /// </summary>
    private sealed record TabKey(int? Id, string? Url)
    {
        public static TabKey Of(BrowserTab tab) => tab.Id is { } id ? new TabKey(id, null) : new TabKey(null, tab.Url);
    }
}
