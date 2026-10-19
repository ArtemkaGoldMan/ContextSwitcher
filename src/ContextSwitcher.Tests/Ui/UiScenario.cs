using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Infrastructure.MacOS;
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
        this.AppState = new AppState();
        this.AppState.UpdateConfiguration(configuration, validator.Validate(configuration));
        this.AppState.UpdateState(new CurrentContextState { CurrentContextId = configuration.ActiveContextId });

        this.Paths = new ConfigPaths("/tmp/context-switcher-ui-tests");
        InMemoryJsonStore inner = new();
        inner.Seed(this.Paths.SettingsPath, configuration);

        // Completes off-context the way real file IO does; the in-memory store on its own never
        // suspends, which hides anything that resumes on the wrong thread.
        this.JsonStore = new YieldingJsonStore(inner);
        this.ConfigurationStore = new ConfigurationStore(this.JsonStore, this.Paths, validator, this.AppState, new TestLogger());
        this.SwitchService = new StubContextSwitchService();
        this.Shell = new FakeShell();
        this.Permissions = new FakePermissionsChecker();
        this.InstalledApps = new FakeInstalledAppsService();
        this.Catalog = new FakeSystemCatalog();
        this.FocusShortcuts = new FakeFocusShortcutInstaller { AddsTo = this.Catalog };
        this.Clock = new FakeClock();
        this.Sessions = new InMemoryAnalyticsSessionStore();
        this.Analytics = new AnalyticsService(this.Clock, this.Sessions);
        this.Notice = new SwitchNoticeViewModel(this.AppState, DateTimeOffset.UtcNow, (_, app) =>
        {
            this.ForceQuit.Add(app);
            return Task.CompletedTask;
        });
        this.Releases = new FakeReleaseSource();
        this.Updater = new FakeAppUpdater();
        this.UpdateChecker = new UpdateChecker(
            this.Releases, this.Updater, new FakeSystemNotifications(), this.AppState, new FakeAppInfo(new Version(0, 1, 0)), this.Clock, new TestLogger());
        this.Updates = new UpdatesViewModel(this.UpdateChecker, this.Shell);
        this.Diagnostics = new DiagnosticsSources(
            this.Paths, () => new RunInfo("ContextSwitcher", "0.1.0", "macOS", "26.6", "arm64", "release", 1), () => [], () => [], this.Clock);
    }

    /// <summary>The scenario's settings and state, which every view model it builds reads and saves to.</summary>
    public IAppState AppState { get; }

    public ConfigPaths Paths { get; }

    public YieldingJsonStore JsonStore { get; }

    public ConfigurationStore ConfigurationStore { get; }

    public StubContextSwitchService SwitchService { get; }

    /// <summary>What the scenario asked the system to open: links, files, System Settings.</summary>
    public FakeShell Shell { get; }

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

    /// <summary>The apps the card's "Force quit" was clicked for - none is ended here.</summary>
    public List<string> ForceQuit { get; } = [];

    /// <summary>What GitHub says the latest release is.</summary>
    public FakeReleaseSource Releases { get; }

    public FakeAppUpdater Updater { get; }

    /// <summary>Checks and installs updates, for a build that is version 0.1.0.</summary>
    public UpdateChecker UpdateChecker { get; }

    /// <summary>The update state, as Settings shows it.</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>What a diagnostic report is made of; dated by <see cref="Clock"/>.</summary>
    public DiagnosticsSources Diagnostics { get; }

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
        this.AppState, this.SwitchService, this.ConfigurationStore,
        this.Permissions, this.Shell, this.Analytics, this.InstalledApps, this.Notice, this.Updates, this.Diagnostics,
        new FakePreferredLanguages(), MacPlatform.Capabilities, FakeLoginItem.Unavailable(), this.ProfileSetup);

    public ProfilesViewModel Profiles() =>
        new(this.AppState, this.SwitchService, this.ConfigurationStore, new SwitchNoticeViewModel(this.AppState, DateTimeOffset.UtcNow));

    public SettingsViewModel Settings() =>
        new(this.AppState, this.ConfigurationStore, this.Permissions, this.Shell, this.Updates, this.Diagnostics, new FakePreferredLanguages(), MacPlatform.Capabilities, FakeLoginItem.Unavailable(), this.InstalledApps);

    public StatsViewModel Stats() => new(this.AppState, this.Analytics);

    public DashboardViewModel Dashboard() => new(
        this.AppState, this.SwitchService, this.Analytics, this.Shell, this.Clock);

    public OnboardingViewModel Onboarding() =>
        new(this.AppState, this.ConfigurationStore, this.InstalledApps, this.SwitchService, FakeLoginItem.Unavailable());

    public ProfileSetupViewModel ProfileSetup(ContextDefinition? existing = null) =>
        new(this.AppState, this.ConfigurationStore, this.InstalledApps, this.Catalog, this.Shell, this.FocusShortcuts, MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), existing);
}
