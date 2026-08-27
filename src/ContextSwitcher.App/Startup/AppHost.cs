using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.Applications;
using ContextSwitcher.Infrastructure.Automation;
using ContextSwitcher.Infrastructure.Analytics;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Cli;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Hotkeys;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Infrastructure.ProcessExecution;
using ContextSwitcher.Infrastructure.Time;
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
        services.AddSingleton<IHotkeyService, SharpHookHotkeyService>();
        services.AddSingleton<IPermissionsChecker, MacPermissionsChecker>();
        services.AddSingleton<IInstalledAppsService, InstalledAppsService>();
        services.AddSingleton<ConfigurationStore>();

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
    /// Ends the current analytics session and unregisters hotkeys on app shutdown. Safe to call
    /// even if neither was ever started (headless CLI runs never call this).
    /// </summary>
    public static async Task ShutdownAsync()
    {
        IAnalyticsService analyticsService = Services.GetRequiredService<IAnalyticsService>();
        await analyticsService.EndCurrentSessionAsync(SessionEndReason.AppShutdown, CancellationToken.None)
            .ConfigureAwait(false);

        IHotkeyService hotkeyService = Services.GetRequiredService<IHotkeyService>();
        await hotkeyService.UnregisterAllAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task BootstrapAsync(bool isInteractive)
    {
        ConfigPaths configPaths = Services.GetRequiredService<ConfigPaths>();
        IJsonStore jsonStore = Services.GetRequiredService<IJsonStore>();
        ConfigurationValidator validator = Services.GetRequiredService<ConfigurationValidator>();
        IAnalyticsService analyticsService = Services.GetRequiredService<IAnalyticsService>();

        configPaths.EnsureCreated();

        AppConfiguration? configuration = await jsonStore.ReadAsync<AppConfiguration>(configPaths.SettingsPath)
            .ConfigureAwait(false);

        if (configuration is null)
        {
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

        if (ConfigurationValidation.IsValid)
        {
            IHotkeyService hotkeyService = Services.GetRequiredService<IHotkeyService>();
            hotkeyService.HotkeyPressed += OnHotkeyPressed;
            await hotkeyService.RegisterAsync(Configuration.Hotkeys, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static void OnHotkeyPressed(object? sender, string contextId)
    {
        _ = HandleHotkeyPressedAsync(contextId);
    }

    private static async Task HandleHotkeyPressedAsync(string contextId)
    {
        try
        {
            IContextSwitchService switchService = Services.GetRequiredService<IContextSwitchService>();
            await switchService
                .SwitchAsync(new ContextSwitchRequest(contextId, ContextSwitchSource.GlobalHotkey), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A hard boundary: this runs on SharpHook's native callback thread via a fire-and-forget
            // task, so an unhandled exception here would become an unobserved task exception instead
            // of a normal call-stack failure. ContextSwitchService already logs its own failure paths;
            // this only catches genuinely unexpected exceptions.
            ILogger logger = Services.GetRequiredService<ILogger>();
            IClock clock = Services.GetRequiredService<IClock>();
            await logger.LogAsync(
                new LogEntry
                {
                    Timestamp = clock.UtcNow,
                    Level = LogLevel.Error,
                    Category = "Hotkeys",
                    EventId = "HotkeySwitchFailed",
                    Message = $"Unhandled error switching to '{contextId}' from a hotkey: {ex.Message}",
                    ContextId = contextId
                },
                CancellationToken.None).ConfigureAwait(false);
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
