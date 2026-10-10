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

    /// <summary>
    /// Gets the configuration loaded (or created) during startup.
    /// </summary>
    public static AppConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// Gets the result of validating <see cref="Configuration"/>.
    /// </summary>
    public static ConfigurationValidationResult ConfigurationValidation { get; private set; } = null!;

    /// <summary>
    /// Gets whether startup found a settings file it could not parse and moved it aside, in which
    /// case <see cref="Configuration"/> is a fresh default rather than the user's own.
    /// </summary>
    public static bool ConfigurationWasQuarantined { get; private set; }

    private static StateFileWatcher? stateWatcher;

    /// <summary>
    /// Gets the runtime state loaded during startup, or a fresh default when none was persisted.
    /// </summary>
    public static CurrentContextState State { get; private set; } = null!;

    /// <summary>
    /// Raised after <see cref="UpdateConfiguration"/> replaces <see cref="Configuration"/>, so
    /// long-lived view models (the Dashboard) can refresh without the app restarting.
    /// </summary>
    public static event EventHandler? ConfigurationChanged;

    /// <summary>
    /// Raised after <see cref="UpdateState"/> replaces <see cref="State"/>, so the Dashboard and
    /// the Profiles page can both reflect a switch triggered from the other window.
    /// </summary>
    public static event EventHandler? StateChanged;

    /// <summary>
    /// Builds the service container and runs the synchronous startup sequence.
    /// Must be called once, before the Avalonia application starts.
    /// </summary>
    public static void Initialize(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ServiceCollection services = new();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ConfigPaths>();
        services.AddSingleton<IJsonStore, JsonFileStore>();
        services.AddSingleton<ILogger, LocalJsonLogger>();
        services.AddSingleton<ConfigurationValidator>();
        services.AddSingleton<IAnalyticsSessionStore, AnalyticsSessionStore>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<AutomationPlanBuilder>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IScriptRunner, AppleScriptRunner>();
        services.AddSingleton<BrowserLauncher>();
        services.AddSingleton<IAutomationStepExecutor, AutomationStepExecutor>();
        services.AddSingleton<IContextSwitchService>(provider => new ContextSwitchService(
            provider.GetRequiredService<IJsonStore>(),
            provider.GetRequiredService<IAutomationStepExecutor>(),
            provider.GetRequiredService<AutomationPlanBuilder>(),
            provider.GetRequiredService<IAnalyticsService>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger>(),
            provider.GetRequiredService<ConfigPaths>().SettingsPath,
            provider.GetRequiredService<ConfigPaths>().StatePath));
        services.AddSingleton<CliCommandRouter>();
        services.AddSingleton<IPermissionsChecker, MacPermissionsChecker>();
        services.AddSingleton<IInstalledAppsService, InstalledAppsService>();
        services.AddSingleton<ISystemCatalog, SystemCatalog>();
        services.AddSingleton<IFocusShortcutInstaller, FocusShortcutInstaller>();
        services.AddSingleton<ConfigurationStore>();
        services.AddSingleton<StateFileWatcher>();

        // One for the whole run, created now: it remembers a closed notice across windows, and counts
        // switches from the moment the app started rather than from when a window first asked.
        services.AddSingleton(new SwitchNoticeViewModel(DateTimeOffset.UtcNow));

        // Releases are about 50 MB, so the client's own timeout is generous; the API request has a
        // short one of its own.
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        services.AddSingleton<IReleaseSource>(provider => new GitHubReleaseSource(
            provider.GetRequiredService<HttpClient>(), ReleaseVersion.Format(UpdatesViewModel.RunningVersion)));
        services.AddSingleton<IAppUpdater>(provider => new AppBundleUpdater(
            provider.GetRequiredService<HttpClient>(), provider.GetRequiredService<IProcessRunner>()));
        services.AddSingleton(provider => new UpdatesViewModel(
            provider.GetRequiredService<IReleaseSource>(),
            provider.GetRequiredService<IAppUpdater>(),
            provider.GetRequiredService<IProcessRunner>(),
            UpdatesViewModel.RunningVersion));
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<MainAppViewModel>();
        services.AddTransient<OnboardingViewModel>();

        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // A headless CLI invocation is a one-shot process that exits immediately; starting an
        // analytics session or a persistent global hook for it would be pointless and wasteful.
        bool isInteractive = args.Length == 0 || !CliCommandRouter.IsHeadlessCommand(args[0]);
        BootstrapAsync(isInteractive).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Replaces <see cref="Configuration"/> and <see cref="ConfigurationValidation"/> after the Main
    /// App window persists a change (agent.md section 11.1.2), then raises
    /// <see cref="ConfigurationChanged"/>. Callers must already have written and validated
    /// <paramref name="configuration"/> - see <c>ConfigurationStore</c>.
    /// </summary>
    public static void UpdateConfiguration(AppConfiguration configuration, ConfigurationValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(validation);

        Configuration = configuration;
        ConfigurationValidation = validation;
        ConfigurationChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Replaces <see cref="State"/> after a context switch completes, then raises
    /// <see cref="StateChanged"/>.
    /// </summary>
    public static void UpdateState(CurrentContextState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        State = state;
        StateChanged?.Invoke(null, EventArgs.Empty);
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

    private static async Task BootstrapAsync(bool isInteractive)
    {
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

        if (configuration is null)
        {
            ConfigurationWasQuarantined = settingsFileExisted;

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

        Configuration = configuration;
        ConfigurationValidation = validator.Validate(configuration);
        analyticsService.Enabled = Configuration.Analytics.Enabled;

        State = await jsonStore.ReadAsync<CurrentContextState>(configPaths.StatePath).ConfigureAwait(false)
            ?? new CurrentContextState();

        string activeContextId = string.IsNullOrEmpty(State.CurrentContextId)
            ? Configuration.ActiveContextId
            : State.CurrentContextId;

        bool activeContextIsValid = ConfigurationValidation.IsValid
            && Configuration.Contexts.Any(context => context.Id == activeContextId);

        if (!isInteractive)
        {
            return;
        }

        await analyticsService.RecoverFromCrashAsync(CancellationToken.None).ConfigureAwait(false);
        await analyticsService.PruneOldSessionsAsync(Configuration.Analytics.RetentionDays, CancellationToken.None).ConfigureAwait(false);



        if (activeContextIsValid)
        {
            await analyticsService.StartSessionAsync(activeContextId, CancellationToken.None).ConfigureAwait(false);
        }

    }

    /// <summary>
    /// Re-prunes analytics on a slow timer for the life of the process. Fire-and-forget on purpose:
    /// housekeeping must never take the app down, and the next tick retries anyway.
    /// </summary>
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
        StartPeriodicPruning(Services.GetRequiredService<IAnalyticsService>());

        // Keeps the UI honest about switches made by Shortcuts, Siri or the CLI, each of which runs
        // in its own process and so cannot raise StateChanged here.
        stateWatcher ??= Services.GetRequiredService<StateFileWatcher>();

        Services.GetRequiredService<UpdatesViewModel>().StartAutomaticChecks();
    }

    private static void StartPeriodicPruning(IAnalyticsService analyticsService)
    {
        DispatcherTimer timer = new() { Interval = TimeSpan.FromHours(6) };
        timer.Tick += (_, _) => _ = PruneQuietlyAsync(analyticsService);
        timer.Start();
    }

    private static async Task PruneQuietlyAsync(IAnalyticsService analyticsService)
    {
        try
        {
            await analyticsService.PruneOldSessionsAsync(Configuration.Analytics.RetentionDays, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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
