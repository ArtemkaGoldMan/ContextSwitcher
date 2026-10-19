using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Automation;

/// <summary>
/// What each type of step carries, and how: the keys and the text under them are what they were
/// before the records, and what the plan builder writes, the handler reads back whole.
/// </summary>
public sealed class StepArgumentsTests
{
    [Fact]
    public void AppsAreAJsonList()
    {
        AppsArguments apps = new(["Visual Studio Code", "Slack"]);

        Assert.Equal(new Dictionary<string, string> { ["apps"] = """["Visual Studio Code","Slack"]""" }, apps.ToDictionary());
        Assert.Equal(apps.Apps, AppsArguments.From(apps.ToDictionary()).Apps);
    }

    /// <summary>A comma belongs to the address: a list joined with commas split a map's coordinates into pieces.</summary>
    [Fact]
    public void AnAddressWithCommasStaysWhole()
    {
        BrowserContextArguments arguments = new(
            "work", BrowserManagementMode.Urls, BrowserKind.Chrome, ["https://www.google.com/maps/@50.45,30.52,12z"], [], AvoidDuplicateTabs: true, []);

        Assert.Equal(["https://www.google.com/maps/@50.45,30.52,12z"], BrowserContextArguments.From(arguments.ToDictionary()).Urls);
    }

    [Fact]
    public void ABrowserContextIsCarriedAsBefore()
    {
        BrowserContextArguments arguments = new(
            "work",
            BrowserManagementMode.Profiles,
            BrowserKind.Default,
            [],
            ["https://fusion.online.autodesk.com/"],
            AvoidDuplicateTabs: true,
            [new BrowserProfileConfig { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1", Urls = ["https://mail.example/"] }]);

        IReadOnlyDictionary<string, string> carried = arguments.ToDictionary();

        Assert.Equal("work", carried["contextId"]);
        Assert.Equal("Profiles", carried["mode"]);
        Assert.Equal("Default", carried["browser"]);
        Assert.Equal("[]", carried["urls"]);
        Assert.Equal("""["https://fusion.online.autodesk.com/"]""", carried["rememberUrlsJson"]);
        Assert.Equal("True", carried["avoidDuplicateTabs"]);
        Assert.Equal("""[{"browser":"Chrome","profile_directory":"Profile 1","urls":["https://mail.example/"]}]""", carried["profilesJson"]);

        BrowserContextArguments read = BrowserContextArguments.From(carried);
        Assert.Equal(
            (arguments.ContextId, arguments.Mode, arguments.Browser, arguments.AvoidDuplicateTabs),
            (read.ContextId, read.Mode, read.Browser, read.AvoidDuplicateTabs));
        Assert.Empty(read.Urls);
        Assert.Equal(arguments.RememberUrls, read.RememberUrls);
        Assert.Equal("Profile 1", Assert.Single(read.Profiles).ProfileDirectory);
    }

    /// <summary>The tabs no profile had carry no profile, and so no remembered tabs.</summary>
    [Fact]
    public void ClosingTabsCarriesTheProfileOnlyWhenThereIsOne()
    {
        CloseTabsArguments mine = new(BrowserKind.Chrome, ["https://linear.app/"], ["https://mail.google.com/"], "work");
        CloseTabsArguments others = new(BrowserKind.Safari, ["https://news.example/"], [], ContextId: null);

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["browser"] = "Chrome",
                ["closeUrlsJson"] = """["https://linear.app/"]""",
                ["keepUrlsJson"] = """["https://mail.google.com/"]""",
                ["contextId"] = "work"
            },
            mine.ToDictionary());
        Assert.False(others.ToDictionary().ContainsKey("contextId"));
        Assert.Null(CloseTabsArguments.From(others.ToDictionary()).ContextId);
        Assert.Equal("work", CloseTabsArguments.From(mine.ToDictionary()).ContextId);
    }

    [Fact]
    public void FocusIsCarriedAsBefore()
    {
        FocusArguments focus = new(Enabled: false, ModeName: string.Empty, PreviousModeName: "Work");

        Assert.Equal(
            new Dictionary<string, string> { ["enabled"] = "False", ["modeName"] = string.Empty, ["previousModeName"] = "Work" },
            focus.ToDictionary());
        Assert.Equal(focus, FocusArguments.From(focus.ToDictionary()));
    }

    [Fact]
    public void MediaIsCarriedAsBefore()
    {
        MediaArguments media = new(MediaPlayerKind.AppleMusic, "Deep Focus", AutoPlay: true);

        Assert.Equal(
            new Dictionary<string, string> { ["player"] = "AppleMusic", ["playlist"] = "Deep Focus", ["autoPlay"] = "True" },
            media.ToDictionary());
        Assert.Equal(media, MediaArguments.From(media.ToDictionary()));
    }

    /// <summary>A player this build doesn't know plays nothing, rather than failing the switch.</summary>
    [Fact]
    public void AnUnknownPlayerIsNone()
    {
        Assert.Equal(MediaPlayerKind.None, MediaArguments.From(new Dictionary<string, string> { ["player"] = "Winamp" }).Player);
    }

    [Fact]
    public void ContainersAreAJsonList()
    {
        ContainersArguments containers = new(["postgres-work", "redis-work"]);

        Assert.Equal(new Dictionary<string, string> { ["containers"] = """["postgres-work","redis-work"]""" }, containers.ToDictionary());
        Assert.Equal(containers.Containers, ContainersArguments.From(containers.ToDictionary()).Containers);
    }
}
