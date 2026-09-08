using System.Text;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Serialization;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.Browser;

namespace ContextSwitcher.Infrastructure.Automation;

/// <summary>
/// Maps an <see cref="AutomationStepType"/> and its <see cref="AutomationStep.Arguments"/> to real
/// <see cref="IProcessRunner"/>/<see cref="IScriptRunner"/> calls. Step types not implemented yet
/// (open-links, write-state, analytics-boundary steps never appear in the built plan) are reported
/// as <c>Skipped</c> rather than throwing, so the pipeline stays inspectable end to end.
/// </summary>
public sealed class AutomationStepExecutor : IAutomationStepExecutor
{
    private readonly IProcessRunner processRunner;
    private readonly IScriptRunner scriptRunner;
    private readonly BrowserLauncher browserLauncher;
    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomationStepExecutor"/> class.
    /// </summary>
    public AutomationStepExecutor(IProcessRunner processRunner, IScriptRunner scriptRunner, BrowserLauncher browserLauncher, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(scriptRunner);
        ArgumentNullException.ThrowIfNull(browserLauncher);
        ArgumentNullException.ThrowIfNull(clock);

        this.processRunner = processRunner;
        this.scriptRunner = scriptRunner;
        this.browserLauncher = browserLauncher;
        this.clock = clock;
    }

