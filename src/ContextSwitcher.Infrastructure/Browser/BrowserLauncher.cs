using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Implements the three browser management modes from agent.md section 9.3-9.5: opening URLs
/// (with best-effort duplicate-tab avoidance), activating named tab groups, and launching
/// Chromium browser profiles.
/// </summary>
public sealed class BrowserLauncher
{
    /// <summary>
    /// Duplicate-tab inspection is a best-effort nicety, so it gets a small slice of the step's
    /// budget rather than all of it. A browser that answers AppleEvents at all answers in
    /// milliseconds; one that does not (no Automation permission, or a wedged process) blocks until
    /// the script is killed. Handing it the whole step timeout used to starve the plain
    /// <c>open</c> fallback that agent.md section 9.3 requires, so no URL opened at all.
    /// </summary>
    private static readonly TimeSpan TabProbeTimeout = TimeSpan.FromSeconds(3);

    private readonly IProcessRunner processRunner;
    private readonly IScriptRunner scriptRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserLauncher"/> class.
    /// </summary>
    public BrowserLauncher(IProcessRunner processRunner, IScriptRunner scriptRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(scriptRunner);

        this.processRunner = processRunner;
        this.scriptRunner = scriptRunner;
    }

    /// <summary>
    /// Manages the browser context for a switch according to <paramref name="request"/>'s mode.
    /// </summary>
    public Task<BrowserLaunchOutcome> ManageBrowserContextAsync(BrowserContextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Mode switch
        {
            BrowserManagementMode.Urls => this.OpenUrlsAsync(request.Browser, request.Urls, request.AvoidDuplicateTabs, request.Timeout, cancellationToken),
            BrowserManagementMode.Groups => this.ActivateTabGroupsAsync(request.Browser, request.TabGroups, request.Urls, request.Timeout, cancellationToken),
            BrowserManagementMode.Profiles => this.LaunchProfilesAsync(request.Profiles, request.Timeout, cancellationToken),
            _ => Task.FromResult(BrowserLaunchOutcome.Empty)
        };
    }

    private async Task<BrowserLaunchOutcome> OpenUrlsAsync(
        BrowserKind browser,
        IReadOnlyList<string> urls,
        bool avoidDuplicateTabs,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        List<string> warnings = [];

        // The default browser is unknown ahead of time, so it cannot be targeted by AppleScript.
        bool probeTabs = avoidDuplicateTabs && browser != BrowserKind.Default;
        TimeSpan probeTimeout = timeout < TabProbeTimeout ? timeout : TabProbeTimeout;

        // One sweep of the browser's tabs for the whole context. This used to be a tab sweep per
        // URL, which is what made a three-URL context spend about 490ms deciding what to open even
        // when every tab was already there; a single listing costs about 130ms and does not grow
        // with the URL count.
        HashSet<string> openTabs = [];
        if (probeTabs)
        {
            ProcessResult listing = await this.scriptRunner
                .RunAsync(AppleScriptBuilder.ListTabUrls(BrowserAppName(browser)), probeTimeout, cancellationToken)
                .ConfigureAwait(false);

            // A non-zero exit means the inspection itself did not run - a timeout, a denied
            // Automation prompt, or a browser without tab scripting - which is different from
            // "no matching tab". Section 9.3 says to fall back to a plain `open` for everything.
            if (listing.ExitCode == 0)
            {
                foreach (string tabUrl in listing.StandardOutput
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    openTabs.Add(tabUrl);
                }
            }
            else
            {
                probeTabs = false;
                warnings.Add(
                    $"Could not check {BrowserAppName(browser)} for existing tabs, so URLs were opened without duplicate checking. Check Automation permissions.");
            }
        }

        // The last configured URL that was already open with nothing opened after it. Focusing just
        // that one at the end leaves the browser where the old per-URL probe left it, which focused
        // every match in turn and so ended on the last action, without paying for the ones whose
        // focus a later `open` would immediately have replaced.
        string? tabToFocus = null;

        foreach (string url in urls)
        {
            if (probeTabs && openTabs.Contains(url))
            {
                tabToFocus = url;
                continue;
            }

            ProcessResult result = await this.OpenUrlAsync(browser, url, timeout, cancellationToken).ConfigureAwait(false);
            tabToFocus = null;

            if (result.ExitCode != 0)
            {
                warnings.Add($"Could not open '{url}': {result.StandardError.Trim()}");
            }
        }

        if (tabToFocus is not null)
        {
            await this.TryFocusExistingTabAsync(browser, tabToFocus, probeTimeout, cancellationToken).ConfigureAwait(false);
        }

        return new BrowserLaunchOutcome(warnings);
    }

