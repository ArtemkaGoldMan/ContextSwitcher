using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Automation;

public sealed class AutomationPlanBuilderTests
{
    [Fact]
    public void BuildOrdersStepsAsResourceFreeingThenEnvironmentThenActivation()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition previous = new()
        {
            Id = "personal",
            DisplayName = "Personal",
            CloseApps = ["Spotify"],
            Docker = new DockerResourceConfig { Stop = ["redis-personal"] }
        };

        ContextDefinition target = new()
        {
            Id = "work",
            DisplayName = "Work",
            LaunchApps = ["Slack"],
            Focus = new FocusConfig { Enabled = true, ModeName = "Work" },
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Urls = ["https://example.com/"]
            },
            Docker = new DockerResourceConfig { Start = ["postgres-work"] },
            Media = new MediaConfig { Player = MediaPlayerKind.Spotify, Playlist = "Deep Focus" }
        };

        AutomationPlan plan = builder.Build(previous, target);

        Assert.Equal(
            [
                AutomationStepType.StopDockerResources,
                AutomationStepType.CloseApplications,
                AutomationStepType.SetFocusMode,
                AutomationStepType.LaunchApplications,
                AutomationStepType.ManageBrowserContext,
                AutomationStepType.StartDockerResources,
                AutomationStepType.ControlMedia
            ],
            plan.Steps.Select(step => step.Type));

        Assert.Equal("work", plan.TargetContextId);
        Assert.Equal("personal", plan.PreviousContextId);
    }

    [Fact]
    public void BuildOmitsStepsForEmptyOrDefaultConfiguration()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition target = new()
        {
            Id = "default",
            DisplayName = "Default"
        };

        AutomationPlan plan = builder.Build(previous: null, target);

        // Nothing configured means nothing to do. SetFocusMode used to appear here regardless,
        // which is what made every switch report a warning on a machine without the Shortcuts.
        Assert.Empty(plan.Steps);
    }

    /// <summary>
    /// Pins the direction of <c>closeApps</c>: a profile's list names the apps quit on the way
    /// *out* of it, matching the close-on-leave toggle in section 11.1.2. The example config in
    /// section 6.1 was once written the other way round, which read as "these are closed when you
    /// arrive" and silently did nothing on that transition.
    /// </summary>
    [Fact]
    public void BuildClosesThePreviousContextsAppsAndNotTheTargets()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition previous = new()
        {
            Id = "personal",
            DisplayName = "Personal",
            CloseApps = ["Obsidian", "Spotify"]
        };

        ContextDefinition target = new()
        {
            Id = "work",
            DisplayName = "Work",
            CloseApps = ["Slack"]
        };

        AutomationPlan plan = builder.Build(previous, target);

        AutomationStep close = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.CloseApplications);
        Assert.Equal(["Obsidian", "Spotify"], AppsArguments.From(close.Arguments).Apps);
    }

    [Fact]
    public void BuildOmitsCloseApplicationsWhenThereIsNoPreviousContext()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition target = new()
        {
            Id = "work",
            DisplayName = "Work",
            CloseApps = ["Slack"]
        };

        AutomationPlan plan = builder.Build(previous: null, target);

        Assert.DoesNotContain(plan.Steps, step => step.Type == AutomationStepType.CloseApplications);
    }

    [Fact]
    public void BuildMarksStepCriticalWhenTypeNameIsInSwitchPolicy()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition target = new()
        {
            Id = "work",
            DisplayName = "Work",
            LaunchApps = ["Slack"],
            Focus = new FocusConfig { Enabled = true, ModeName = "Work" },
            SwitchPolicy = new SwitchPolicyConfig { CriticalSteps = ["LaunchApplications"] }
        };

        AutomationPlan plan = builder.Build(previous: null, target);

        AutomationStep launchStep = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.LaunchApplications);
        AutomationStep focusStep = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);

        Assert.True(launchStep.IsCritical);
        Assert.False(focusStep.IsCritical);
    }

    /// <summary>
    /// Running "Focus Off" is only worth doing when something turned Focus on. Building the step
    /// unconditionally meant anyone who had not created the three Shortcuts saw a warning on every
    /// switch, and every switch exited 5 rather than 0.
    /// </summary>
    [Fact]
    public void BuildOmitsFocusStepWhenNeitherContextUsesFocus()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition previous = new() { Id = "personal", DisplayName = "Personal" };
        ContextDefinition target = new() { Id = "work", DisplayName = "Work", LaunchApps = ["Slack"] };

        AutomationPlan plan = builder.Build(previous, target);

        Assert.DoesNotContain(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);
    }

    [Fact]
    public void BuildIncludesFocusStepWhenTheTargetEnablesIt()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition target = new()
        {
            Id = "work",
            DisplayName = "Work",
            Focus = new FocusConfig { Enabled = true, ModeName = "Work" }
        };

        AutomationPlan plan = builder.Build(previous: null, target);

        AutomationStep focus = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);
        Assert.True(FocusArguments.From(focus.Arguments).Enabled);
        Assert.Equal("Work", FocusArguments.From(focus.Arguments).ModeName);
    }

    /// <summary>
    /// The case that keeps the step honest: leaving a profile that turned Focus on still has to
    /// clear it, even though the profile being entered wants no Focus of its own.
    /// </summary>
    [Fact]
    public void BuildIncludesFocusStepToClearFocusLeftOnByThePreviousContext()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());

        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            Focus = new FocusConfig { Enabled = true, ModeName = "Work" }
        };
        ContextDefinition target = new() { Id = "personal", DisplayName = "Personal" };

        AutomationPlan plan = builder.Build(previous, target);

        AutomationStep focus = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);
        Assert.False(FocusArguments.From(focus.Arguments).Enabled);

        // The mode to turn off, which has its own Off Shortcut.
        Assert.Equal("Work", FocusArguments.From(focus.Arguments).PreviousModeName);
    }

    /// <summary>
    /// Found in a real settings file: Focus enabled with no mode named. That turns nothing on, so it
    /// must not count as using Focus - it made every switch to the profile run the "Off" Shortcut and
    /// warn that it was missing. Leaving such a profile has nothing to clear either.
    /// </summary>
    [Fact]
    public void FocusEnabledWithNoModeNamedIsNotUsingFocus()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());
        ContextDefinition noMode = new() { Id = "test-work", DisplayName = "Work", Focus = new FocusConfig { Enabled = true, ModeName = "  " } };
        ContextDefinition none = new() { Id = "personal", DisplayName = "Personal" };

        Assert.DoesNotContain(builder.Build(none, noMode).Steps, step => step.Type == AutomationStepType.SetFocusMode);
        Assert.DoesNotContain(builder.Build(noMode, none).Steps, step => step.Type == AutomationStepType.SetFocusMode);

        // Coming back from a profile that did turn a Focus on still clears it.
        ContextDefinition dnd = new() { Id = "home", DisplayName = "Home", Focus = new FocusConfig { Enabled = true, ModeName = "Do Not Disturb" } };
        AutomationStep focus = Assert.Single(builder.Build(dnd, noMode).Steps, step => step.Type == AutomationStepType.SetFocusMode);
        Assert.False(FocusArguments.From(focus.Arguments).Enabled);
    }
    /// <summary>
    /// Leaving a profile closes the tabs it lists - first, before any app is quit, so a browser
    /// that is quit too doesn't bring them back - and spares what the next profile opens.
    /// </summary>
    [Fact]
    public void LeavingAProfileClosesItsTabsButNotThoseTheNextOneOpens()
    {
        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            CloseApps = ["Slack"],
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                Urls = ["https://linear.app/"],
                CloseUrls = ["https://linear.app/", "https://mail.google.com/"]
            }
        };
        ContextDefinition target = new()
        {
            Id = "personal",
            DisplayName = "Personal",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                Urls = ["https://mail.google.com/"]
            }
        };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous, target);

        Assert.Equal(
            [AutomationStepType.CloseBrowserTabs, AutomationStepType.CloseApplications, AutomationStepType.ManageBrowserContext],
            plan.Steps.Select(step => step.Type));
        AutomationStep closing = plan.Steps[0];
        Assert.Equal(BrowserKind.Chrome, CloseTabsArguments.From(closing.Arguments).Browser);
        Assert.Equal(["https://linear.app/", "https://mail.google.com/"], CloseTabsArguments.From(closing.Arguments).CloseUrls);
        Assert.Equal(["https://mail.google.com/"], CloseTabsArguments.From(closing.Arguments).KeepUrls);
        Assert.Equal("work", CloseTabsArguments.From(closing.Arguments).ContextId);

        // Entering, the profile says whose pages it opens and which of them close later, so the tabs
        // opened for those can be remembered - and found again when it is left.
        AutomationStep entering = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(target, previous).Steps.Single(step => step.Type == AutomationStepType.ManageBrowserContext);
        Assert.Equal("work", BrowserContextArguments.From(entering.Arguments).ContextId);
        Assert.Equal(["https://linear.app/", "https://mail.google.com/"], BrowserContextArguments.From(entering.Arguments).RememberUrls);
    }

    /// <summary>Only pages in the same browser are spared: a page open in Safari is a different tab.</summary>
    [Fact]
    public void PagesTheNextProfileOpensInAnotherBrowserAreNotSpared()
    {
        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                CloseUrls = ["https://mail.google.com/"]
            }
        };
        ContextDefinition target = new()
        {
            Id = "personal",
            DisplayName = "Personal",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Safari,
                Urls = ["https://mail.google.com/"]
            }
        };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous, target);

        Assert.Empty(CloseTabsArguments.From(plan.Steps.Single(step => step.Type == AutomationStepType.CloseBrowserTabs).Arguments).KeepUrls);
    }

    /// <summary>
    /// A profile in another mode has no pages to close; entering a profile with pages only to close
    /// opens nothing.
    /// </summary>
    [Fact]
    public void NothingClosesWithoutPagesToClose()
    {
        AutomationPlanBuilder builder = new(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser());
        ContextDefinition closeOnly = new()
        {
            Id = "focus",
            DisplayName = "Focus",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                CloseUrls = ["https://youtube.com/"]
            }
        };
        ContextDefinition target = new() { Id = "rest", DisplayName = "Rest" };

        Assert.Empty(builder.Build(target, closeOnly).Steps);
        Assert.Single(builder.Build(closeOnly, target).Steps);
        Assert.Empty(builder.Build(closeOnly with { BrowserManagement = closeOnly.BrowserManagement with { Mode = BrowserManagementMode.Groups } }, target).Steps);
    }

    /// <summary>
    /// Turning a profile off is the half of a switch that leaves it - its containers, tabs, apps and
    /// Focus - under its own policy, and nothing else.
    /// </summary>
    [Fact]
    public void TurningOffBuildsOnlyTheLeavingHalf()
    {
        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            CloseApps = ["Slack"],
            LaunchApps = ["Linear"],
            Focus = new FocusConfig { Enabled = true, ModeName = "Work" },
            Docker = new DockerResourceConfig { Stop = ["db"], Start = ["db"] },
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                Urls = ["https://linear.app/"],
                CloseUrls = ["https://linear.app/"]
            },
            SwitchPolicy = new SwitchPolicyConfig { CriticalSteps = ["CloseApplications"] }
        };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous, target: null);

        Assert.Equal(
            [AutomationStepType.StopDockerResources, AutomationStepType.CloseBrowserTabs, AutomationStepType.CloseApplications, AutomationStepType.SetFocusMode],
            plan.Steps.Select(step => step.Type));
        Assert.Equal((string.Empty, "work"), (plan.TargetContextId, plan.PreviousContextId));
        Assert.Empty(CloseTabsArguments.From(plan.Steps[1].Arguments).KeepUrls);
        Assert.True(plan.Steps[2].IsCritical);
        AutomationStep focus = plan.Steps[3];
        Assert.Equal((false, "Work"), (FocusArguments.From(focus.Arguments).Enabled, FocusArguments.From(focus.Arguments).PreviousModeName));
    }

    /// <summary>
    /// An app or container the next profile starts again is left running, rather than quit and
    /// reopened - which lost its windows and made the switch wait for both.
    /// </summary>
    [Fact]
    public void WhatTheNextProfileStartsIsLeftRunning()
    {
        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            CloseApps = ["Slack", "Visual Studio Code"],
            Docker = new DockerResourceConfig { Stop = ["postgres", "redis"] }
        };
        ContextDefinition target = new()
        {
            Id = "side",
            DisplayName = "Side project",
            LaunchApps = ["slack"],
            Docker = new DockerResourceConfig { Start = ["postgres"] }
        };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous, target);

        Assert.Equal(["Visual Studio Code"], AppsArguments.From(plan.Steps.Single(step => step.Type == AutomationStepType.CloseApplications).Arguments).Apps);
        Assert.Equal(["redis"], ContainersArguments.From(plan.Steps.Single(step => step.Type == AutomationStepType.StopDockerResources).Arguments).Containers);

        // Leaving for no profile at all, everything listed goes.
        AutomationPlan off = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous, target: null);
        Assert.Equal(["Slack", "Visual Studio Code"], AppsArguments.From(off.Steps.Single(step => step.Type == AutomationStepType.CloseApplications).Arguments).Apps);
    }

    [Fact]
    public void NothingToCloseWhenTheNextProfileStartsItAll()
    {
        ContextDefinition previous = new() { Id = "work", DisplayName = "Work", CloseApps = ["Slack"] };
        ContextDefinition target = new() { Id = "side", DisplayName = "Side", LaunchApps = ["Slack"] };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous, target);

        Assert.DoesNotContain(plan.Steps, step => step.Type == AutomationStepType.CloseApplications);
    }

    /// <summary>
    /// "Default browser" is planned as the browser macOS opens links in, when it can be scripted:
    /// duplicates are checked for in it, its tabs close, and the next profile's pages in it are
    /// spared whether that profile names it or not.
    /// </summary>
    [Fact]
    public void TheDefaultBrowserIsPlannedAsTheOneInUse()
    {
        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Default,
                CloseUrls = ["https://linear.app/", "https://mail.google.com/"]
            }
        };
        ContextDefinition target = new()
        {
            Id = "personal",
            DisplayName = "Personal",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                Urls = ["https://mail.google.com/"]
            }
        };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeDefaultBrowser(BrowserKind.Chrome, "Google Chrome")).Build(previous, target);

        AutomationStep closing = plan.Steps.Single(step => step.Type == AutomationStepType.CloseBrowserTabs);
        Assert.Equal((BrowserKind.Chrome, "https://mail.google.com/"), (CloseTabsArguments.From(closing.Arguments).Browser, Assert.Single(CloseTabsArguments.From(closing.Arguments).KeepUrls)));

        // Entering a Default profile opens its pages in that browser too, checking for duplicates.
        ContextDefinition entering = previous with { BrowserManagement = previous.BrowserManagement with { Urls = ["https://linear.app/"] } };
        AutomationPlan opening = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeDefaultBrowser(BrowserKind.Safari, "Safari")).Build(null, entering);
        Assert.Equal(BrowserKind.Safari, BrowserContextArguments.From(opening.Steps.Single(step => step.Type == AutomationStepType.ManageBrowserContext).Arguments).Browser);
    }

    /// <summary>
    /// Tab groups mode can't come from a settings file any more - it is read as pages or nothing -
    /// and one made in code is not carried out: no browser lets another app open a tab group.
    /// </summary>
    [Fact]
    public void ATabGroupsProfileOpensAndClosesNothing()
    {
        ContextDefinition groups = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Groups, Browser = BrowserKind.Safari, Urls = ["https://example.com/"], CloseUrls = ["https://example.com/"] }
        };
        ContextDefinition other = new() { Id = "personal", DisplayName = "Personal" };

        AutomationPlan entering = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(other, groups);
        AutomationPlan leaving = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(groups, other);

        Assert.DoesNotContain(entering.Steps, step => step.Type is AutomationStepType.ManageBrowserContext or AutomationStepType.CloseBrowserTabs);
        Assert.DoesNotContain(leaving.Steps, step => step.Type is AutomationStepType.ManageBrowserContext or AutomationStepType.CloseBrowserTabs);
        Assert.DoesNotContain(entering.Steps.SelectMany(step => step.Arguments.Keys), key => key == "tabGroups");
    }

    /// <summary>
    /// What the user chose to close of everything else open joins the profile's own: apps in the one
    /// closing step - but never one the next profile opens - and tabs a step per browser, by their own
    /// addresses and with no profile's remembered tabs.
    /// </summary>
    [Fact]
    public void ChosenExtrasAreClosedWithTheProfilesOwn()
    {
        ContextDefinition work = new() { Id = "work", DisplayName = "Work", CloseApps = ["Slack"] };
        ContextDefinition personal = new() { Id = "personal", DisplayName = "Personal", LaunchApps = ["Notes"] };
        SwitchExtras extras = new(
            ["Figma", "slack", "Notes"],
            [new TabToClose(BrowserKind.Chrome, "https://a.example/"), new TabToClose(BrowserKind.Safari, "https://b.example/"), new TabToClose(BrowserKind.Chrome, "https://c.example/")]);

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(work, personal, extras);

        AutomationStep apps = plan.Steps.Single(step => step.Type == AutomationStepType.CloseApplications);
        Assert.Equal(["Slack", "Figma"], AppsArguments.From(apps.Arguments).Apps);
        List<AutomationStep> tabs = plan.Steps.Where(step => step.Type == AutomationStepType.CloseBrowserTabs).ToList();
        Assert.Equal(2, tabs.Count);
        Assert.Equal(["https://a.example/", "https://c.example/"], tabs.Select(step => CloseTabsArguments.From(step.Arguments)).Single(arguments => arguments.Browser == BrowserKind.Chrome).CloseUrls);
        Assert.All(tabs, step => Assert.False(step.Arguments.ContainsKey("contextId")));
        Assert.Equal(tabs.Count, tabs.Select(step => step.Id).Distinct().Count());
        List<AutomationStep> order = plan.Steps.ToList();
        Assert.True(order.IndexOf(tabs[0]) < order.IndexOf(apps), "tabs close before the apps, as the profile's own do");
    }

    /// <summary>Turning a profile on with none on before: what was chosen to close still closes.</summary>
    [Fact]
    public void ExtrasCloseEvenWithNoProfileBeingLeft()
    {
        ContextDefinition personal = new() { Id = "personal", DisplayName = "Personal" };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(null, personal, new SwitchExtras(["Figma"], []));

        Assert.Equal(["Figma"], AppsArguments.From(plan.Steps.Single(step => step.Type == AutomationStepType.CloseApplications).Arguments).Apps);
    }

    /// <summary>An app never touched is neither opened, closed nor hidden, even when a profile still lists it by hand.</summary>
    [Fact]
    public void IgnoredAppsAreLeftAlone()
    {
        ContextDefinition work = new() { Id = "work", DisplayName = "Work", CloseApps = ["Telegram", "Slack"], HideApps = ["telegram"] };
        ContextDefinition personal = new() { Id = "personal", DisplayName = "Personal", LaunchApps = ["Telegram"] };

        AutomationPlan leaving = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(work, personal, ignoredApps: ["Telegram"]);
        AutomationPlan entering = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(null, personal, ignoredApps: ["Telegram"]);

        Assert.Equal(["Slack"], AppsArguments.From(leaving.Steps.Single(step => step.Type == AutomationStepType.CloseApplications).Arguments).Apps);
        Assert.DoesNotContain(leaving.Steps, step => step.Type == AutomationStepType.HideApplications);
        Assert.DoesNotContain(entering.Steps, step => step.Type == AutomationStepType.LaunchApplications);
    }

    /// <summary>A default browser that can't be scripted stays Default, and the closing step says why it did nothing.</summary>
    [Fact]
    public void AnUnscriptableDefaultBrowserStaysDefault()
    {
        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, CloseUrls = ["https://linear.app/"] }
        };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeDefaultBrowser(BrowserKind.Default, "Firefox")).Build(previous, target: null);

        Assert.Equal(BrowserKind.Default, CloseTabsArguments.From(plan.Steps.Single(step => step.Type == AutomationStepType.CloseBrowserTabs).Arguments).Browser);
    }

    /// <summary>
    /// Leaving hides the apps listed to hide, after quitting those listed to close - except one the
    /// next profile launches, which opening would only bring straight back.
    /// </summary>
    [Fact]
    public void LeavingHidesTheAppsListedToHide()
    {
        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            CloseApps = ["Slack"],
            HideApps = ["Visual Studio Code", "Notion"]
        };
        ContextDefinition target = new() { Id = "personal", DisplayName = "Personal", LaunchApps = ["Notion"] };

        AutomationPlan plan = new AutomationPlanBuilder(MacPlatform.Capabilities, StepBudgets.Default, new FakeUnknownDefaultBrowser()).Build(previous, target);

        Assert.Equal(
            [AutomationStepType.CloseApplications, AutomationStepType.HideApplications, AutomationStepType.LaunchApplications],
            plan.Steps.Select(step => step.Type));
        Assert.Equal(["Visual Studio Code"], AppsArguments.From(plan.Steps[1].Arguments).Apps);
    }
}
