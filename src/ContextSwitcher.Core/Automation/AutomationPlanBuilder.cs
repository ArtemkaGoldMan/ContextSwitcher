using System.Text.Json;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Core.Automation;

/// <summary>
/// Builds the ordered <see cref="AutomationPlan"/> for a switch from one context to another,
/// following the resource-freeing / environment / activation ordering in agent.md section 8.
/// </summary>
public sealed class AutomationPlanBuilder
{
    private static readonly TimeSpan QuitAppTimeoutPerApp = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LaunchAppTimeoutPerApp = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BrowserContextTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ThemeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WallpaperTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FocusTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MediaTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan DockerTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Builds the automation plan for switching from <paramref name="previous"/> to <paramref name="target"/>.
    /// </summary>
    /// <param name="previous">The context being left, or <see langword="null"/> on the first switch.</param>
    /// <param name="target">The context being switched to.</param>
    public AutomationPlan Build(ContextDefinition? previous, ContextDefinition target)
    {
        ArgumentNullException.ThrowIfNull(target);

        HashSet<string> criticalTypes = new(target.SwitchPolicy.CriticalSteps, StringComparer.Ordinal);
        List<AutomationStep> steps = [];

        if (previous is not null)
        {
            AddStopDockerResources(steps, previous, criticalTypes);
            AddCloseApplications(steps, previous, criticalTypes);
        }

        AddSetTheme(steps, target, criticalTypes);
        AddSetWallpaper(steps, target, criticalTypes);
        AddSetFocusMode(steps, target, criticalTypes);
        AddLaunchApplications(steps, target, criticalTypes);
        AddManageBrowserContext(steps, target, criticalTypes);
        AddStartDockerResources(steps, target, criticalTypes);
        AddControlMedia(steps, target, criticalTypes);

        return new AutomationPlan(target.Id, previous?.Id, steps);
    }

    private static void AddStopDockerResources(List<AutomationStep> steps, ContextDefinition previous, HashSet<string> criticalTypes)
    {
        if (previous.Docker.Stop.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.StopDockerResources,
            previous.Id,
            "Stop Docker resources",
            DockerTimeout,
            criticalTypes,
            new Dictionary<string, string> { ["containers"] = string.Join(',', previous.Docker.Stop) }));
    }

    private static void AddCloseApplications(List<AutomationStep> steps, ContextDefinition previous, HashSet<string> criticalTypes)
    {
        if (previous.CloseApps.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.CloseApplications,
            previous.Id,
            "Close applications",
            QuitAppTimeoutPerApp * previous.CloseApps.Count,
            criticalTypes,
            new Dictionary<string, string> { ["apps"] = string.Join(',', previous.CloseApps) }));
    }

    private static void AddSetTheme(List<AutomationStep> steps, ContextDefinition target, HashSet<string> criticalTypes)
    {
        if (target.Theme.Mode == ThemeMode.System)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.SetTheme,
            target.Id,
            "Set theme",
            ThemeTimeout,
            criticalTypes,
            new Dictionary<string, string> { ["mode"] = target.Theme.Mode.ToString() }));
    }

    private static void AddSetWallpaper(List<AutomationStep> steps, ContextDefinition target, HashSet<string> criticalTypes)
    {
        if (string.IsNullOrWhiteSpace(target.Wallpaper.Path))
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.SetWallpaper,
            target.Id,
            "Set wallpaper",
            WallpaperTimeout,
            criticalTypes,
            new Dictionary<string, string>
            {
                ["path"] = target.Wallpaper.Path,
                ["allSpaces"] = target.Wallpaper.AllSpaces.ToString()
            }));
    }

    private static void AddSetFocusMode(List<AutomationStep> steps, ContextDefinition target, HashSet<string> criticalTypes)
    {
        steps.Add(CreateStep(
            AutomationStepType.SetFocusMode,
            target.Id,
            "Set Focus mode",
            FocusTimeout,
            criticalTypes,
            new Dictionary<string, string>
            {
                ["enabled"] = target.Focus.Enabled.ToString(),
                ["modeName"] = target.Focus.ModeName
            }));
    }

    private static void AddLaunchApplications(List<AutomationStep> steps, ContextDefinition target, HashSet<string> criticalTypes)
    {
        if (target.LaunchApps.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.LaunchApplications,
            target.Id,
            "Launch applications",
            LaunchAppTimeoutPerApp * target.LaunchApps.Count,
            criticalTypes,
            new Dictionary<string, string> { ["apps"] = string.Join(',', target.LaunchApps) }));
    }

    private static void AddManageBrowserContext(List<AutomationStep> steps, ContextDefinition target, HashSet<string> criticalTypes)
    {
        BrowserManagementConfig browser = target.BrowserManagement;
        if (browser.Mode == BrowserManagementMode.None)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.ManageBrowserContext,
            target.Id,
            "Manage browser context",
            BrowserContextTimeout,
            criticalTypes,
            new Dictionary<string, string>
            {
                ["mode"] = browser.Mode.ToString(),
                ["browser"] = browser.Browser.ToString(),
                ["urls"] = string.Join(',', browser.Urls),
                ["tabGroups"] = string.Join(',', browser.TabGroups),
                ["avoidDuplicateTabs"] = browser.AvoidDuplicateTabs.ToString(),
                ["profilesJson"] = JsonSerializer.Serialize(browser.Profiles, ContextSwitcherJson.CompactOptions)
            }));
    }

    private static void AddStartDockerResources(List<AutomationStep> steps, ContextDefinition target, HashSet<string> criticalTypes)
    {
        if (target.Docker.Start.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.StartDockerResources,
            target.Id,
            "Start Docker resources",
            DockerTimeout,
            criticalTypes,
            new Dictionary<string, string> { ["containers"] = string.Join(',', target.Docker.Start) }));
    }

    private static void AddControlMedia(List<AutomationStep> steps, ContextDefinition target, HashSet<string> criticalTypes)
    {
        if (target.Media.Player == MediaPlayerKind.None)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.ControlMedia,
            target.Id,
            "Control media",
            MediaTimeout,
            criticalTypes,
            new Dictionary<string, string>
            {
                ["player"] = target.Media.Player.ToString(),
                ["playlist"] = target.Media.Playlist,
                ["autoPlay"] = target.Media.AutoPlay.ToString()
            }));
    }

    private static AutomationStep CreateStep(
        AutomationStepType type,
        string contextId,
        string displayName,
        TimeSpan timeout,
        HashSet<string> criticalTypes,
        IReadOnlyDictionary<string, string> arguments)
    {
        return new AutomationStep(
            Id: $"{type}.{contextId}",
            Type: type,
            DisplayName: displayName,
            IsCritical: criticalTypes.Contains(type.ToString()),
            Timeout: timeout,
            Arguments: arguments);
    }
}
