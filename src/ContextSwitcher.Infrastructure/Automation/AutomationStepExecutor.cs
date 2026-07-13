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
/// (Docker, Focus, media - later phases) are reported as <c>Skipped</c> rather than throwing, so
/// the pipeline stays inspectable end to end even before every phase lands.
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
            _ => NotImplemented(step)
        };
    }

    private async Task<AutomationResult> CloseApplicationsAsync(AutomationStep step, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = this.clock.UtcNow;
        string[] apps = SplitArgument(step.Arguments, "apps");
        List<string> stillRunning = [];
        StringBuilder standardError = new();

        foreach (string app in apps)
        {
            ProcessResult quitResult = await this.scriptRunner
                .RunAsync(AppleScriptBuilder.QuitApplicationIfRunning(app), step.Timeout, cancellationToken)
                .ConfigureAwait(false);
            AppendIfPresent(standardError, app, quitResult.StandardError);

            ProcessResult checkResult = await this.scriptRunner
                .RunAsync(AppleScriptBuilder.IsApplicationRunning(app), step.Timeout, cancellationToken)
                .ConfigureAwait(false);

            if (checkResult.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
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

        foreach (string app in apps)
        {
            ProcessStartOptions options = new("open", ["-a", app], step.Timeout);
            ProcessResult result = await this.processRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                failedApps.Add(app);
                AppendIfPresent(standardError, app, result.StandardError);
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
            .RunAsync(AppleScriptBuilder.SetDarkMode(dark), step.Timeout, cancellationToken)
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

        ProcessResult result = await this.scriptRunner
            .RunAsync(AppleScriptBuilder.SetWallpaper(path, allSpaces), step.Timeout, cancellationToken)
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

        BrowserContextRequest request = new(mode, browser, urls, tabGroups, avoidDuplicateTabs, profiles, step.Timeout);
        BrowserLaunchOutcome outcome = await this.browserLauncher.ManageBrowserContextAsync(request, cancellationToken).ConfigureAwait(false);

        DateTimeOffset completedAt = this.clock.UtcNow;
        if (outcome.Warnings.Count == 0)
        {
            return Succeeded(step, "Browser context managed.", startedAt, completedAt);
        }

        return Degraded(step, string.Join(' ', outcome.Warnings), standardError: string.Empty, startedAt, completedAt);
    }

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
