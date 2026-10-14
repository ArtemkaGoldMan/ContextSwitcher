using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// The apps and tabs open that neither profile in a switch has - the profile being left and the one
/// being entered - and so nothing in the switch would touch: what <see cref="AppConfiguration.AskOnSwitch"/>
/// asks about. An app is a profile's when it opens, closes or hides it; a tab, when it shows one of
/// its pages (<see cref="PageMatch"/>). Ignored apps are never anyone's business.
/// </summary>
public sealed record Unclaimed(IReadOnlyList<RunningApp> Apps, IReadOnlyList<OpenBrowserTab> Tabs)
{
    /// <summary>Whether there is nothing to ask about.</summary>
    public bool IsEmpty => this.Apps.Count == 0 && this.Tabs.Count == 0;
}

/// <summary>Finds what a switch leaves unclaimed; see <see cref="Unclaimed"/>.</summary>
public static class UnclaimedItems
{
    public static Unclaimed Find(
        IReadOnlyList<RunningApp> apps,
        IReadOnlyList<OpenBrowserTab> tabs,
        ContextDefinition? leaving,
        ContextDefinition? entering,
        IReadOnlyCollection<string> ignoredApps)
    {
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(ignoredApps);

        List<string> claimedApps = [.. AppsOf(leaving), .. AppsOf(entering), .. ignoredApps];
        List<RunningApp> unclaimedApps = apps
            .Where(app => !claimedApps.Any(app.IsCalled))
            .DistinctBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        List<string> claimedPages = [.. PagesOf(leaving), .. PagesOf(entering)];
        List<OpenBrowserTab> unclaimedTabs = tabs
            .Where(tab => IsWebPage(tab.Url) && !PageMatch.MatchesAny(claimedPages, tab.Url))
            .DistinctBy(tab => (tab.Browser, tab.Url))
            .ToList();

        return new Unclaimed(unclaimedApps, unclaimedTabs);
    }

    private static IEnumerable<string> AppsOf(ContextDefinition? context) =>
        context is null ? [] : context.LaunchApps.Concat(context.CloseApps).Concat(context.HideApps);

    private static IEnumerable<string> PagesOf(ContextDefinition? context) =>
        context is null
            ? []
            : context.BrowserManagement.Urls
                .Concat(context.BrowserManagement.CloseUrls)
                .Concat(context.BrowserManagement.Profiles.SelectMany(profile => profile.Urls));

    /// <summary>Only what a profile can hold: a new tab page or a chrome:// page is no one's to keep or add.</summary>
    private static bool IsWebPage(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
