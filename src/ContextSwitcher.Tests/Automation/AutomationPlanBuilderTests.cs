using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Automation;

public sealed class AutomationPlanBuilderTests
{
    [Fact]
    public void BuildOrdersStepsAsResourceFreeingThenEnvironmentThenActivation()
    {
        AutomationPlanBuilder builder = new();

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
            Theme = new ThemeConfig { Mode = ThemeMode.Dark },
            Wallpaper = new WallpaperConfig { Path = "/tmp/work.jpg" },
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
                AutomationStepType.SetTheme,
                AutomationStepType.SetWallpaper,
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
        AutomationPlanBuilder builder = new();

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
        AutomationPlanBuilder builder = new();

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
        Assert.Equal("Obsidian,Spotify", close.Arguments["apps"]);
    }

    [Fact]
    public void BuildOmitsCloseApplicationsWhenThereIsNoPreviousContext()
    {
        AutomationPlanBuilder builder = new();

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
        AutomationPlanBuilder builder = new();

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
        AutomationPlanBuilder builder = new();

        ContextDefinition previous = new() { Id = "personal", DisplayName = "Personal" };
        ContextDefinition target = new() { Id = "work", DisplayName = "Work", LaunchApps = ["Slack"] };

        AutomationPlan plan = builder.Build(previous, target);

        Assert.DoesNotContain(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);
    }

    [Fact]
    public void BuildIncludesFocusStepWhenTheTargetEnablesIt()
    {
        AutomationPlanBuilder builder = new();

        ContextDefinition target = new()
        {
            Id = "work",
            DisplayName = "Work",
            Focus = new FocusConfig { Enabled = true, ModeName = "Work" }
        };

        AutomationPlan plan = builder.Build(previous: null, target);

        AutomationStep focus = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);
        Assert.Equal("True", focus.Arguments["enabled"]);
        Assert.Equal("Work", focus.Arguments["modeName"]);
    }

    /// <summary>
    /// The case that keeps the step honest: leaving a profile that turned Focus on still has to
    /// clear it, even though the profile being entered wants no Focus of its own.
    /// </summary>
    [Fact]
    public void BuildIncludesFocusStepToClearFocusLeftOnByThePreviousContext()
    {
        AutomationPlanBuilder builder = new();

        ContextDefinition previous = new()
        {
            Id = "work",
            DisplayName = "Work",
            Focus = new FocusConfig { Enabled = true, ModeName = "Work" }
        };
        ContextDefinition target = new() { Id = "personal", DisplayName = "Personal" };

        AutomationPlan plan = builder.Build(previous, target);

        AutomationStep focus = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);
        Assert.Equal("False", focus.Arguments["enabled"]);
    }

}
