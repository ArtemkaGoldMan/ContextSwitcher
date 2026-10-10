using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// Builds a whole app's worth of view models over test doubles, so an interaction test can say what
/// it is exercising instead of wiring six services first.
/// </summary>
public sealed class UiScenario
{
    private UiScenario(AppConfiguration configuration)
    {
        ConfigurationValidator validator = new();
        AppHost.UpdateConfiguration(configuration, validator.Validate(configuration));
        AppHost.UpdateState(new CurrentContextState { CurrentContextId = configuration.ActiveContextId });

        this.Paths = new ConfigPaths("/tmp/context-switcher-ui-tests");
        InMemoryJsonStore inner = new();
        inner.Seed(this.Paths.SettingsPath, configuration);

        // Completes off-context the way real file IO does; the in-memory store on its own never
        // suspends, which hides anything that resumes on the wrong thread.
        this.JsonStore = new YieldingJsonStore(inner);
        this.ConfigurationStore = new ConfigurationStore(this.JsonStore, this.Paths, validator);
        this.SwitchService = new StubContextSwitchService();
        this.ProcessRunner = new FakeProcessRunner();
        this.Permissions = new FakePermissionsChecker();
        this.InstalledApps = new FakeInstalledAppsService();
        this.Catalog = new FakeSystemCatalog();
        this.FocusShortcuts = new FakeFocusShortcutInstaller { AddsTo = this.Catalog };
        this.Clock = new FakeClock();
        this.Sessions = new InMemoryAnalyticsSessionStore();
        this.Analytics = new AnalyticsService(this.Clock, this.Sessions);
        this.Notice = new SwitchNoticeViewModel(DateTimeOffset.UtcNow);
        this.Releases = new FakeReleaseSource();
        this.Updater = new FakeAppUpdater();
        this.Updates = new UpdatesViewModel(this.Releases, this.Updater, this.ProcessRunner, new Version(0, 1, 0), () => new DateTimeOffset(2026, 10, 10, 14, 32, 0, TimeSpan.Zero));
    }

    public ConfigPaths Paths { get; }

    public YieldingJsonStore JsonStore { get; }

    public ConfigurationStore ConfigurationStore { get; }

    public StubContextSwitchService SwitchService { get; }

    public FakeProcessRunner ProcessRunner { get; }

    public FakePermissionsChecker Permissions { get; }

    public FakeInstalledAppsService InstalledApps { get; }

    public FakeSystemCatalog Catalog { get; }

    public FakeFocusShortcutInstaller FocusShortcuts { get; }

    public FakeClock Clock { get; }

    public AnalyticsService Analytics { get; }

    /// <summary>Recorded context time, for seeding the balance chart.</summary>
    public InMemoryAnalyticsSessionStore Sessions { get; }

    /// <summary>The main window's switch-problems card; counts switches from when the scenario began.</summary>
    public SwitchNoticeViewModel Notice { get; }

    /// <summary>What GitHub says the latest release is.</summary>
    public FakeReleaseSource Releases { get; }

    public FakeAppUpdater Updater { get; }

    /// <summary>The update state, for a build that is version 0.1.0.</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>Two profiles, the first active - the shape the app ships after onboarding.</summary>
    public static UiScenario WithTwoProfiles() => new(new AppConfiguration
    {
        ActiveContextId = "work",
        Contexts =
        [
            new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
            new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
        ]
    });

    public static UiScenario With(AppConfiguration configuration) => new(configuration);

    public MainAppViewModel MainApp() => new(
        this.SwitchService, this.ConfigurationStore, this.JsonStore, this.Paths,
        this.Permissions, this.ProcessRunner, this.Analytics, this.InstalledApps, this.Catalog, this.FocusShortcuts, this.Notice, this.Updates);

    public ProfilesViewModel Profiles() =>
        new(this.SwitchService, this.ConfigurationStore, this.JsonStore, this.Paths);

    public SettingsViewModel Settings() =>
        new(this.ConfigurationStore, this.Permissions, this.ProcessRunner, this.Updates);

    public StatsViewModel Stats() => new(this.Analytics);

    public DashboardViewModel Dashboard() => new(
        this.SwitchService, this.Analytics, this.JsonStore, this.ProcessRunner, this.Paths, this.Clock);

    public OnboardingViewModel Onboarding() =>
        new(this.ConfigurationStore, this.InstalledApps, this.SwitchService);

    public ProfileSetupViewModel ProfileSetup(ContextDefinition? existing = null) =>
        new(this.ConfigurationStore, this.InstalledApps, this.Catalog, this.ProcessRunner, this.FocusShortcuts, existing);
}
