using Avalonia.Threading;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.Applications;
using ContextSwitcher.Infrastructure.Automation;
using ContextSwitcher.Infrastructure.Catalog;
using ContextSwitcher.Infrastructure.Analytics;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Cli;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Infrastructure.ProcessExecution;
using ContextSwitcher.Infrastructure.Time;
using ContextSwitcher.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Composition root that builds the dependency injection container and runs the startup sequence
/// (create config directory, load or create default settings, validate, load state, start the
/// analytics session for the active context) before the Avalonia UI thread starts.
/// </summary>
public static class AppHost
{
    /// <summary>
    /// Gets the root service provider. Only <see cref="Program"/> and Avalonia-instantiated
    /// types (which cannot receive constructor injection from <c>AppBuilder</c>) should read this.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private static StateFileWatcher? stateWatcher;
    private static AwayEvents? awayEvents;
    private static ClosingAppsFollowUp? closingFollowUp;

    /// <summary>
    /// Builds the service container and runs the synchronous startup sequence.
    /// Must be called once, before the Avalonia application starts.
    /// </summary>
    public static StartupOutcome Initialize(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ServiceCollection services = new();

        AppState appState = new();
        services.AddSingleton<IAppState>(appState);
        services.AddSingleton<IClock, SystemClock>();
        AddPlatformFileServices(services);
        services.AddSingleton<IJsonStore, JsonFileStore>();
        services.AddSingleton(provider => new LocalJsonLogger(
            provider.GetRequiredService<ConfigPaths>(), provider.GetRequiredService<IFileReplacer>(), provider.GetRequiredService<IClock>()));

        // What happens is remembered and only a problem is written, with the trail that led to it - see
        // TrailLogger. CONTEXTSWITCHER_LOG_LEVEL=information (or debug, trace) writes from that level on
        // as it happens, for looking into something that is not a failure.
        services.AddSingleton(provider => new TrailLogger(
            provider.GetRequiredService<LocalJsonLogger>(),
            () => RunInfo.Current(UpdatesViewModel.RunningVersion.ToString(3), AppBuild.IsDevelopment),

            // Settings → Detailed log: everything is written, as it happens, until the week is up.
            detailed: () => appState.Configuration.DetailedLogUntil is { } until && until > DateTimeOffset.UtcNow,
            writeFrom: Enum.TryParse(Environment.GetEnvironmentVariable("CONTEXTSWITCHER_LOG_LEVEL"), ignoreCase: true, out LogLevel from) ? from : null));
        services.AddSingleton<ILogger>(provider => provider.GetRequiredService<TrailLogger>());
        services.AddSingleton(provider => new DiagnosticsSources(
            provider.GetRequiredService<ConfigPaths>(),
            () => RunInfo.Current(UpdatesViewModel.RunningVersion.ToString(3), AppBuild.IsDevelopment),
            () => provider.GetRequiredService<TrailLogger>().Remembered(),
            provider.GetRequiredService<IClock>()));
        services.AddSingleton<ConfigurationValidator>();
        services.AddSingleton<IAnalyticsSessionStore, AnalyticsSessionStore>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IDefaultBrowser, MacDefaultBrowser>();
        services.AddSingleton<ILoginItem, MacLoginItem>();
        services.AddSingleton(provider => new AutomationPlanBuilder(provider.GetRequiredService<IDefaultBrowser>()));
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IScriptRunner, AppleScriptRunner>();
        services.AddSingleton<IOpenedTabsStore, OpenedTabsStore>();
        services.AddSingleton<IBrowserRuns, ProcessBrowserRuns>();
        services.AddSingleton(provider => new BrowserLauncher(
            provider.GetRequiredService<IProcessRunner>(),
            provider.GetRequiredService<IScriptRunner>(),
            provider.GetRequiredService<IOpenedTabsStore>(),
            provider.GetRequiredService<IBrowserRuns>()));
        services.AddSingleton<IApplicationHider, MacApplicationHider>();
        services.AddSingleton<IAppProcesses, MacAppProcesses>();
        services.AddSingleton<IAutomationStepExecutor, AutomationStepExecutor>();
        services.AddSingleton<ISwitchLock>(provider => new FileSwitchLock(provider.GetRequiredService<ConfigPaths>().SwitchLockPath));
        services.AddSingleton(provider => new ContextSwitchService(
            provider.GetRequiredService<IJsonStore>(),
            provider.GetRequiredService<IAutomationStepExecutor>(),
            provider.GetRequiredService<AutomationPlanBuilder>(),
            provider.GetRequiredService<IAnalyticsService>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger>(),
            provider.GetRequiredService<ISwitchLock>(),
            provider.GetRequiredService<ConfigPaths>().SettingsPath,
            provider.GetRequiredService<ConfigPaths>().StatePath));
        services.AddSingleton(provider => new ClosingAppsFollowUp(
            provider.GetRequiredService<ContextSwitchService>(),
            provider.GetRequiredService<IAppProcesses>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger>()));

        // Switches started in the app ask, when that's on, about everything else open; the command
        // line has no window to ask in, so it switches through the service itself.
        services.AddSingleton<IRunningApplications, MacRunningApplications>();
        services.AddSingleton<IImageConverter>(provider => new SipsImageConverter(provider.GetRequiredService<IProcessRunner>(), SipsImageConverter.DefaultTimeout));
        services.AddSingleton<ISiteIcons>(provider => new SiteIcons(provider.GetRequiredService<IImageConverter>(), provider.GetRequiredService<ConfigPaths>()));
        services.AddSingleton<IAskOnSwitchPresenter, WindowAskOnSwitchPresenter>();
        services.AddSingleton<IContextSwitchService>(provider => new SwitchCoordinator(
            provider.GetRequiredService<ContextSwitchService>(),
            provider.GetRequiredService<IAppState>(),
            provider.GetRequiredService<ConfigurationStore>(),
            provider.GetRequiredService<IRunningApplications>(),
            provider.GetRequiredService<ISystemCatalog>(),
            provider.GetRequiredService<IAskOnSwitchPresenter>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IJsonStore>(),
            provider.GetRequiredService<ConfigPaths>().StatePath));
        services.AddSingleton(provider => new CliCommandRouter(provider.GetRequiredService<ContextSwitchService>()));
        services.AddSingleton<IPermissionsChecker, MacPermissionsChecker>();
        services.AddSingleton<IInstalledAppsService, InstalledAppsService>();
        services.AddSingleton<ISystemCatalog, SystemCatalog>();
        services.AddSingleton<IFocusShortcutInstaller, FocusShortcutInstaller>();
        services.AddSingleton<ConfigurationStore>();
        services.AddSingleton<StateFileWatcher>();

        // One for the whole run, created now: it remembers a closed notice across windows, and counts
        // switches from the moment the app started rather than from when a window first asked.
        // Its "Force quit" asks the follow-up, which is only built later - so it is found when clicked.
        services.AddSingleton(new SwitchNoticeViewModel(
            appState,
            DateTimeOffset.UtcNow,
            (completedAt, app) => Services.GetRequiredService<ClosingAppsFollowUp>().ForceQuitAsync(completedAt, app, CancellationToken.None)));

        // Releases are about 50 MB, so the client's own timeout is generous; the API request has a
        // short one of its own.
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        // The release source is shared; which releases are this app's is the updater's to say - the Mac's
        // zip and disk image - so a release with only Windows files is never offered here.
        services.AddSingleton<IReleaseSource>(provider => new GitHubReleaseSource(
            provider.GetRequiredService<HttpClient>(), ReleaseVersion.Format(UpdatesViewModel.RunningVersion), AppBundleUpdater.Assets));
        services.AddSingleton<IAppUpdater>(provider => new AppBundleUpdater(
            provider.GetRequiredService<HttpClient>(), provider.GetRequiredService<IProcessRunner>()));
        services.AddSingleton<ISystemNotifications, MacNotifications>();
        services.AddSingleton(provider => new UpdatesViewModel(
            provider.GetRequiredService<IAppState>(),
            provider.GetRequiredService<IReleaseSource>(),
            provider.GetRequiredService<IAppUpdater>(),
            provider.GetRequiredService<IProcessRunner>(),
            provider.GetRequiredService<ISystemNotifications>(),
            UpdatesViewModel.RunningVersion));
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<MainAppViewModel>();
        services.AddTransient<OnboardingViewModel>();

        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // For the places that cannot be handed a logger: a callback from macOS, a catch in a static helper.
        AppLog.Use(Services.GetRequiredService<ILogger>());

        // A log is a week long. Looked at on every start, so one nothing was written to for a month
        // does not still hold what it did in the month before.
        if (!CliCommandRouter.IsHeadless(args))
        {
            Services.GetRequiredService<LocalJsonLogger>().TidyAsync().GetAwaiter().GetResult();
        }

        // A headless CLI invocation is a one-shot process that exits immediately; starting an
        // analytics session or a persistent global hook for it would be pointless and wasteful.
        bool isInteractive = !CliCommandRouter.IsHeadless(args);
        return BootstrapAsync(isInteractive).GetAwaiter().GetResult();
    }

