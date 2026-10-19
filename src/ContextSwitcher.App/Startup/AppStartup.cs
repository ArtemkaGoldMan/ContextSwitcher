using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// What happens before the first window, or before the command line runs: the log is tidied, the
/// settings are loaded - or a default written, with a file that can't be read moved aside - and
/// checked, where the app stood is loaded, and both are published in <see cref="IAppState"/>. The app,
/// though not the command line, then recovers the time history a crash left, clears out what is past
/// keeping, and starts counting time for the active profile.
/// </summary>
public sealed class AppStartup
{
    private readonly IAppState appState;
    private readonly ConfigPaths configPaths;
    private readonly IJsonStore jsonStore;
    private readonly ConfigurationValidator validator;
    private readonly IAnalyticsService analytics;
    private readonly LocalJsonLogger log;
    private readonly ILogger logger;
    private readonly IClock clock;

    public AppStartup(
        IAppState appState,
        ConfigPaths configPaths,
        IJsonStore jsonStore,
        ConfigurationValidator validator,
        IAnalyticsService analytics,
        LocalJsonLogger log,
        ILogger logger,
        IClock clock)
    {
        this.appState = appState;
        this.configPaths = configPaths;
        this.jsonStore = jsonStore;
        this.validator = validator;
        this.analytics = analytics;
        this.log = log;
        this.logger = logger;
        this.clock = clock;
    }

    /// <summary>Runs it, for the app when <paramref name="interactive"/>, otherwise for a command.</summary>
    public async Task<StartupOutcome> RunAsync(bool interactive)
    {
        // A log is a week long. Looked at on every start, so one nothing was written to for a month
        // does not still hold what it did in the month before. A command is over in a moment.
        if (interactive)
        {
            await this.log.TidyAsync().ConfigureAwait(false);
        }

        this.configPaths.EnsureCreated();

        // A settings file that is present but unparseable is moved aside by the store as
        // settings.json.corrupt.<timestamp> and read back as null - the same signal as "no settings
        // yet". Only the file's existence tells the two apart, and it has to be checked before the
        // write below recreates it. Without this a single typo in a hand-edited config just made
        // every profile disappear, with nothing in the log to say why.
        bool settingsFileExisted = File.Exists(this.configPaths.SettingsPath);

        AppConfiguration? configuration = await this.jsonStore.ReadAsync<AppConfiguration>(this.configPaths.SettingsPath)
            .ConfigureAwait(false);

        bool quarantined = configuration is null && settingsFileExisted;
        if (configuration is null)
        {
            if (settingsFileExisted)
            {
                await this.logger.LogAsync(
                    new LogEntry
                    {
                        Timestamp = this.clock.UtcNow,
                        Level = LogLevel.Warning,
                        Category = "Configuration",
                        EventId = "ConfigurationQuarantined",
                        Message = $"'{this.configPaths.SettingsPath}' could not be parsed and was moved aside as "
                            + "settings.json.corrupt.<timestamp>; starting from a default configuration. "
                            + "Correct the JSON in that file and move it back to restore your profiles."
                    }).ConfigureAwait(false);
            }

            configuration = CreateDefaultConfiguration();
            await this.jsonStore.WriteAsync(this.configPaths.SettingsPath, configuration).ConfigureAwait(false);
        }

        ConfigurationValidationResult validation = this.validator.Validate(configuration);
        this.appState.UpdateConfiguration(configuration, validation);
        if (!validation.IsValid)
        {
            // Said once a start, by setting and without the profile's number: which, not what was typed.
            this.logger.Warn(
                "Configuration",
                "ConfigurationInvalid",
                $"The settings have {validation.Errors.Count} problem(s): "
                    + validation.DescribeForLog());
        }

        this.analytics.Enabled = configuration.Analytics.Enabled;

        CurrentContextState state = await this.jsonStore.ReadAsync<CurrentContextState>(this.configPaths.StatePath).ConfigureAwait(false)
            ?? new CurrentContextState();
        this.appState.UpdateState(state);

        // A command is a one-shot process that exits at once; recovering, pruning or starting a session
        // for it would be pointless.
        if (!interactive)
        {
            return new StartupOutcome(quarantined);
        }

        await this.analytics.RecoverFromCrashAsync(CancellationToken.None).ConfigureAwait(false);
        await this.analytics.PruneOldSessionsAsync(configuration.Analytics.RetentionDays, CancellationToken.None).ConfigureAwait(false);

        string activeContextId = ActiveAtStartup(state, configuration);
        if (validation.IsValid && configuration.Contexts.Any(context => context.Id == activeContextId))
        {
            await this.analytics.StartSessionAsync(activeContextId, CancellationToken.None).ConfigureAwait(false);
        }

        return new StartupOutcome(quarantined);
    }

    /// <summary>
    /// The profile whose time is counted from launch. The configured one stands in only until the
    /// first switch; after one, no current profile means the active one was turned off, and nothing
    /// is counted until another is switched to.
    /// </summary>
    public static string ActiveAtStartup(CurrentContextState state, AppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(configuration);

        return string.IsNullOrEmpty(state.CurrentContextId) && state.LastSwitchCompletedAt is null
            ? configuration.ActiveContextId
            : state.CurrentContextId;
    }

    private static AppConfiguration CreateDefaultConfiguration()
    {
        const string defaultContextId = "default";

        return new AppConfiguration
        {
            ActiveContextId = defaultContextId,

            // The one place this is written as false: a brand-new install, which is exactly when
            // the first-run wizard should appear. See AppConfiguration.OnboardingCompleted.
            OnboardingCompleted = false,
            Contexts =
            [
                new ContextDefinition
                {
                    Id = defaultContextId,
                    DisplayName = "Default",
                    MenuBarLabel = "DEFAULT"
                }
            ]
        };
    }
}
