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

        foreach (string url in urls)
        {
            bool focused = avoidDuplicateTabs
                && await this.TryFocusExistingTabAsync(browser, url, timeout, cancellationToken).ConfigureAwait(false);

            if (focused)
            {
                continue;
            }

            ProcessResult result = await this.OpenUrlAsync(browser, url, timeout, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                warnings.Add($"Could not open '{url}': {result.StandardError.Trim()}");
            }
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

    private async Task<bool> TryFocusExistingTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (browser == BrowserKind.Default)
        {
            // The default browser is unknown ahead of time, so it cannot be targeted by AppleScript.
            return false;
        }

        string script = browser == BrowserKind.Safari
            ? AppleScriptBuilder.FocusSafariTabWithUrl(url)
            : AppleScriptBuilder.FocusChromiumTabWithUrl(BrowserAppName(browser), url);

        ProcessResult result = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private Task<ProcessResult> OpenUrlAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        List<string> arguments = browser == BrowserKind.Default
            ? [url]
            : ["-a", BrowserAppName(browser), url];

        return this.processRunner.RunAsync(new ProcessStartOptions("open", arguments, timeout), cancellationToken);
    }

    private static string BrowserAppName(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Brave => "Brave Browser",
        BrowserKind.Safari => "Safari",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, "Default browser has no fixed application name.")
    };
}
