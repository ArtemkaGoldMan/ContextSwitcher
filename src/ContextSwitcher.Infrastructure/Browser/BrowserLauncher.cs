using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Implements the browser management modes from agent.md section 9.3 and 9.5: opening URLs (with
/// best-effort duplicate-tab avoidance) and launching Chromium browser profiles - and closing the
/// tabs a profile being left lists to close. Tab groups are not among them: no browser lets
/// another app open one (docs/browser-control.md).
/// </summary>
public sealed class BrowserLauncher
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

    private readonly IProcessRunner processRunner;
    private readonly IScriptRunner scriptRunner;
    private readonly IOpenedTabsStore? openedTabs;
    private readonly IBrowserRuns? browserRuns;
    private readonly TimeSpan openedTabPollInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserLauncher"/> class.
    /// </summary>
    /// <param name="openedTabs">Where the tabs a profile opened are remembered; without it, tabs close by address alone.</param>
    /// <param name="browserRuns">Tells one run of Chrome or Brave from the next, so remembered tab ids are never another run's.</param>
    /// <param name="openedTabPollInterval">How long to wait between looks for the tabs just opened.</param>
    public BrowserLauncher(
        IProcessRunner processRunner,
        IScriptRunner scriptRunner,
        IOpenedTabsStore? openedTabs = null,
        IBrowserRuns? browserRuns = null,
        TimeSpan? openedTabPollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(scriptRunner);

        this.processRunner = processRunner;
        this.scriptRunner = scriptRunner;
        this.openedTabs = openedTabs;
        this.browserRuns = browserRuns;
        this.openedTabPollInterval = openedTabPollInterval ?? TimeSpan.FromMilliseconds(250);
    }

    /// <summary>
    /// Manages the browser context for a switch according to <paramref name="request"/>'s mode.
    /// </summary>
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

        // The default browser is unknown ahead of time, so it cannot be targeted by AppleScript.
        bool probeTabs = request.AvoidDuplicateTabs && browser != BrowserKind.Default;
        TimeSpan probeTimeout = timeout < TabProbeTimeout ? timeout : TabProbeTimeout;

        // A page that closes on leaving is remembered by the tab it opened in, in Chrome and Brave,
        // whose tabs keep an id wherever they navigate - see RememberOpenedTabsAsync.
        bool remember = IsChromium(browser)
            && request.ContextId is not null
            && this.openedTabs is not null
            && this.browserRuns is not null
            && request.Urls.Any(url => closeLater.Contains(url, StringComparer.Ordinal));

        // One sweep of the browser's tabs for the whole context. This used to be a tab sweep per
        // URL, which is what made a three-URL context spend about 490ms deciding what to open even
        // when every tab was already there; a single listing costs about 130ms and does not grow
        // with the URL count.
        List<BrowserTab> openTabs = [];
        if (probeTabs || remember)
        {
            // A non-zero exit means the inspection itself did not run - a timeout, a denied
            // Automation prompt, or a browser without tab scripting - which is different from
            // "no matching tab". Section 9.3 says to fall back to a plain `open` for everything.
            (List<BrowserTab>? tabs, ProcessResult listing) = await this.ListTabsWithResultAsync(browser, startIfNeeded: true, probeTimeout, cancellationToken).ConfigureAwait(false);
            if (tabs is not null)
            {
                openTabs = tabs;
            }
            else
            {
                remember = false;
                if (probeTabs)
                {
                    probeTabs = false;
                    warnings.Add(WhyNotAsked(browser, listing, SwitchMessages.CouldNotCheckTabs));
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

            ProcessResult result = await this.OpenUrlAsync(browser, url, timeout, cancellationToken).ConfigureAwait(false);
            tabToFocus = null;

            if (result.ExitCode != 0)
            {
                warnings.Add(SwitchMessages.CouldNotOpenUrl.With(url, result.StandardError.Trim()));
            }
            else
            {
                opened.Add(url);
            }
        }

        if (tabToFocus is not null)
        {
            await this.TryFocusExistingTabAsync(browser, tabToFocus, probeTimeout, cancellationToken).ConfigureAwait(false);
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
            if (await this.ListTabsAsync(browser, startIfNeeded: false, timeout, cancellationToken).ConfigureAwait(false) is not { } tabs)
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
        string? run = this.browserRuns!.Current(browser);
        try
        {
            if (ids.Count > 0 && run is not null)
            {
                await this.openedTabs!.SetAsync(contextId, new OpenedTabs(browser, run, ids), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.openedTabs!.RemoveAsync(contextId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Remembering is a nicety: the tabs still close by address when the profile is left.
        }
    }

    /// <summary>
    /// Closes the tabs in <paramref name="browser"/> that show any of <paramref name="pages"/> - and,
    /// in Chrome and Brave, the tabs <paramref name="contextId"/> opened for them, wherever they have
    /// gone since - except those showing one of <paramref name="keep"/>: the pages the profile being
    /// entered is about to open, which would only be closed to be loaded again. No other tab is
    /// touched, and a browser that isn't running is not started.
    /// </summary>
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

        string app = BrowserAppName(browser);

        // The matching happens here rather than in AppleScript, where it would be unreadable and
        // untested: list the tabs, pick the ones to close, then close exactly those.
        (List<BrowserTab>? tabs, ProcessResult listing) = await this.ListTabsWithResultAsync(browser, startIfNeeded: false, timeout / 2, cancellationToken).ConfigureAwait(false);
        if (tabs is null)
        {
            return new TabClosingOutcome(0, WhyNotAsked(browser, listing, SwitchMessages.CouldNotCloseTabs));
        }

        List<BrowserTab> doomed = tabs
            .Where(tab => (PageMatch.MatchesAny(pages, tab.Url) || (tab.Id is { } id && mine?.TabIds.Contains(id) == true))
                && !PageMatch.MatchesAny(keep, tab.Url))
            .ToList();
        if (doomed.Count == 0)
        {
            return new TabClosingOutcome(0, null);
        }

        // Chrome and Brave close by id, so a tab whose address changes between being listed and being
        // closed - a page still redirecting, an app rewriting its URL - is still closed.
        string script = IsChromium(browser)
            ? AppleScriptBuilder.CloseChromiumTabsWithIds(app, doomed.Select(tab => tab.Id!.Value).Distinct().ToList())
            : AppleScriptBuilder.CloseTabsWithUrls(app, doomed.Select(tab => tab.Url).Distinct(StringComparer.Ordinal).ToList());
        ProcessResult closing = await this.scriptRunner.RunAsync(script, timeout / 2, cancellationToken).ConfigureAwait(false);
        if (closing.ExitCode != 0)
        {
            return new TabClosingOutcome(0, WhyNotAsked(browser, closing, SwitchMessages.CouldNotCloseTabs));
        }

        bool counted = int.TryParse(closing.StandardOutput.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int closed);

        // The script says how many it closed. Fewer than were meant to close is a tab that stayed open
        // - it asked to confirm leaving, say - and that was never reported: the switch said nothing, and
        // the page was still there. Look again to say which.
        int meant = IsChromium(browser)
            ? doomed.Select(tab => tab.Id!.Value).Distinct().Count()
            : doomed.Select(tab => tab.Url).Distinct(StringComparer.Ordinal).Count();
        if (!counted || closed >= meant)
        {
            return new TabClosingOutcome(closed, null);
        }

        TimeSpan lookTimeout = timeout / 4 < TabProbeTimeout ? timeout / 4 : TabProbeTimeout;
        (List<BrowserTab>? after, _) = await this.ListTabsWithResultAsync(browser, startIfNeeded: false, lookTimeout, cancellationToken).ConfigureAwait(false);
        List<BrowserTab> remaining = after is null
            ? []
            : after.Where(tab => IsChromium(browser)
                ? doomed.Any(gone => gone.Id == tab.Id)
                : doomed.Any(gone => string.Equals(gone.Url, tab.Url, StringComparison.Ordinal))).ToList();

        UserMessage partial = remaining.Count > 0
            ? SwitchMessages.SomeTabsStayedOpen.With(
                closed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                meant.ToString(System.Globalization.CultureInfo.InvariantCulture),
                app,
                DescribePages(remaining.Select(tab => tab.Url)))
            : SwitchMessages.SomeTabsNotClosed.With(
                closed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                meant.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
        if (contextId is null || this.openedTabs is null || this.browserRuns is null)
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
            return remembered.Browser == browser && IsChromium(browser) && this.browserRuns.Current(browser) == remembered.BrowserRun
                ? remembered
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The browser's tabs, or <see langword="null"/> when it couldn't be asked. Chrome and Brave say
    /// each tab's id as well as its address; Safari's tabs have no id. Without
    /// <paramref name="startIfNeeded"/>, a browser that isn't running has no tabs, rather than being
    /// started to say so.
    /// </summary>
    private async Task<List<BrowserTab>?> ListTabsAsync(BrowserKind browser, bool startIfNeeded, TimeSpan timeout, CancellationToken cancellationToken) =>
        (await this.ListTabsWithResultAsync(browser, startIfNeeded, timeout, cancellationToken).ConfigureAwait(false)).Tabs;

    /// <summary>The browser's tabs, or - when it couldn't be asked - why not: what the script said.</summary>
    private async Task<(List<BrowserTab>? Tabs, ProcessResult Result)> ListTabsWithResultAsync(BrowserKind browser, bool startIfNeeded, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string app = BrowserAppName(browser);
        bool chromium = IsChromium(browser);
        string script = chromium
            ? AppleScriptBuilder.ListChromiumTabs(app, startIfNeeded)
            : startIfNeeded ? AppleScriptBuilder.ListTabUrls(app) : AppleScriptBuilder.ListOpenTabs(app, isSafari: true);

        ProcessResult listing = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);
        if (listing.ExitCode != 0)
        {
            return (null, listing);
        }

        List<BrowserTab> tabs = [];
        foreach (string line in listing.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (chromium)
            {
                string[] fields = line.Split(AppleScriptBuilder.OpenTabFieldSeparator, 2);
                if (fields.Length == 2 && int.TryParse(fields[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id))
                {
                    tabs.Add(new BrowserTab(id, fields[1].Trim()));
                }
            }
            else
            {
                tabs.Add(new BrowserTab(null, line.Split(AppleScriptBuilder.OpenTabFieldSeparator)[0].Trim()));
            }
        }

        return (tabs, listing);
    }

    /// <summary>
    /// Says why a browser couldn't be asked about its tabs, from what the script reported. "Check
    /// Automation permissions" was the answer to every failure, and sent people to settings that were
    /// fine when the browser had merely been slow. <paramref name="unknown"/> is for when the script
    /// said nothing to go on.
    /// </summary>
    private static UserMessage WhyNotAsked(BrowserKind browser, ProcessResult failure, MessageTemplate unknown)
    {
        string app = BrowserAppName(browser);
        string reason = failure.StandardError.Trim();

        if (failure.TimedOut)
        {
            return SwitchMessages.BrowserNotAnswering.With(app);
        }

        // -1743 is errAEEventNotPermitted: Automation is off for this app, or was refused.
        if (reason.Contains("-1743", StringComparison.Ordinal) || reason.Contains("Not authorized", StringComparison.OrdinalIgnoreCase))
        {
            return SwitchMessages.BrowserNotAllowed.With(app);
        }

        return reason.Length == 0
            ? unknown.With(app)
            : SwitchMessages.CouldNotControlBrowser.With(app, reason.Split('\n')[0].Trim());
    }

    private async Task<BrowserLaunchOutcome> LaunchProfilesAsync(
        IReadOnlyList<BrowserProfileConfig> profiles,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        List<UserMessage> warnings = [];

        foreach (BrowserProfileConfig profile in profiles)
        {
            List<string> arguments =
            [
                "-na",
                BrowserAppName(profile.Browser),
                "--args",
                $"--profile-directory={profile.ProfileDirectory}"
            ];
            arguments.AddRange(profile.Urls);

            ProcessResult result = await this.processRunner
                .RunAsync(new ProcessStartOptions("open", arguments, timeout), cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                warnings.Add(SwitchMessages.CouldNotLaunchBrowserProfile.With(profile.Browser.ToString(), profile.ProfileDirectory, result.StandardError.Trim()));
            }
        }

        return new BrowserLaunchOutcome(warnings);
    }

    private async Task<TabProbeOutcome> TryFocusExistingTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string script = browser == BrowserKind.Safari
            ? AppleScriptBuilder.FocusSafariTabWithUrl(url)
            : AppleScriptBuilder.FocusChromiumTabWithUrl(BrowserAppName(browser), url);

        ProcessResult result = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);

        // A non-zero exit means the inspection itself did not run - a timeout, a denied Automation
        // prompt, or a browser without tab scripting - which is different from "no matching tab".
        if (result.ExitCode != 0)
        {
            return TabProbeOutcome.InspectionFailed;
        }

        return result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            ? TabProbeOutcome.Focused
            : TabProbeOutcome.NotFound;
    }

    private Task<ProcessResult> OpenUrlAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        List<string> arguments = browser == BrowserKind.Default
            ? [url]
            : ["-a", BrowserAppName(browser), url];

        return this.processRunner.RunAsync(new ProcessStartOptions("open", arguments, timeout), cancellationToken);
    }

    /// <summary>
    /// Why a duplicate-tab probe did not end in "focus this tab, skip the open".
    /// </summary>
    private enum TabProbeOutcome
    {
        /// <summary>A matching tab was found and focused; the URL must not be opened again.</summary>
        Focused,

        /// <summary>The inspection ran and found no matching tab.</summary>
        NotFound,

        /// <summary>The inspection could not run at all, so nothing is known about existing tabs.</summary>
        InspectionFailed
    }

    private static bool IsChromium(BrowserKind browser) => browser is BrowserKind.Chrome or BrowserKind.Brave;

    private static string BrowserAppName(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Brave => "Brave Browser",
        BrowserKind.Safari => "Safari",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, "Default browser has no fixed application name.")
    };

    /// <summary>A tab: its id where the browser gives one (Chrome, Brave), and its address.</summary>
    private sealed record BrowserTab(int? Id, string Url);
}
