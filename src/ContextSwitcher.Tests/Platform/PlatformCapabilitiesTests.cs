using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.Tests.Platform;

/// <summary>
/// What a computer can do. A Mac does everything the app does today; one that can do less - as
/// Windows will - gets no step for what it lacks, and is told what of a Mac's settings it can't use.
/// </summary>
public sealed class PlatformCapabilitiesTests
{
    /// <summary>No Focus, no Apple Music, no Dock, and Chrome alone.</summary>
    internal static readonly PlatformCapabilities Lesser = PlatformCapabilities.MacOS with
    {
        Focus = false,
        AppleMusic = false,
        DockIcon = false,
        Browsers = [BrowserKind.Chrome],
        FocusFilterBrowsers = []
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
        PlatformCapabilities mac = PlatformCapabilities.MacOS;

        Assert.True(mac.Focus && mac.AppleMusic && mac.DockIcon);
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

        AutomationPlan plan = new AutomationPlanBuilder(Lesser, StepBudgets.Default).Build(previous: null, target);

        Assert.Equal([AutomationStepType.LaunchApplications], plan.Steps.Select(step => step.Type));
    }

    /// <summary>Tabs are remembered by id only in a browser whose tabs keep one.</summary>
    [Fact]
    public void TabsAreRememberedOnlyWhereTheyKeepAnId()
    {
        ContextDefinition chrome = Everything with { BrowserManagement = Everything.BrowserManagement with { Browser = BrowserKind.Chrome } };

        AutomationStep safari = new AutomationPlanBuilder(PlatformCapabilities.MacOS, StepBudgets.Default).Build(null, Everything).Steps.Single(step => step.Type == AutomationStepType.ManageBrowserContext);
        AutomationStep chromeStep = new AutomationPlanBuilder(PlatformCapabilities.MacOS, StepBudgets.Default).Build(null, chrome).Steps.Single(step => step.Type == AutomationStepType.ManageBrowserContext);

        Assert.Empty(BrowserContextArguments.From(safari.Arguments).RememberUrls);
        Assert.Equal(["https://linear.app/"], BrowserContextArguments.From(chromeStep.Arguments).RememberUrls);
    }

    private static AppConfiguration Configuration(params ContextDefinition[] contexts) =>
        new() { ActiveContextId = contexts[0].Id, Contexts = contexts };
}