    /// <inheritdoc />
    public Task<AutomationResult> ExecuteAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.Type switch
        {
            AutomationStepType.CloseApplications => this.CloseApplicationsAsync(step, cancellationToken),
            AutomationStepType.LaunchApplications => this.LaunchApplicationsAsync(step, cancellationToken),
            AutomationStepType.SetTheme => this.SetThemeAsync(step, cancellationToken),
            AutomationStepType.SetWallpaper => this.SetWallpaperAsync(step, cancellationToken),
            AutomationStepType.ManageBrowserContext => this.ManageBrowserContextAsync(step, cancellationToken),
            AutomationStepType.StartDockerResources => this.RunDockerCommandAsync(step, "start", cancellationToken),
            AutomationStepType.StopDockerResources => this.RunDockerCommandAsync(step, "stop", cancellationToken),
            AutomationStepType.ControlMedia => this.ControlMediaAsync(step, cancellationToken),
            AutomationStepType.SetFocusMode => this.SetFocusModeAsync(step, cancellationToken),
            _ => NotImplemented(step)
        };
    }

    private async Task<AutomationResult> CloseApplicationsAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        string[] apps = SplitArgument(step.Arguments, "apps");
        List<string> stillRunning = [];
        StringBuilder standardError = new();

        // Section 9.1 budgets ten seconds per app, which the plan builder encodes as
        // Timeout = 10s x app count. Split it back out: a graceful quit can block indefinitely on a
        // modal "save this document?" sheet, and handing every script the whole step budget meant
        // one such app consumed all of it. The step-level timeout then fired mid-quit, so the
        // warning below never ran and every later app in the list was never even asked to quit.
        TimeSpan perApp = step.Timeout / Math.Max(apps.Length, 1);
        TimeSpan checkTimeout = perApp / 5;
        TimeSpan quitTimeout = perApp - (2 * checkTimeout);

        // The apps are quit concurrently. The budget above is already per app, so running them
        // together makes the step cost the slowest single quit instead of the sum of all of them -
        // on six apps that is the difference between about 1.2s and 0.2s. They stay one script per
        // app rather than one script for all of them precisely so an app sitting on a modal "save
        // this document?" sheet still only spends its own budget, which is what the split above
        // exists to guarantee. Task.WhenAll preserves the input order, so the message below names
        // the apps in configured order however the quits interleave.
        (string App, string Error, bool StillRunning)[] outcomes = await Task.WhenAll(
            apps.Select(async app =>
            {
                ProcessResult quitResult = await this.scriptRunner
                    .RunAsync(AppleScriptBuilder.QuitApplicationIfRunning(app), quitTimeout, cancellationToken)
                    .ConfigureAwait(false);

                ProcessResult checkResult = await this.scriptRunner
                    .RunAsync(AppleScriptBuilder.IsApplicationRunning(app), checkTimeout, cancellationToken)
                    .ConfigureAwait(false);

                return (app, quitResult.StandardError,
                    checkResult.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
            })).ConfigureAwait(false);

        foreach ((string app, string error, bool isStillRunning) in outcomes)
        {
            AppendIfPresent(standardError, app, error);

            if (isStillRunning)
            {
                stillRunning.Add(app);
            }
        }

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (stillRunning.Count == 0)
        {
            return Succeeded(step, "All applications closed.", startedAt, completedAt);
        }

        return Degraded(
            step,
            $"Could not close: {string.Join(", ", stillRunning)}.",
            standardError.ToString(),
            startedAt,
            completedAt);
    }

    private async Task<AutomationResult> LaunchApplicationsAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        string[] apps = SplitArgument(step.Arguments, "apps");
        List<string> failedApps = [];
        StringBuilder standardError = new();

        // Same per-app split as CloseApplicationsAsync: the plan builds 15s x app count, so no
        // single slow launch may spend the budget the apps after it still need.
        TimeSpan perApp = WithReportingHeadroom(step.Timeout) / Math.Max(apps.Length, 1);

        // Launched concurrently for the same reason as the quits above: `open -a` calls are
        // independent of one another, and the per-app budget means running them together costs the
        // slowest launch rather than their total.
        (string App, string Error, bool Failed)[] outcomes = await Task.WhenAll(
            apps.Select(async app =>
            {
                ProcessStartOptions options = new("open", ["-a", app], perApp);
                ProcessResult result = await this.processRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);
                return (app, result.StandardError, result.ExitCode != 0);
            })).ConfigureAwait(false);

        foreach ((string app, string error, bool failed) in outcomes)
        {
            if (failed)
            {
                failedApps.Add(app);
                AppendIfPresent(standardError, app, error);
            }
        }

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (failedApps.Count == 0)
        {
            return Succeeded(step, "All applications launched.", startedAt, completedAt);
        }

        return Degraded(
            step,
            $"Could not launch: {string.Join(", ", failedApps)}.",
            standardError.ToString(),
            startedAt,
            completedAt);
    }

    private async Task<AutomationResult> SetThemeAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        bool dark = step.Arguments.GetValueOrDefault("mode") == "Dark";

        ProcessResult result = await this.scriptRunner
            .RunAsync(AppleScriptBuilder.SetDarkMode(dark), WithReportingHeadroom(step.Timeout), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return new AutomationResult(
                step.Id, step.Type, AutomationResultStatus.Succeeded, "Theme updated.",
                result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt);
        }

        AutomationResultStatus status = step.IsCritical ? AutomationResultStatus.Failed : AutomationResultStatus.Warning;
        return new AutomationResult(
            step.Id, step.Type, status, "Could not change theme. Check Automation permissions.",
            result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt);
    }

    private async Task<AutomationResult> SetWallpaperAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        string path = step.Arguments.GetValueOrDefault("path", string.Empty);
        bool allSpaces = step.Arguments.GetValueOrDefault("allSpaces") == "True";

        if (!File.Exists(path))
        {
            DateTimeOffset missingCompletedAt = this.clock.UtcNow;
            return new AutomationResult(
                step.Id, step.Type, AutomationResultStatus.Warning, $"Wallpaper file not found: '{path}'.",
                null, null, null, startedAt, missingCompletedAt);
        }

        // System Events accepts any path at all - point it at a text file and it reports success
        // while the desktop is left referencing something that cannot be drawn. `sips` is already
        // on the command allowlist for image work, and reading an image property is the cheapest
        // way to ask "is this actually an image?" before committing to it.
        ProcessResult imageProbe = await this.processRunner
            .RunAsync(new ProcessStartOptions("sips", ["-g", "pixelWidth", path], WithReportingHeadroom(step.Timeout)), cancellationToken)
            .ConfigureAwait(false);

        if (imageProbe.ExitCode != 0 || !ReportsPixelWidth(imageProbe.StandardOutput))
        {
            return new AutomationResult(
                step.Id, step.Type, AutomationResultStatus.Warning, $"Wallpaper file is not a readable image: '{path}'.",
                imageProbe.ExitCode, imageProbe.StandardOutput, imageProbe.StandardError, startedAt, this.clock.UtcNow);
        }

        ProcessResult result = await this.scriptRunner
            .RunAsync(AppleScriptBuilder.SetWallpaper(path, allSpaces), WithReportingHeadroom(step.Timeout), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        // Missing wallpaper is a warning regardless of step criticality (section 9.7); other
        // failures still get here since the file-existence check above already short-circuited.
        AutomationResultStatus status = result.ExitCode == 0 ? AutomationResultStatus.Succeeded : AutomationResultStatus.Warning;
        string message = result.ExitCode == 0 ? "Wallpaper updated." : "Could not set wallpaper.";
        return new AutomationResult(
            step.Id, step.Type, status, message, result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt);
    }

    private async Task<AutomationResult> ManageBrowserContextAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;

        BrowserManagementMode mode = Enum.Parse<BrowserManagementMode>(step.Arguments.GetValueOrDefault("mode", nameof(BrowserManagementMode.None)));
        BrowserKind browser = Enum.Parse<BrowserKind>(step.Arguments.GetValueOrDefault("browser", nameof(BrowserKind.Default)));
        string[] urls = SplitArgument(step.Arguments, "urls");
        string[] tabGroups = SplitArgument(step.Arguments, "tabGroups");
        bool avoidDuplicateTabs = step.Arguments.GetValueOrDefault("avoidDuplicateTabs") == "True";
        IReadOnlyList<BrowserProfileConfig> profiles = DeserializeProfiles(step.Arguments.GetValueOrDefault("profilesJson", "[]"));

        BrowserContextRequest request = new(mode, browser, urls, tabGroups, avoidDuplicateTabs, profiles, WithReportingHeadroom(step.Timeout));
        BrowserLaunchOutcome outcome = await this.browserLauncher.ManageBrowserContextAsync(request, cancellationToken).ConfigureAwait(false);

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (outcome.Warnings.Count == 0)
        {
            return Succeeded(step, "Browser context managed.", startedAt, completedAt);
        }

        return Degraded(step, string.Join(' ', outcome.Warnings), standardError: string.Empty, startedAt, completedAt);
    }

    private async Task<AutomationResult> RunDockerCommandAsync(AutomationStep step, string dockerCommand, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        string[] containers = SplitArgument(step.Arguments, "containers");

        List<string> arguments = [dockerCommand, .. containers];
        ProcessResult result = await this.processRunner
            .RunAsync(new ProcessStartOptions("docker", arguments, WithReportingHeadroom(step.Timeout)), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return Succeeded(step, $"Docker containers {dockerCommand}ed.", startedAt, completedAt);
        }

        AutomationResultStatus status = step.IsCritical ? AutomationResultStatus.Failed : AutomationResultStatus.Warning;
        return new AutomationResult(
            step.Id, step.Type, status, $"Could not {dockerCommand} containers: {string.Join(", ", containers)}.",
            result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt);
    }

    private async Task<AutomationResult> ControlMediaAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        string player = step.Arguments.GetValueOrDefault("player", nameof(MediaPlayerKind.None));
        string playlist = step.Arguments.GetValueOrDefault("playlist", string.Empty);
        bool autoPlay = step.Arguments.GetValueOrDefault("autoPlay") == "True";

        if (!autoPlay)
        {
            return Succeeded(step, "Auto-play disabled; media not started.", startedAt, this.clock.UtcNow);
        }

        if (string.IsNullOrWhiteSpace(playlist))
        {
            return new AutomationResult(
                step.Id, step.Type, AutomationResultStatus.Warning, "No playlist configured.",
                null, null, null, startedAt, this.clock.UtcNow);
        }

        return player switch
        {
            nameof(MediaPlayerKind.AppleMusic) => await this.PlayAppleMusicAsync(step, playlist, startedAt, cancellationToken).ConfigureAwait(false),
            nameof(MediaPlayerKind.Spotify) => await this.PlaySpotifyAsync(step, playlist, startedAt, cancellationToken).ConfigureAwait(false),
            _ => Succeeded(step, "No media player configured.", startedAt, this.clock.UtcNow)
        };
    }

    private async Task<AutomationResult> PlayAppleMusicAsync(AutomationStep step, string playlist, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.scriptRunner
            .RunAsync(AppleScriptBuilder.PlayAppleMusicPlaylist(playlist), WithReportingHeadroom(step.Timeout), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return Succeeded(step, "Apple Music playlist started.", startedAt, completedAt);
        }

        AutomationResultStatus status = step.IsCritical ? AutomationResultStatus.Failed : AutomationResultStatus.Warning;
        return new AutomationResult(
            step.Id, step.Type, status, $"Could not play Apple Music playlist '{playlist}'.",
            result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt);
    }

    private async Task<AutomationResult> PlaySpotifyAsync(AutomationStep step, string playlist, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        ProcessResult result = await this.scriptRunner
            .RunAsync(AppleScriptBuilder.PlaySpotify(playlist), WithReportingHeadroom(step.Timeout), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return Succeeded(step, "Spotify playback started.", startedAt, completedAt);
        }

        // Spotify automation failures are always non-critical (section 9.10), regardless of step.IsCritical.
        return new AutomationResult(
            step.Id, step.Type, AutomationResultStatus.Warning, $"Could not start Spotify playback for '{playlist}'.",
            result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt);
    }

    private async Task<AutomationResult> SetFocusModeAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        bool enabled = step.Arguments.GetValueOrDefault("enabled") == "True";
        string modeName = step.Arguments.GetValueOrDefault("modeName", string.Empty);

        string shortcutName = enabled && !string.IsNullOrWhiteSpace(modeName)
            ? $"ContextSwitcher - Focus {modeName}"
            : "ContextSwitcher - Focus Off";

        ProcessResult result = await this.processRunner
            .RunAsync(new ProcessStartOptions("shortcuts", ["run", shortcutName], WithReportingHeadroom(step.Timeout)), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset completedAt = this.clock.UtcNow;

        if (result.ExitCode == 0)
        {
            return Succeeded(step, $"Ran Shortcut '{shortcutName}'.", startedAt, completedAt);
        }

        AutomationResultStatus status = step.IsCritical ? AutomationResultStatus.Failed : AutomationResultStatus.Warning;
        return new AutomationResult(
            step.Id, step.Type, status,
            $"Could not run Shortcut '{shortcutName}'. Create it in the Shortcuts app (see docs/shortcuts-integration.md) or check Shortcuts permissions.",
            result.ExitCode, result.StandardOutput, result.StandardError, startedAt, completedAt);
    }

    /// <summary>
    /// Whether <c>sips -g pixelWidth</c> reported a real width. It exits 0 even for a text file,
    /// printing <c>pixelWidth: &lt;nil&gt;</c>, so the exit code alone says nothing - only the value does.
    /// </summary>
    private static bool ReportsPixelWidth(string? sipsOutput)
    {
        const string Marker = "pixelWidth:";

        if (string.IsNullOrWhiteSpace(sipsOutput))
        {
            return false;
        }

        int start = sipsOutput.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest = sipsOutput.AsSpan(start + Marker.Length);
        int lineEnd = rest.IndexOf('\n');
        ReadOnlySpan<char> value = (lineEnd >= 0 ? rest[..lineEnd] : rest).Trim();

        return int.TryParse(value, out int width) && width > 0;
    }

    /// <summary>
    /// Trims a step's budget before handing it to the process or script it wraps, so the call
    /// always returns before <c>ContextSwitchService</c>'s step-level timeout fires. Without the
    /// margin a hung command consumed the entire step and cancellation unwound past the code that
    /// turns a failure into a specific, actionable message - a browser that never answered, or a
    /// Shortcut that does not exist, both surfaced as a bare "step timed out" naming nothing.
    /// </summary>
    private static TimeSpan WithReportingHeadroom(TimeSpan budget) => budget * 0.8;

    private static IReadOnlyList<BrowserProfileConfig> DeserializeProfiles(string profilesJson)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<BrowserProfileConfig>>(profilesJson, ContextSwitcherJson.CompactOptions) ?? [];
    }

    private static Task<AutomationResult> NotImplemented(AutomationStep step)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Task.FromResult(new AutomationResult(
            step.Id, step.Type, AutomationResultStatus.Skipped,
            $"{step.Type} automation is not implemented yet.", null, null, null, now, now));
    }

    private static AutomationResult Succeeded(AutomationStep step, string message, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        return new AutomationResult(step.Id, step.Type, AutomationResultStatus.Succeeded, message, 0, null, null, startedAt, completedAt);
    }

    private static AutomationResult Degraded(
        AutomationStep step,
        string message,
        string standardError,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        AutomationResultStatus status = step.IsCritical ? AutomationResultStatus.Failed : AutomationResultStatus.Warning;
        return new AutomationResult(step.Id, step.Type, status, message, null, null, standardError, startedAt, completedAt);
    }

    private static void AppendIfPresent(StringBuilder builder, string app, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{app}: {value.Trim()}");
        }
    }

    private static string[] SplitArgument(IReadOnlyDictionary<string, string> arguments, string key)
    {
        return arguments.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            : [];
    }
}