    /// <summary>
    /// What differs between macOS and Windows for the app's files, chosen here once rather than in the
    /// classes that use them: where everything is kept - <c>~/.config</c>, or <c>%APPDATA%</c> on
    /// Windows - and whether a move refused for a file held open elsewhere is tried again, which only
    /// Windows needs. Moves into each platform's own registrations with stage 0 of
    /// <c>docs/windows-plan.md</c>.
    /// </summary>
    private static void AddPlatformFileServices(IServiceCollection services)
    {
        bool windows = OperatingSystem.IsWindows();
        services.AddSingleton(_ => new ConfigPaths(
            settingsRoot: windows ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : null));
        services.AddSingleton<IFileReplacer>(windows ? FileReplacer.RetryingWhileHeldOpen : FileReplacer.Once);
    }

    /// <summary>
    /// Ends the current analytics session on app shutdown. Safe to call even if none was ever
    /// started (headless CLI runs never call this).
    /// </summary>
    public static async Task ShutdownAsync()
    {
        stateWatcher?.Dispose();
        stateWatcher = null;

        IAnalyticsService analyticsService = Services.GetRequiredService<IAnalyticsService>();
        await analyticsService.EndCurrentSessionAsync(SessionEndReason.AppShutdown, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task<StartupOutcome> BootstrapAsync(bool isInteractive)
    {
        IAppState appState = Services.GetRequiredService<IAppState>();
        ConfigPaths configPaths = Services.GetRequiredService<ConfigPaths>();
        IJsonStore jsonStore = Services.GetRequiredService<IJsonStore>();
        ConfigurationValidator validator = Services.GetRequiredService<ConfigurationValidator>();
        IAnalyticsService analyticsService = Services.GetRequiredService<IAnalyticsService>();

        configPaths.EnsureCreated();

        // A settings file that is present but unparseable is moved aside by the store as
        // settings.json.corrupt.<timestamp> and read back as null - the same signal as "no settings
        // yet". Only the file's existence tells the two apart, and it has to be checked before the
        // write below recreates it. Without this a single typo in a hand-edited config just made
        // every profile disappear, with nothing in the log to say why.
        bool settingsFileExisted = File.Exists(configPaths.SettingsPath);

        AppConfiguration? configuration = await jsonStore.ReadAsync<AppConfiguration>(configPaths.SettingsPath)
            .ConfigureAwait(false);

        bool quarantined = configuration is null && settingsFileExisted;
        if (configuration is null)
        {
            if (settingsFileExisted)
            {
                await Services.GetRequiredService<ILogger>().LogAsync(
                    new LogEntry
                    {
                        Timestamp = Services.GetRequiredService<IClock>().UtcNow,
                        Level = LogLevel.Warning,
                        Category = "Configuration",
                        EventId = "ConfigurationQuarantined",
                        Message = $"'{configPaths.SettingsPath}' could not be parsed and was moved aside as "
                            + "settings.json.corrupt.<timestamp>; starting from a default configuration. "
                            + "Correct the JSON in that file and move it back to restore your profiles."
                    }).ConfigureAwait(false);
            }

            configuration = CreateDefaultConfiguration();
            await jsonStore.WriteAsync(configPaths.SettingsPath, configuration).ConfigureAwait(false);
        }

        ConfigurationValidationResult validation = validator.Validate(configuration);
        appState.UpdateConfiguration(configuration, validation);
        if (!validation.IsValid)
        {
            // Said once a start, by setting and without the profile's number: which, not what was typed.
            AppLog.Warn(
                "Configuration",
                "ConfigurationInvalid",
                $"The settings have {validation.Errors.Count} problem(s): "
                    + validation.DescribeForLog());
        }

        analyticsService.Enabled = configuration.Analytics.Enabled;

        CurrentContextState state = await jsonStore.ReadAsync<CurrentContextState>(configPaths.StatePath).ConfigureAwait(false)
            ?? new CurrentContextState();
        appState.UpdateState(state);

        string activeContextId = ActiveAtStartup(state, configuration);

        bool activeContextIsValid = validation.IsValid
            && configuration.Contexts.Any(context => context.Id == activeContextId);

        if (!isInteractive)
        {
            return new StartupOutcome(quarantined);
        }

        await analyticsService.RecoverFromCrashAsync(CancellationToken.None).ConfigureAwait(false);
        await analyticsService.PruneOldSessionsAsync(configuration.Analytics.RetentionDays, CancellationToken.None).ConfigureAwait(false);



        if (activeContextIsValid)
        {
            await analyticsService.StartSessionAsync(activeContextId, CancellationToken.None).ConfigureAwait(false);
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

    /// <summary>
    /// Starts the things that need Avalonia's dispatcher, and so cannot run during
    /// <see cref="Initialize"/> - that happens before the platform is set up, and merely
    /// constructing a DispatcherTimer there claims the UI dispatcher for the calling thread, which
    /// made Avalonia's own initialization fail with "the calling thread cannot access this object".
    /// Called from OnFrameworkInitializationCompleted instead.
    /// </summary>
    public static void StartRuntimeServices()
    {
        if (Services is null)
        {
            return;
        }

        // Pruning only at startup meant a menu bar app left up for months honoured its retention
        // setting exactly once, keeping every session past the cutoff until the next restart.
        StartPeriodicPruning(Services.GetRequiredService<IAnalyticsService>(), Services.GetRequiredService<IAppState>());

        // A sign of life every couple of minutes: after a crash, the session ends at the last one.
        StartAliveRecording(Services.GetRequiredService<IAnalyticsService>());

        // Keeps the UI honest about switches made by Shortcuts, Siri or the CLI, each of which runs
        // in its own process and so cannot raise StateChanged here.
        stateWatcher ??= Services.GetRequiredService<StateFileWatcher>();

        // No time counts while the Mac sleeps, its screen is locked, or another user is in front.
        awayEvents ??= StartAwayPause(Services.GetRequiredService<IAnalyticsService>(), Services.GetRequiredService<IAppState>());

        // An app a switch asked to quit that was still going when it finished is watched for a couple
        // of minutes, and its line in the warnings follows what it does.
        closingFollowUp ??= StartClosingFollowUp(Services.GetRequiredService<ClosingAppsFollowUp>(), Services.GetRequiredService<IAppState>());

        Services.GetRequiredService<UpdatesViewModel>().StartAutomaticChecks();
    }

    /// <summary>
    /// Has <paramref name="followUp"/> watch each switch that finishes from now on - wherever it was
    /// started, the command line included - and shows what it changes, as long as that is still the
    /// last switch: one that finished meanwhile has warnings of its own.
    /// </summary>
    private static ClosingAppsFollowUp StartClosingFollowUp(ClosingAppsFollowUp followUp, IAppState appState)
    {
        DateTimeOffset since = DateTimeOffset.UtcNow;
        DateTimeOffset? watched = null;
        appState.StateChanged += (_, _) =>
        {
            if (appState.State.LastSwitchCompletedAt is { } completedAt && completedAt >= since && completedAt != watched)
            {
                watched = completedAt;
                followUp.Watch(appState.State);
            }
        };

        followUp.StateAmended += (_, amended) => Dispatcher.UIThread.Post(() =>
        {
            if (appState.State.LastSwitchCompletedAt == amended.LastSwitchCompletedAt)
            {
                appState.UpdateState(amended);
            }
        });

        return followUp;
    }

    private static void StartAliveRecording(IAnalyticsService analyticsService)
    {
        _ = QuietlyAsync(() => analyticsService.RecordAliveAsync(CancellationToken.None));
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(2) };
        timer.Tick += (_, _) => _ = QuietlyAsync(() => analyticsService.RecordAliveAsync(CancellationToken.None));
        timer.Start();
    }

    private static AwayEvents StartAwayPause(IAnalyticsService analyticsService, IAppState appState)
    {
        AwayPause pause = new(analyticsService, () => appState.State.CurrentContextId);
        AwayEvents events = new();
        events.Away += (_, reason) => _ = QuietlyAsync(() => pause.OnAwayAsync(reason, CancellationToken.None));
        events.Back += (_, reason) => _ = QuietlyAsync(() => pause.OnBackAsync(reason, CancellationToken.None));
        return events;
    }

    /// <summary>Time tracking that can't write its file gives up on that once; it never takes the app down.</summary>
    private static async Task QuietlyAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Analytics", "TimeNotRecorded", "The time history could not be written.", ex);
        }
    }

    private static void StartPeriodicPruning(IAnalyticsService analyticsService, IAppState appState)
    {
        DispatcherTimer timer = new() { Interval = TimeSpan.FromHours(6) };
        timer.Tick += (_, _) => _ = PruneQuietlyAsync(analyticsService, appState);
        timer.Start();
    }

    private static async Task PruneQuietlyAsync(IAnalyticsService analyticsService, IAppState appState)
    {
        try
        {
            await analyticsService.PruneOldSessionsAsync(appState.Configuration.Analytics.RetentionDays, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Analytics", "HistoryNotPruned", "Old time history could not be cleared away.", ex);
            // Nothing to do about it here; the next tick tries again.
        }
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
