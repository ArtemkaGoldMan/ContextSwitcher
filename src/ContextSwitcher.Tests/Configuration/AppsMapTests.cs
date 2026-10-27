using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Configuration;

/// <summary>Which apps the profiles name, which of them the <c>apps</c> map has an entry for, and adding the rest.</summary>
public sealed class AppsMapTests
{
    private static readonly AppIdentity Slack = new() { WindowsAppId = "com.squirrel.slack.slack", WindowsExe = @"C:\Slack\slack.exe" };
    private static readonly AppIdentity Code = new() { WindowsAppId = "Microsoft.VisualStudioCode" };

    private static AppConfiguration Configuration(IReadOnlyDictionary<string, AppIdentity>? apps = null, params string[] ignored) => new()
    {
        Contexts =
        [
            new ContextDefinition { Id = "work", DisplayName = "Work", LaunchApps = ["Slack", "Visual Studio Code"], CloseApps = ["Telegram"] },
            new ContextDefinition { Id = "home", DisplayName = "Home", LaunchApps = ["slack"], HideApps = ["  Spotify  ", "Telegram"] }
        ],
        IgnoredApps = ignored,
        Apps = apps
    };

    [Fact]
    public void EveryAppAProfileNamesIsNamedOnceWhateverTheListAndTheCase()
    {
        Assert.Equal(["Slack", "Visual Studio Code", "Telegram", "Spotify"], AppsMap.NamedIn(Configuration()));
    }

    [Fact]
    public void AppsThePersonNeverWantsTouchedAreNotNamed()
    {
        Assert.Equal(["Slack", "Visual Studio Code", "Spotify"], AppsMap.NamedIn(Configuration(null, "telegram")));
    }

    [Fact]
    public void NoProfilesNameNoApps()
    {
        Assert.Empty(AppsMap.NamedIn(new AppConfiguration()));
    }

    [Fact]
    public void BlankNamesAreNotApps()
    {
        AppConfiguration configuration = new()
        {
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", LaunchApps = ["", "  ", "Slack"] }]
        };

        Assert.Equal(["Slack"], AppsMap.NamedIn(configuration));
    }

    [Fact]
    public void AnEntryIsFoundWhateverTheCaseOrSpaces()
    {
        AppConfiguration configuration = Configuration(new Dictionary<string, AppIdentity> { ["Slack"] = Slack });

        Assert.Same(Slack, AppsMap.Of(configuration, "slack"));
        Assert.Same(Slack, AppsMap.Of(configuration, "  SLACK "));
        Assert.Null(AppsMap.Of(configuration, "Telegram"));
        Assert.Null(AppsMap.Of(Configuration(), "Slack"));
    }

    [Fact]
    public void TheAppsWithNoEntryAreThoseUnrecorded()
    {
        AppConfiguration configuration = Configuration(new Dictionary<string, AppIdentity> { ["slack"] = Slack });

        Assert.Equal(["Visual Studio Code", "Telegram", "Spotify"], AppsMap.Unrecorded(configuration));
    }

    [Fact]
    public void WhatWasFoundIsAddedForTheAppsThatHaveNoEntry()
    {
        AppConfiguration? updated = AppsMap.With(Configuration(), new Dictionary<string, AppIdentity> { ["Visual Studio Code"] = Code, ["Slack"] = Slack });

        Assert.NotNull(updated);
        Assert.Equal(Code, updated.Apps!["Visual Studio Code"]);
        Assert.Equal(Slack, updated.Apps["Slack"]);
        Assert.Equal(2, updated.Apps.Count);
    }

    /// <summary>A person may have edited an entry by hand.</summary>
    [Fact]
    public void AnEntryThatIsThereIsNeverReplaced()
    {
        AppIdentity edited = new() { WindowsExe = "my-slack.exe" };
        AppConfiguration configuration = Configuration(new Dictionary<string, AppIdentity> { ["Slack"] = edited });

        AppConfiguration? updated = AppsMap.With(configuration, new Dictionary<string, AppIdentity> { ["Slack"] = Slack, ["Visual Studio Code"] = Code });

        Assert.NotNull(updated);
        Assert.Equal(edited, updated.Apps!["Slack"]);
        Assert.Equal(Code, updated.Apps["Visual Studio Code"]);
    }

    [Fact]
    public void AnEntryForAnAppNoProfileNamesStays()
    {
        AppIdentity old = new() { WindowsAppId = "gone.app" };
        AppConfiguration configuration = Configuration(new Dictionary<string, AppIdentity> { ["Old App"] = old });

        AppConfiguration? updated = AppsMap.With(configuration, new Dictionary<string, AppIdentity> { ["Slack"] = Slack });

        Assert.Equal(old, updated!.Apps!["Old App"]);
    }

    [Fact]
    public void NothingNewLeavesTheConfigurationAsItWas()
    {
        AppConfiguration configuration = Configuration(new Dictionary<string, AppIdentity> { ["Slack"] = Slack });

        Assert.Null(AppsMap.With(configuration, new Dictionary<string, AppIdentity>()));
        Assert.Null(AppsMap.With(configuration, new Dictionary<string, AppIdentity> { ["Slack"] = Code }));
        Assert.Null(AppsMap.With(configuration, new Dictionary<string, AppIdentity> { ["Not Named By Any Profile"] = Code }));
    }

    [Fact]
    public void AnAppFoundUnderAnotherCaseIsRecordedUnderTheProfilesSpelling()
    {
        AppConfiguration? updated = AppsMap.With(Configuration(), new Dictionary<string, AppIdentity> { ["SLACK"] = Slack });

        Assert.Equal(Slack, updated!.Apps!["Slack"]);
        Assert.DoesNotContain("SLACK", updated.Apps.Keys);
    }

    [Fact]
    public void AddingEntriesChangesNothingElse()
    {
        AppConfiguration original = Configuration();

        AppConfiguration updated = AppsMap.With(original, new Dictionary<string, AppIdentity> { ["Slack"] = Slack })!;

        Assert.Equal(original with { Apps = updated.Apps }, updated);
        Assert.Null(original.Apps);
    }
}