    private async Task<BrowserLaunchOutcome> ActivateTabGroupsAsync(
        BrowserKind browser,
        IReadOnlyList<string> tabGroups,
        IReadOnlyList<string> fallbackUrls,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        List<string> warnings = [];
        bool anyGroupFailed = false;

        foreach (string group in tabGroups)
        {
            string script = browser == BrowserKind.Safari
                ? AppleScriptBuilder.ActivateSafariTabGroup(group)
                : AppleScriptBuilder.ActivateChromiumTabGroup(BrowserAppName(browser), group);

            ProcessResult result = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);
            bool activated = result.ExitCode == 0 && result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

            if (!activated)
            {
                anyGroupFailed = true;
                warnings.Add($"Could not activate tab group '{group}'. Browser tab group scripting may not be supported on this macOS version.");
            }
        }

        if (anyGroupFailed && fallbackUrls.Count > 0)
        {
            BrowserLaunchOutcome fallbackOutcome = await this.OpenUrlsAsync(browser, fallbackUrls, avoidDuplicateTabs: false, timeout, cancellationToken)
                .ConfigureAwait(false);
            warnings.AddRange(fallbackOutcome.Warnings);
        }

        return new BrowserLaunchOutcome(warnings);
    }

    private async Task<BrowserLaunchOutcome> LaunchProfilesAsync(
        IReadOnlyList<BrowserProfileConfig> profiles,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        List<string> warnings = [];

        foreach (BrowserProfileConfig profile in profiles)
        {
            List<string> arguments =
            [
                "-na",
                BrowserAppName(profile.Browser),
                "--args",
                $"--profile-directory={profile.ProfileDirectory}"
            ];
            arguments.AddRange(profile.Urls);

            ProcessResult result = await this.processRunner
                .RunAsync(new ProcessStartOptions("open", arguments, timeout), cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                warnings.Add($"Could not launch {profile.Browser} profile '{profile.ProfileDirectory}': {result.StandardError.Trim()}");
            }
        }

        return new BrowserLaunchOutcome(warnings);
    }

    private async Task<TabProbeOutcome> TryFocusExistingTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string script = browser == BrowserKind.Safari
            ? AppleScriptBuilder.FocusSafariTabWithUrl(url)
            : AppleScriptBuilder.FocusChromiumTabWithUrl(BrowserAppName(browser), url);

        ProcessResult result = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);

        // A non-zero exit means the inspection itself did not run - a timeout, a denied Automation
        // prompt, or a browser without tab scripting - which is different from "no matching tab".
        if (result.ExitCode != 0)
        {
            return TabProbeOutcome.InspectionFailed;
        }

        return result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            ? TabProbeOutcome.Focused
            : TabProbeOutcome.NotFound;
    }

    private Task<ProcessResult> OpenUrlAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        List<string> arguments = browser == BrowserKind.Default
            ? [url]
            : ["-a", BrowserAppName(browser), url];

        return this.processRunner.RunAsync(new ProcessStartOptions("open", arguments, timeout), cancellationToken);
    }

    /// <summary>
    /// Why a duplicate-tab probe did not end in "focus this tab, skip the open".
    /// </summary>
    private enum TabProbeOutcome
    {
        /// <summary>A matching tab was found and focused; the URL must not be opened again.</summary>
        Focused,

        /// <summary>The inspection ran and found no matching tab.</summary>
        NotFound,

        /// <summary>The inspection could not run at all, so nothing is known about existing tabs.</summary>
        InspectionFailed
    }

    private static string BrowserAppName(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Brave => "Brave Browser",
        BrowserKind.Safari => "Safari",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, "Default browser has no fixed application name.")
    };
}
