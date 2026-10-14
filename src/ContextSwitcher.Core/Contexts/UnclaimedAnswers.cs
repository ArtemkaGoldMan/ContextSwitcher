using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Contexts;

/// <summary>What to do with an app or tab no profile in a switch had.</summary>
public enum UnclaimedChoice
{
    /// <summary>Leave it as it is.</summary>
    Keep,

    /// <summary>Close it along with the switch.</summary>
    Close,

    /// <summary>Make it part of the profile being left: it opens with it, and closes now and whenever it is left.</summary>
    AddToLeaving,

    /// <summary>Make it part of the profile being entered: it opens with it and stays open now.</summary>
    AddToEntering
}

/// <summary>
/// The answers to "what about everything else open?" (<see cref="AppConfiguration.AskOnSwitch"/>): what
/// to close with the switch, and what to add to which profile - as a full part of it, opened on the way
/// in and closed on the way out.
/// </summary>
public sealed record UnclaimedAnswers(
    IReadOnlyList<(RunningApp App, UnclaimedChoice Choice)> Apps,
    IReadOnlyList<(OpenBrowserTab Tab, UnclaimedChoice Choice)> Tabs)
{
    /// <summary>The apps and tabs to close besides what the profiles say.</summary>
    public SwitchExtras ToExtras() => new(
        this.Apps.Where(answer => answer.Choice == UnclaimedChoice.Close).Select(answer => answer.App.Name).ToList(),
        this.Tabs.Where(answer => answer.Choice == UnclaimedChoice.Close).Select(answer => new TabToClose(answer.Tab.Browser, answer.Tab.Url)).ToList());

    /// <summary>Whether anything is to be added to a profile.</summary>
    public bool AddsAnything =>
        this.Apps.Any(answer => answer.Choice is UnclaimedChoice.AddToLeaving or UnclaimedChoice.AddToEntering)
        || this.Tabs.Any(answer => answer.Choice is UnclaimedChoice.AddToLeaving or UnclaimedChoice.AddToEntering);

    /// <summary>
    /// <paramref name="configuration"/> with what was added put into the profiles: an app opened on
    /// entering and closed on leaving, a page the same. A profile that opened no pages starts opening
    /// them, in the tab's own browser; one that opens browser profiles takes no pages - it isn't
    /// offered them.
    /// </summary>
    public AppConfiguration ApplyTo(AppConfiguration configuration, string? leavingId, string? enteringId)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<ContextDefinition> contexts = configuration.Contexts.Select(context =>
        {
            UnclaimedChoice? mine = context.Id == leavingId ? UnclaimedChoice.AddToLeaving
                : context.Id == enteringId ? UnclaimedChoice.AddToEntering
                : null;
            if (mine is null)
            {
                return context;
            }

            List<string> apps = this.Apps.Where(answer => answer.Choice == mine).Select(answer => answer.App.Name).ToList();
            List<OpenBrowserTab> tabs = this.Tabs.Where(answer => answer.Choice == mine).Select(answer => answer.Tab).ToList();
            return WithAdded(context, apps, tabs);
        }).ToList();

        return configuration with { Contexts = contexts };
    }

    /// <summary>Whether a profile can be given pages: it opens pages, or nothing yet - not browser profiles.</summary>
    public static bool TakesPages(ContextDefinition? context) =>
        context is not null && context.BrowserManagement.Mode is BrowserManagementMode.Urls or BrowserManagementMode.None;

    private static ContextDefinition WithAdded(ContextDefinition context, IReadOnlyList<string> apps, IReadOnlyList<OpenBrowserTab> tabs)
    {
        ContextDefinition updated = context with
        {
            LaunchApps = Union(context.LaunchApps, apps),
            CloseApps = Union(context.CloseApps, apps)
        };

        if (tabs.Count == 0 || !TakesPages(context))
        {
            return updated;
        }

        BrowserManagementConfig browser = context.BrowserManagement;
        List<string> pages = tabs.Select(tab => tab.Url).ToList();
        return updated with
        {
            BrowserManagement = browser with
            {
                Mode = BrowserManagementMode.Urls,
                Browser = browser.Mode == BrowserManagementMode.None ? tabs[0].Browser : browser.Browser,
                Urls = Union(browser.Urls, pages),
                CloseUrls = Union(browser.CloseUrls, pages)
            }
        };
    }

    private static IReadOnlyList<string> Union(IReadOnlyList<string> existing, IEnumerable<string> added) =>
        existing.Concat(added.Where(item => !existing.Contains(item, StringComparer.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
