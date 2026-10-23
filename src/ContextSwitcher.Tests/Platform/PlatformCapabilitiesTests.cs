using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Platform;

/// <summary>
/// What a computer can do. A Mac does everything the app does today; one that can do less - as
/// Windows will - gets no step for what it lacks, and is told what of a Mac's settings it can't use.
/// </summary>
public sealed class PlatformCapabilitiesTests
{
    /// <summary>No Focus, no Apple Music, no Dock, and Chrome alone.</summary>
    internal static readonly PlatformCapabilities Lesser = MacPlatform.Capabilities with
    {
        Focus = false,
        AppleMusic = false,
        DockIcon = false,
        Browsers = [BrowserKind.Chrome],
        FocusFilterBrowsers = []
    };

    /// <summary>Apps and nothing else - no pages, music or Docker - as Windows has in its first stage.</summary>
    internal static readonly PlatformCapabilities AppsOnly = Lesser with
    {
        Spotify = false,
        Docker = false,
        AutomationPermission = false,
        BrowserPages = false,
        Browsers = []
    };

    private static readonly ContextDefinition Everything = new()
    {
        Id = "work",
        DisplayName = "Work",
        Focus = new FocusConfig { Enabled = true, ModeName = "Work" },
        Media = new MediaConfig { Player = MediaPlayerKind.AppleMusic, Playlist = "Deep Focus" },
        BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Browser = BrowserKind.Safari, Urls = ["https://linear.app/"], CloseUrls = ["https://linear.app/"] }
    };

    [Fact]
    public void AMacHasEverythingTheAppDoes()
    {
        PlatformCapabilities mac = MacPlatform.Capabilities;

        Assert.True(mac.Focus && mac.AppleMusic && mac.Spotify && mac.Docker && mac.DockIcon && mac.AutomationPermission && mac.BrowserPages);
        Assert.Equal([BrowserKind.Safari, BrowserKind.Chrome, BrowserKind.Brave], mac.Browsers);
        Assert.Equal([BrowserKind.Chrome, BrowserKind.Brave], mac.BrowsersWithProfiles);
        Assert.True(mac.ClosesTabsById(BrowserKind.Brave));
        Assert.False(mac.ClosesTabsById(BrowserKind.Safari));
        Assert.True(mac.FocusFiltersBrowser(BrowserKind.Safari));
        Assert.True(mac.HasBrowser(BrowserKind.Default));
        Assert.Empty(mac.Unsupported(Configuration(Everything)));
    }

    [Fact]
    public void WhatAComputerLacksIsSaidOnceForEachProfile()
    {
        ContextDefinition profiles = new()
        {
            Id = "home",
            DisplayName = "Home",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Profiles,
                Profiles = [new BrowserProfileConfig { Browser = BrowserKind.Brave, ProfileDirectory = "Default" }, new BrowserProfileConfig { Browser = BrowserKind.Chrome, ProfileDirectory = "Profile 1" }]
            }
        };

        IReadOnlyList<UserMessage> unsupported = Lesser.Unsupported(Configuration(Everything, profiles));

        Assert.Equal(
            [
                "Work turns on a Focus, which this computer doesn't have.",
                "Work plays Apple Music, which this computer doesn't have.",
                "Work opens pages in Safari, which this computer doesn't have.",
                "Home opens Brave's profiles, which this computer can't."
            ],
            unsupported.Select(message => message.English));
    }

    /// <summary>The plan has no step for what the computer lacks, and the rest of the profile still switches.</summary>
    [Fact]
    public void WhatAComputerLacksGetsNoStep()
    {
        ContextDefinition target = Everything with { LaunchApps = ["Slack"] };

        AutomationPlan plan = new AutomationPlanBuilder(Lesser, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous: null, target);

        Assert.Equal([AutomationStepType.LaunchApplications], plan.Steps.Select(step => step.Type));
    }

    /// <summary>
    /// Without pages no page is opened or closed - neither a profile's own nor the tabs chosen when
    /// asked - whichever browsers the computer has.
    /// </summary>
    [Fact]
    public void WithoutPagesNoPageIsOpenedOrClosed()
    {
        PlatformCapabilities noPages = MacPlatform.Capabilities with { BrowserPages = false };
        ContextDefinition chrome = Everything with { BrowserManagement = Everything.BrowserManagement with { Browser = BrowserKind.Chrome } };
        ContextDefinition home = new()
        {
            Id = "home",
            DisplayName = "Home",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Profiles,
                Profiles = [new BrowserProfileConfig { Browser = BrowserKind.Chrome, ProfileDirectory = "Default" }]
            }
        };
        SwitchExtras chosen = new([], [new TabToClose(BrowserKind.Chrome, "https://news.ycombinator.com/")]);
        AutomationPlanBuilder builder = new(noPages, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        AutomationPlan leaving = builder.Build(chrome, home, chosen);
        AutomationPlan entering = builder.Build(home, chrome);

        AutomationStepType[] pageSteps = [AutomationStepType.CloseBrowserTabs, AutomationStepType.ManageBrowserContext];
        Assert.DoesNotContain(leaving.Steps, step => pageSteps.Contains(step.Type));
        Assert.DoesNotContain(entering.Steps, step => pageSteps.Contains(step.Type));
    }

    [Fact]
    public void WithoutAnyPlayerThereIsNoMusic()
    {
        Assert.False(AppsOnly.PlaysMusic);
        Assert.True(AppsOnly.Plays(MediaPlayerKind.None));
        Assert.False(AppsOnly.Plays(MediaPlayerKind.Spotify));
        Assert.True(Lesser.PlaysMusic);
    }

    /// <summary>A Mac's settings on a computer with apps alone: every other part of each profile is named, once.</summary>
    [Fact]
    public void AComputerWithAppsAloneSaysWhatElseAProfileDoes()
    {
        ContextDefinition home = new()
        {
            Id = "home",
            DisplayName = "Home",
            Media = new MediaConfig { Player = MediaPlayerKind.Spotify, Playlist = "spotify:playlist:1" },
            Docker = new DockerResourceConfig { Stop = ["postgres-work"] },
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Profiles,
                Profiles = [new BrowserProfileConfig { Browser = BrowserKind.Chrome, ProfileDirectory = "Default" }]
            }
        };

        IReadOnlyList<UserMessage> unsupported = AppsOnly.Unsupported(Configuration(Everything, home));

        Assert.Equal(
            [
                "Work turns on a Focus, which this computer doesn't have.",
                "Work plays Apple Music, which this computer doesn't have.",
                "Work opens web pages, which this computer can't.",
                "Home plays Spotify, which this computer can't.",
                "Home starts or stops Docker containers, which this computer can't.",
                "Home opens web pages, which this computer can't."
            ],
            unsupported.Select(message => message.English));
    }

    /// <summary>Leaving and entering, a computer with apps alone quits and opens apps, and plans nothing else.</summary>
    [Fact]
    public void AComputerWithAppsAloneGetsOnlyTheirSteps()
    {
        ContextDefinition leaving = Everything with
        {
            CloseApps = ["Slack"],
            Docker = new DockerResourceConfig { Stop = ["postgres-work"] }
        };
        ContextDefinition entering = new()
        {
            Id = "home",
            DisplayName = "Home",
            LaunchApps = ["Spotify"],
            Media = new MediaConfig { Player = MediaPlayerKind.Spotify, Playlist = "spotify:playlist:1" },
            Docker = new DockerResourceConfig { Start = ["postgres-home"] },
            BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Urls = ["https://youtube.com/"] }
        };

        AutomationPlan plan = new AutomationPlanBuilder(AppsOnly, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(leaving, entering);

        Assert.Equal([AutomationStepType.CloseApplications, AutomationStepType.LaunchApplications], plan.Steps.Select(step => step.Type));
    }

    /// <summary>Tabs are remembered by id only in a browser whose tabs keep one.</summary>
    [Fact]
    public void TabsAreRememberedOnlyWhereTheyKeepAnId()
    {
        ContextDefinition chrome = Everything with { BrowserManagement = Everything.BrowserManagement with { Browser = BrowserKind.Chrome } };

        AutomationStep safari = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(null, Everything).Steps.Single(step => step.Type == AutomationStepType.ManageBrowserContext);
        AutomationStep chromeStep = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(null, chrome).Steps.Single(step => step.Type == AutomationStepType.ManageBrowserContext);

        Assert.Empty(BrowserContextArguments.From(safari.Arguments).RememberUrls);
        Assert.Equal(["https://linear.app/"], BrowserContextArguments.From(chromeStep.Arguments).RememberUrls);
    }

    private static AppConfiguration Configuration(params ContextDefinition[] contexts) =>
        new() { ActiveContextId = contexts[0].Id, Contexts = contexts };
}
