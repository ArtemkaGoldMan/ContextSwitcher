using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.Updates;
using ContextSwitcher.Infrastructure.Automation;
using ContextSwitcher.Infrastructure.Analytics;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Cli;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Infrastructure.Platform;
using ContextSwitcher.Infrastructure.ProcessExecution;
using ContextSwitcher.Infrastructure.Time;
using ContextSwitcher.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// What the app is made of: every service and which implementation it has. What differs between macOS
/// and Windows is each platform project's <see cref="PlatformServices.Register"/>; the app's build
/// brings in one of them.
/// </summary>
public static class AppServices
{
    /// <summary>Registers everything the app and the command line use.</summary>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAppState, AppState>();
        services.AddSingleton<IAppInfo, AppInfo>();
        services.AddSingleton<IClock, SystemClock>();
        PlatformServices.Register(services);

        // Everything is kept in the platform's folder for settings - or a development build's own.
        services.AddSingleton(provider => new ConfigPaths(
            settingsRoot: provider.GetRequiredService<SettingsRoot>().Path,
            development: provider.GetRequiredService<IAppInfo>().IsDevelopment));

        // How long a switch's steps may take and how they wait - what the app has always used.
        services.AddSingleton(StepBudgets.Default);
        services.AddSingleton(QuitTimings.Default);
        services.AddSingleton(TabTimings.Default);
        services.AddSingleton(FollowUpTimings.Default);

        services.AddSingleton<IJsonStore, JsonFileStore>();
        services.AddSingleton(provider => new LocalJsonLogger(
            provider.GetRequiredService<ConfigPaths>(), provider.GetRequiredService<IFileReplacer>(), provider.GetRequiredService<IClock>()));

        // What happens is remembered and only a problem is written, with the trail that led to it - see
        // TrailLogger. CONTEXTSWITCHER_LOG_LEVEL=information (or debug, trace) writes from that level on
        // as it happens, for looking into something that is not a failure.
        services.AddSingleton(provider =>
        {
            IAppState appState = provider.GetRequiredService<IAppState>();
            IClock clock = provider.GetRequiredService<IClock>();
            return new TrailLogger(
                provider.GetRequiredService<LocalJsonLogger>(),
                RunOf(provider),

                // Settings → Detailed log: everything is written, as it happens, until the week is up.
                detailed: () => appState.Configuration.DetailedLogUntil is { } until && until > clock.UtcNow,
                writeFrom: Enum.TryParse(Environment.GetEnvironmentVariable("CONTEXTSWITCHER_LOG_LEVEL"), ignoreCase: true, out LogLevel from) ? from : null);
        });
        services.AddSingleton<ILogger>(provider => provider.GetRequiredService<TrailLogger>());
        services.AddSingleton(provider => new DiagnosticsSources(
            provider.GetRequiredService<ConfigPaths>(),
            RunOf(provider),
            () => DiagnosticsReport.ReadLog(provider.GetRequiredService<ConfigPaths>().AppLogPath),
            () => provider.GetRequiredService<TrailLogger>().Remembered(),
            provider.GetRequiredService<IClock>()));
        services.AddSingleton<ConfigurationValidator>();
        services.AddSingleton<IAnalyticsSessionStore, AnalyticsSessionStore>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton(provider => new AutomationPlanBuilder(
            provider.GetRequiredService<PlatformCapabilities>(), provider.GetRequiredService<StepBudgets>(), provider.GetRequiredService<IDefaultBrowser>()));
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IOpenedTabsStore, OpenedTabsStore>();
        services.AddSingleton<IBrowserLauncher, BrowserLauncher>();

