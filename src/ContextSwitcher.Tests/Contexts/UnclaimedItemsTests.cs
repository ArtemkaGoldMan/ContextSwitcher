using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.Tests.Contexts;

/// <summary>
/// What a switch asks about: the apps and tabs open that neither profile in it has - nothing either
/// profile opens, closes or hides, no page of theirs, no app that's never touched, and nothing that
/// isn't a web page.
/// </summary>
public sealed class UnclaimedItemsTests
{
    private static readonly ContextDefinition Work = new()
    {
        Id = "work",
        DisplayName = "Work",
        LaunchApps = ["Visual Studio Code"],
        CloseApps = ["Slack"],
        BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Browser = BrowserKind.Chrome, Urls = ["https://github.com/"] }
    };

    private static readonly ContextDefinition Personal = new()
    {
        Id = "personal",
        DisplayName = "Personal",
        HideApps = ["Music"],
        BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, CloseUrls = ["https://www.youtube.com/"] }
    };

    [Fact]
    public void OnlyAppsNeitherProfileHasAreAskedAbout()
    {
        RunningApp[] apps =
        [
            new("Visual Studio Code", "Code", "Code"),
            new("Slack"),
            new("Music"),
            new("Telegram"),
            new("Figma"),
            new("figma")
        ];

        Unclaimed unclaimed = UnclaimedItems.Find(apps, [], Work, Personal, ignoredApps: ["telegram"]);

        Assert.Equal(["Figma"], unclaimed.Apps.Select(app => app.Name));
    }

    /// <summary>A profile may list an app by the name in its bundle or the one macOS shows - "Code" is Visual Studio Code.</summary>
    [Fact]
    public void AnAppListedByAnotherOfItsNamesIsTheProfiles()
    {
        ContextDefinition codeByBundleName = Work with { LaunchApps = ["code"] };

        Unclaimed unclaimed = UnclaimedItems.Find([new("Visual Studio Code", "Code", "Code")], [], codeByBundleName, null, []);

        Assert.Empty(unclaimed.Apps);
    }

    [Fact]
    public void OnlyTabsShowingNeitherProfilesPagesAreAskedAbout()
    {
        OpenBrowserTab[] tabs =
        [
            new(BrowserKind.Chrome, "Pull requests", "https://github.com/pulls"),
            new(BrowserKind.Chrome, "Lo-fi", "https://www.youtube.com/watch?v=1"),
            new(BrowserKind.Safari, "News", "https://news.example/"),
            new(BrowserKind.Safari, "News", "https://news.example/"),
            new(BrowserKind.Chrome, "New Tab", "chrome://newtab/")
        ];

        Unclaimed unclaimed = UnclaimedItems.Find([], tabs, Work, Personal, []);

        OpenBrowserTab news = Assert.Single(unclaimed.Tabs);
        Assert.Equal("https://news.example/", news.Url);
    }

    /// <summary>Turning a profile on, with none on before: only that profile's things are spoken for.</summary>
    [Fact]
    public void WithOneProfileOnlyItsThingsAreSpokenFor()
    {
        Unclaimed unclaimed = UnclaimedItems.Find([new("Slack"), new("Music")], [], null, Work, []);

        Assert.Equal(["Music"], unclaimed.Apps.Select(app => app.Name));
        Assert.False(unclaimed.IsEmpty);
        Assert.True(UnclaimedItems.Find([new("Slack")], [], null, Work, []).IsEmpty);
    }
}
