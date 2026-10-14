using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
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
    private static readonly TimeSpan CloseTabsTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HideAppsTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FocusTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MediaTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan DockerTimeout = TimeSpan.FromSeconds(45);

    private readonly IDefaultBrowser? defaultBrowser;

    /// <param name="defaultBrowser">
    /// Which browser "Default browser" means. A profile set to it is planned as that browser when
    /// it is one that can be scripted, so its tabs can be checked for duplicates and closed.
    /// </param>
    public AutomationPlanBuilder(IDefaultBrowser? defaultBrowser = null)
    {
        this.defaultBrowser = defaultBrowser;
    }

    /// <summary>
    /// Builds the automation plan for switching from <paramref name="previous"/> to <paramref name="target"/>.
    /// </summary>
    /// <param name="previous">The context being left, or <see langword="null"/> on the first switch.</param>
    /// <param name="target">
    /// The context being switched to, or <see langword="null"/> to turn <paramref name="previous"/>
    /// off: only the half of a switch that leaves it, with nothing entered after.
    /// </param>
    /// <param name="extras">Apps and tabs to close besides what the profiles say - chosen when asked.</param>
    /// <param name="ignoredApps">Apps never opened, closed or hidden, even when a profile lists them.</param>
    public AutomationPlan Build(
        ContextDefinition? previous,
        ContextDefinition? target,
        SwitchExtras? extras = null,
        IReadOnlyCollection<string>? ignoredApps = null)
    {
        extras ??= SwitchExtras.None;
        IReadOnlyCollection<string> ignored = ignoredApps ?? [];

        // Which steps must work is the entered profile's call; turning one off, the one being left's.
        SwitchPolicyConfig policy = (target ?? previous)?.SwitchPolicy ?? new SwitchPolicyConfig();
        HashSet<string> criticalTypes = new(policy.CriticalSteps, StringComparer.Ordinal);
        List<AutomationStep> steps = [];

        // Asked once a switch; Default stays Default for a browser that can't be scripted.
        BrowserKind systemDefault = this.defaultBrowser?.Get()?.Kind ?? BrowserKind.Default;
        BrowserKind Resolve(BrowserKind browser) => browser == BrowserKind.Default ? systemDefault : browser;

        if (previous is not null)
        {
            AddStopDockerResources(steps, previous, target, criticalTypes);

            // Before the apps: if the browser itself is quit as well, its tabs are closed first, so
            // they don't come back when it next restores its last session.
            AddCloseBrowserTabs(steps, previous, target, Resolve, criticalTypes);
        }

        // What the user chose to close of everything else open, then the profiles' own.
        AddCloseOtherTabs(steps, extras, previous?.Id ?? target?.Id ?? string.Empty, criticalTypes);
        AddCloseApplications(steps, previous, target, extras, ignored, criticalTypes);
        if (previous is not null)
        {
            AddHideApplications(steps, previous, target, ignored, criticalTypes);
        }

        AddSetFocusMode(steps, previous, target, criticalTypes);

        if (target is not null)
        {
            AddLaunchApplications(steps, target, ignored, criticalTypes);
            AddManageBrowserContext(steps, target, Resolve, criticalTypes);
            AddStartDockerResources(steps, target, criticalTypes);
            AddControlMedia(steps, target, criticalTypes);
        }

        return new AutomationPlan(target?.Id ?? string.Empty, previous?.Id, steps);
    }

    /// <summary>A container the profile being entered starts again is left running.</summary>
    private static void AddStopDockerResources(
        List<AutomationStep> steps,
        ContextDefinition previous,
        ContextDefinition? target,
        HashSet<string> criticalTypes)
    {
        List<string> containers = previous.Docker.Stop
            .Except(target?.Docker.Start ?? [], StringComparer.Ordinal)
            .ToList();
        if (containers.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.StopDockerResources,
            previous.Id,
            "Stop Docker resources",
            DockerTimeout,
            criticalTypes,
            new Dictionary<string, string> { ["containers"] = List(containers) }));
    }

    /// <summary>
    /// The tabs the profile being left lists to close, in its own browser. A page the profile being
    /// entered opens in the same browser is spared: closing it would only make it load again,
    /// losing whatever was done in it. A default browser that can't be scripted is still planned -
    /// the step then says why nothing closed.
    /// </summary>
    private static void AddCloseBrowserTabs(
        List<AutomationStep> steps,
        ContextDefinition previous,
        ContextDefinition? target,
        Func<BrowserKind, BrowserKind> resolve,
        HashSet<string> criticalTypes)
    {
        BrowserManagementConfig leaving = previous.BrowserManagement;
        if (leaving.Mode != BrowserManagementMode.Urls || leaving.CloseUrls.Count == 0)
        {
            return;
        }

        BrowserKind browser = resolve(leaving.Browser);
        BrowserManagementConfig? entering = target?.BrowserManagement;
        IReadOnlyList<string> keep = entering is { Mode: BrowserManagementMode.Urls } && resolve(entering.Browser) == browser
            ? entering.Urls
            : [];

        steps.Add(CreateStep(
            AutomationStepType.CloseBrowserTabs,
            previous.Id,
            "Close browser tabs",
            CloseTabsTimeout,
            criticalTypes,
            new Dictionary<string, string>
            {
                ["contextId"] = previous.Id,
                ["browser"] = browser.ToString(),
                ["closeUrlsJson"] = JsonSerializer.Serialize(leaving.CloseUrls, ContextSwitcherJson.CompactOptions),
                ["keepUrlsJson"] = JsonSerializer.Serialize(keep, ContextSwitcherJson.CompactOptions)
            }));
    }

    /// <summary>
    /// An app the profile being entered launches is left running: quitting it only to open it
    /// again lost its windows and work, and made the switch wait for both.
    /// </summary>
    private static void AddCloseApplications(
        List<AutomationStep> steps,
        ContextDefinition? previous,
        ContextDefinition? target,
        SwitchExtras extras,
        IReadOnlyCollection<string> ignored,
        HashSet<string> criticalTypes)
    {
        List<string> apps = (previous?.CloseApps ?? [])
            .Concat(extras.CloseApps)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Except(target?.LaunchApps ?? [], StringComparer.OrdinalIgnoreCase)
            .Except(ignored, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (apps.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.CloseApplications,
            previous?.Id ?? target?.Id ?? string.Empty,
            "Close applications",
            QuitAppTimeoutPerApp * apps.Count,
            criticalTypes,
            new Dictionary<string, string> { ["apps"] = List(apps) }));
    }

    /// <summary>
    /// The apps the profile being left hides rather than quits. One the profile being entered
    /// launches is left alone: opening it would only bring it straight back.
    /// </summary>
    private static void AddHideApplications(
        List<AutomationStep> steps,
        ContextDefinition previous,
        ContextDefinition? target,
        IReadOnlyCollection<string> ignored,
        HashSet<string> criticalTypes)
    {
        List<string> apps = previous.HideApps
            .Except(target?.LaunchApps ?? [], StringComparer.OrdinalIgnoreCase)
            .Except(ignored, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (apps.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.HideApplications,
            previous.Id,
            "Hide applications",
            HideAppsTimeout,
            criticalTypes,
            new Dictionary<string, string> { ["apps"] = List(apps) }));
    }

    private static void AddSetFocusMode(
        List<AutomationStep> steps,
        ContextDefinition? previous,
        ContextDefinition? target,
        HashSet<string> criticalTypes)
    {
        // Turning Focus off is only worth doing when something turned it on. This step used to be
        // built unconditionally, so a user who had never set up the Shortcuts - which is everyone,
        // until they read section 9.8 - got "Could not run Shortcut 'ContextSwitcher - Focus Off'"
        // on every single switch, and every switch exited 5 instead of 0.
        //
        // The previous context is what decides it: leaving a profile that enabled Focus still needs
        // clearing, even though the target doesn't want Focus itself.
        bool targetUsesFocus = target is not null && UsesFocus(target);
        if (!targetUsesFocus && (previous is null || !UsesFocus(previous)))
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.SetFocusMode,
            (target ?? previous)!.Id,
            "Set Focus mode",
            FocusTimeout,
            criticalTypes,
            new Dictionary<string, string>
            {
                ["enabled"] = targetUsesFocus.ToString(),
                ["modeName"] = target?.Focus.ModeName ?? string.Empty,

                // What to turn off when the target wants no Focus: the mode the profile being left
                // turned on, each having its own Off Shortcut.
                ["previousModeName"] = previous is not null && UsesFocus(previous) ? previous.Focus.ModeName.Trim() : string.Empty
            }));
    }

    /// <summary>
    /// Whether a profile turns a Focus on. Enabled with no mode named turns nothing on - a config
    /// left that way used to count as using Focus, so every switch to it ran the "Off" Shortcut and
    /// warned that it was missing, for a profile that never had a Focus at all.
    /// </summary>
    private static bool UsesFocus(ContextDefinition context) =>
        context.Focus.Enabled && !string.IsNullOrWhiteSpace(context.Focus.ModeName);

    private static void AddLaunchApplications(
        List<AutomationStep> steps,
        ContextDefinition target,
        IReadOnlyCollection<string> ignored,
        HashSet<string> criticalTypes)
    {
        List<string> apps = target.LaunchApps.Except(ignored, StringComparer.OrdinalIgnoreCase).ToList();
        if (apps.Count == 0)
        {
            return;
        }

        steps.Add(CreateStep(
            AutomationStepType.LaunchApplications,
            target.Id,
            "Launch applications",
            LaunchAppTimeoutPerApp * apps.Count,
            criticalTypes,
            new Dictionary<string, string> { ["apps"] = List(apps) }));
    }

    /// <summary>
    /// The tabs the user chose to close of the ones no profile had, a step per browser. They go by
    /// their own addresses, and with no profile's remembered tabs: those are the profile's own step's.
    /// </summary>
    private static void AddCloseOtherTabs(List<AutomationStep> steps, SwitchExtras extras, string subjectId, HashSet<string> criticalTypes)
    {
        foreach (IGrouping<BrowserKind, TabToClose> browser in extras.CloseTabs.GroupBy(tab => tab.Browser))
        {
            steps.Add(CreateStep(
                AutomationStepType.CloseBrowserTabs,
                $"{subjectId}.others.{browser.Key}",
                "Close other browser tabs",
                CloseTabsTimeout,
                criticalTypes,
                new Dictionary<string, string>
                {
                    ["browser"] = browser.Key.ToString(),
                    ["closeUrlsJson"] = JsonSerializer.Serialize(browser.Select(tab => tab.Url).Distinct(StringComparer.Ordinal).ToList(), ContextSwitcherJson.CompactOptions),
                    ["keepUrlsJson"] = "[]"
                }));
        }
    }

    private static void AddManageBrowserContext(
        List<AutomationStep> steps,
        ContextDefinition target,
        Func<BrowserKind, BrowserKind> resolve,
        HashSet<string> criticalTypes)
    {
        BrowserManagementConfig browser = target.BrowserManagement;

        // A pages profile can list pages only to close them, with none to open.
        bool opensSomething = browser.Mode switch
        {
            BrowserManagementMode.Urls => browser.Urls.Count > 0,
            BrowserManagementMode.Profiles => true,
            _ => false
        };
        if (!opensSomething)
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
                ["contextId"] = target.Id,
                ["mode"] = browser.Mode.ToString(),
                ["browser"] = resolve(browser.Browser).ToString(),
                ["urls"] = List(browser.Urls),

                // The pages that close on leaving: the tabs opened for them are remembered, in a
                // browser that can say which tab is which, so they close wherever they have gone.
                ["rememberUrlsJson"] = List(browser.Mode == BrowserManagementMode.Urls ? browser.CloseUrls : []),
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
            new Dictionary<string, string> { ["containers"] = List(target.Docker.Start) }));
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

    /// <summary>
    /// A list argument, as JSON. Lists used to be joined with commas, which split any address that
    /// held one - a map's coordinates, a query's values - into pieces that opened nothing.
    /// </summary>
    private static string List(IReadOnlyList<string> values) =>
        JsonSerializer.Serialize(values, ContextSwitcherJson.CompactOptions);

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
