using System.Globalization;
using System.Text;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.App.Services;

/// <summary>
/// What someone is asked to send when the app misbehaves, made in one click: which app on which Mac,
/// what is switched on, and the recent log - so no one has to be told which commands to run to find
/// out. It holds counts and settings, never a profile's name, a web address or an email: the log in it
/// is cleaned again here, since an older version wrote some of those.
/// </summary>
public static class DiagnosticsReport
{
    /// <summary>How many lines of the log file are included.</summary>
    public const int LogLines = 60;

    public static string Build(DiagnosticsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        AppConfiguration config = input.Configuration;
        StringBuilder report = new();
        report.AppendLine("Context Switcher diagnostics");
        report.AppendLine(CultureInfo.InvariantCulture, $"Made {input.Now:yyyy-MM-dd HH:mm 'UTC'}");
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"Version: {input.Run.Version} ({input.Run.Build})");
        report.AppendLine(CultureInfo.InvariantCulture, $"macOS: {input.Run.MacOs} ({input.Run.Architecture})");
        report.AppendLine(CultureInfo.InvariantCulture, $"Language: {config.Language}, appearance: {config.Appearance}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Automation permission: {Describe(input.Automation)}");
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"Profiles: {config.Contexts.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  browser: pages {Count(config, BrowserManagementMode.Urls)}, browser profiles {Count(config, BrowserManagementMode.Profiles)}, none {Count(config, BrowserManagementMode.None)}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  apps to open {config.Contexts.Sum(c => c.LaunchApps.Count)}, to close {config.Contexts.Sum(c => c.CloseApps.Count)}, to hide {config.Contexts.Sum(c => c.HideApps.Count)}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Ask about other apps and tabs: {OnOff(config.AskOnSwitch)}, never-touch apps: {config.IgnoredApps.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Check for updates: {OnOff(config.CheckForUpdates)}, show in Dock: {OnOff(config.ShowDockIcon)}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Time tracking: {OnOff(config.Analytics.Enabled)} ({config.Analytics.RetentionDays} days)");
        report.AppendLine(CultureInfo.InvariantCulture, $"Detailed log: {(config.DetailedLogUntil is { } until && until > input.Now ? $"on until {until:yyyy-MM-dd}" : "off")}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Problems in the settings: {input.ConfigurationProblems}");
        report.AppendLine();

        report.AppendLine(CultureInfo.InvariantCulture, $"Log (the last {LogLines} lines of app.log.jsonl; empty when nothing went wrong this week)");
        if (input.LogLines.Count == 0)
        {
            report.AppendLine("  (nothing)");
        }

        foreach (string line in input.LogLines.TakeLast(LogLines))
        {
            report.AppendLine(LogRedactor.Redact(line));
        }

        report.AppendLine();
        report.AppendLine("Recent activity not written to the log, because nothing went wrong:");
        if (input.Pending.Count == 0)
        {
            report.AppendLine("  (nothing)");
        }

        foreach (LogEntry entry in input.Pending)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"  {entry.Timestamp:HH:mm:ss} {entry.Level,-11} {entry.Category}/{entry.EventId}: {entry.Message}");
        }

        return report.ToString();
    }

    private static int Count(AppConfiguration config, BrowserManagementMode mode) =>
        config.Contexts.Count(context => context.BrowserManagement.Mode == mode);

    private static string OnOff(bool on) => on ? "on" : "off";

    private static string Describe(bool? granted) => granted switch
    {
        true => "granted",
        false => "not granted",
        null => "not known"
    };
}

/// <summary>What <see cref="DiagnosticsReport.Build"/> is made from.</summary>
public sealed record DiagnosticsInput(
    RunInfo Run,
    AppConfiguration Configuration,
    bool? Automation,
    int ConfigurationProblems,
    IReadOnlyList<string> LogLines,
    IReadOnlyList<LogEntry> Pending,
    DateTimeOffset Now);
