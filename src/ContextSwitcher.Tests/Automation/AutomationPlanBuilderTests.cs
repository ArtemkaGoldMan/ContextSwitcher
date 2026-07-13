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

        Assert.Equal([AutomationStepType.SetFocusMode], plan.Steps.Select(step => step.Type));
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
            SwitchPolicy = new SwitchPolicyConfig { CriticalSteps = ["LaunchApplications"] }
        };

        AutomationPlan plan = builder.Build(previous: null, target);

        AutomationStep launchStep = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.LaunchApplications);
        AutomationStep focusStep = Assert.Single(plan.Steps, step => step.Type == AutomationStepType.SetFocusMode);

        Assert.True(launchStep.IsCritical);
        Assert.False(focusStep.IsCritical);
    }
}
