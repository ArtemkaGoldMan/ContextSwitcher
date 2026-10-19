using System.Globalization;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Asks Safari, Chrome and Brave through AppleScript, and opens pages and profiles with <c>open</c>.
/// Chrome and Brave give their tabs ids, so their tabs are closed by id; Safari's by address.
/// </summary>
public sealed class MacBrowserControl : IBrowserControl
{
    private readonly IProcessRunner processRunner;
    private readonly IScriptRunner scriptRunner;

    public MacBrowserControl(IProcessRunner processRunner, IScriptRunner scriptRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(scriptRunner);

        this.processRunner = processRunner;
        this.scriptRunner = scriptRunner;
    }

    /// <inheritdoc />
    public string AppName(BrowserKind browser) => MacBrowserApps.Name(browser);

    /// <inheritdoc />
    public async Task<TabListing> ListTabsAsync(BrowserKind browser, bool startIfNeeded, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string app = MacBrowserApps.Name(browser);
        bool chromium = IsChromium(browser);
        string script = chromium
            ? AppleScriptBuilder.ListChromiumTabs(app, startIfNeeded)
            : startIfNeeded ? AppleScriptBuilder.ListTabUrls(app) : AppleScriptBuilder.ListOpenTabs(app, isSafari: true);

        ProcessResult listing = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);
        if (listing.ExitCode != 0)
        {
            return new TabListing(null, Problem(listing));
        }

        List<BrowserTab> tabs = [];
        foreach (string line in listing.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (chromium)
            {
                string[] fields = line.Split(AppleScriptBuilder.OpenTabFieldSeparator, 2);
                if (fields.Length == 2 && int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    tabs.Add(new BrowserTab(id, fields[1].Trim()));
                }
            }
            else
            {
                tabs.Add(new BrowserTab(null, line.Split(AppleScriptBuilder.OpenTabFieldSeparator)[0].Trim()));
            }
        }

        return new TabListing(tabs, null);
    }

    /// <inheritdoc />
    public async Task FocusTabAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string script = browser == BrowserKind.Safari
            ? AppleScriptBuilder.FocusSafariTabWithUrl(url)
            : AppleScriptBuilder.FocusChromiumTabWithUrl(MacBrowserApps.Name(browser), url);

        // Whether a tab was found, or the browser could be asked at all, changes nothing for the caller.
        _ = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TabsClosed> CloseTabsAsync(BrowserKind browser, IReadOnlyList<BrowserTab> tabs, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        string app = MacBrowserApps.Name(browser);
        string script = IsChromium(browser)
            ? AppleScriptBuilder.CloseChromiumTabsWithIds(app, tabs.Where(tab => tab.Id is not null).Select(tab => tab.Id!.Value).Distinct().ToList())
            : AppleScriptBuilder.CloseTabsWithUrls(app, tabs.Select(tab => tab.Url).Distinct(StringComparer.Ordinal).ToList());

        ProcessResult closing = await this.scriptRunner.RunAsync(script, timeout, cancellationToken).ConfigureAwait(false);
        if (closing.ExitCode != 0)
        {
            return new TabsClosed(null, Problem(closing));
        }

        // The script says how many it closed.
        return int.TryParse(closing.StandardOutput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int closed)
            ? new TabsClosed(closed, null)
            : new TabsClosed(null, null);
    }

    /// <inheritdoc />
    public async Task<BrowserProblem?> OpenPageAsync(BrowserKind browser, string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        List<string> arguments = browser == BrowserKind.Default
            ? [url]
            : ["-a", MacBrowserApps.Name(browser), url];

        ProcessResult result = await this.processRunner.RunAsync(new ProcessStartOptions("open", arguments, timeout), cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 ? null : Problem(result);
    }

    /// <inheritdoc />
    public async Task<BrowserProblem?> OpenProfileAsync(
        BrowserKind browser, string profileDirectory, IReadOnlyList<string> urls, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Without -n, a browser that is running would only be brought forward, and never see the arguments.
        List<string> arguments = ["-na", MacBrowserApps.Name(browser), "--args", $"--profile-directory={profileDirectory}", .. urls];

        ProcessResult result = await this.processRunner.RunAsync(new ProcessStartOptions("open", arguments, timeout), cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0 ? null : Problem(result);
    }

    /// <summary>Chrome and Brave: their tabs are listed and closed with their ids.</summary>
    private static bool IsChromium(BrowserKind browser) => KnownBrowsers.KeepsTabIds(browser);

    /// <summary>What kind of failure the script's was, from what it said.</summary>
    private static BrowserProblem Problem(ProcessResult failure)
    {
        string reason = failure.StandardError.Trim();
        if (failure.TimedOut)
        {
            return new BrowserProblem(BrowserProblemKind.NotAnswering, reason);
        }

        // -1743 is errAEEventNotPermitted: Automation is off for this app, or was refused.
        return reason.Contains("-1743", StringComparison.Ordinal) || reason.Contains("Not authorized", StringComparison.OrdinalIgnoreCase)
            ? new BrowserProblem(BrowserProblemKind.NotAllowed, reason)
            : new BrowserProblem(BrowserProblemKind.Failed, reason);
    }
}