        // What runs each type of step; a type with none is reported as skipped. A new type of step is
        // a new handler and a line here.
        services.AddSingleton<IStepHandler, CloseApplicationsHandler>();
        services.AddSingleton<IStepHandler, LaunchApplicationsHandler>();
        services.AddSingleton<IStepHandler, HideApplicationsHandler>();
        services.AddSingleton<IStepHandler, ManageBrowserContextHandler>();
        services.AddSingleton<IStepHandler, CloseBrowserTabsHandler>();
        services.AddSingleton<IStepHandler>(provider => new DockerContainersHandler(
            AutomationStepType.StartDockerResources, provider.GetRequiredService<IProcessRunner>(), provider.GetRequiredService<IClock>()));
        services.AddSingleton<IStepHandler>(provider => new DockerContainersHandler(
            AutomationStepType.StopDockerResources, provider.GetRequiredService<IProcessRunner>(), provider.GetRequiredService<IClock>()));
        services.AddSingleton<IStepHandler, ControlMediaHandler>();
        services.AddSingleton<IStepHandler, SetFocusModeHandler>();
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
            provider.GetRequiredService<ILogger>(),
            provider.GetRequiredService<FollowUpTimings>()));

        // Switches started in the app ask, when that's on, about everything else open; the command
        // line has no window to ask in, so it switches through the service itself.
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
        services.AddSingleton<ConfigurationStore>();
        services.AddSingleton<AppStartup>();

        // One for the whole run: it remembers a closed notice across windows, and counts switches from
        // the moment it is made - which AppHost does as the app starts, not when a window first asks.
        services.AddSingleton(provider =>
        {
            ClosingAppsFollowUp followUp = provider.GetRequiredService<ClosingAppsFollowUp>();
            return new SwitchNoticeViewModel(
                provider.GetRequiredService<IAppState>(),
                provider.GetRequiredService<IClock>().UtcNow,
                (completedAt, app) => followUp.ForceQuitAsync(completedAt, app, CancellationToken.None));
        });

        // Releases are about 50 MB, so the client's own timeout is generous; the API request has a
        // short one of its own.
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });

        // The release source is shared; which releases are this app's is the platform's to say, so a
        // release with only another platform's files is never offered here.
        services.AddSingleton<IReleaseSource>(provider => new GitHubReleaseSource(
            provider.GetRequiredService<HttpClient>(),
            ReleaseVersion.Format(provider.GetRequiredService<IAppInfo>().Version),
            provider.GetRequiredService<ReleaseAssets>()));
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<MainAppViewModel>();

        // Profile Setup is made when a profile is opened, for that profile - by the main window, which
        // is given this rather than the services Profile Setup needs.
        services.AddSingleton<Func<ContextDefinition?, ProfileSetupViewModel>>(provider => existing => new ProfileSetupViewModel(
            provider.GetRequiredService<IAppState>(),
            provider.GetRequiredService<ConfigurationStore>(),
            provider.GetRequiredService<IInstalledAppsService>(),
            provider.GetRequiredService<ISystemCatalog>(),
            provider.GetRequiredService<IShell>(),
            provider.GetRequiredService<IFocusShortcutInstaller>(),
            provider.GetRequiredService<PlatformCapabilities>(),
            provider.GetRequiredService<IDefaultBrowser>(),
            existing));
        services.AddTransient<OnboardingViewModel>();

        // What runs while the app is up, started together, in this order, once Avalonia is - see
        // IHousekeeping. The command line starts none of it.
        services.AddSingleton<StateFileWatcher>();
        services.AddSingleton(provider =>
        {
            IAppState appState = provider.GetRequiredService<IAppState>();
            return new AwayPause(provider.GetRequiredService<IAnalyticsService>(), () => appState.State.CurrentContextId);
        });
        services.AddSingleton<IHousekeeping, HistoryPruner>();
        services.AddSingleton<IHousekeeping, AliveRecorder>();
        services.AddSingleton<IHousekeeping>(provider => provider.GetRequiredService<StateFileWatcher>());
        services.AddSingleton<IHousekeeping, AwayWatcher>();
        services.AddSingleton<IHousekeeping, ClosingAppsWatcher>();
        services.AddSingleton<IHousekeeping>(provider => provider.GetRequiredService<UpdateChecker>());
    }

    /// <summary>Which app, which build, on which system: the first line of a log and of a diagnostic report.</summary>
    private static Func<RunInfo> RunOf(IServiceProvider provider)
    {
        IAppInfo app = provider.GetRequiredService<IAppInfo>();
        ISystemVersion system = provider.GetRequiredService<ISystemVersion>();
        return () => RunInfo.Current(app.Version.ToString(3), app.IsDevelopment, system);
    }
}
