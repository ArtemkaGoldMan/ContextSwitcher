using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Root view model for the Main App window (agent.md section 11.1.2): owns the left-hand
/// Profiles/Settings/Stats navigation and swaps in a <see cref="ProfileSetupViewModel"/> when a
/// profile is added or edited. Profile Setup is reachable only from the Profiles page, not from
/// the nav rail itself.
/// </summary>
public sealed class MainAppViewModel : ViewModelBase, IDisposable
{
    private readonly IAppState appState;
    private readonly ConfigurationStore configurationStore;
    private readonly IInstalledAppsService installedAppsService;
    private readonly ISystemCatalog catalog;
    private readonly IShell shell;
    private readonly IFocusShortcutInstaller focusShortcuts;
    private readonly IDefaultBrowser? defaultBrowser;

    private object currentPage;

    public MainAppViewModel(
        IAppState appState,
        IContextSwitchService switchService,
        ConfigurationStore configurationStore,
        IPermissionsChecker permissionsChecker,
        IShell shell,
        IAnalyticsService analyticsService,
        IInstalledAppsService installedAppsService,
        ISystemCatalog catalog,
        IFocusShortcutInstaller focusShortcuts,
        SwitchNoticeViewModel switchNotice,
        UpdatesViewModel updates,
        DiagnosticsSources diagnostics,
        IPreferredLanguages preferredLanguages,
        IDefaultBrowser? defaultBrowser = null,
        ILoginItem? loginItem = null)
    {
        this.appState = appState;
        this.focusShortcuts = focusShortcuts;
        this.defaultBrowser = defaultBrowser;
        this.configurationStore = configurationStore;
        this.installedAppsService = installedAppsService;
        this.catalog = catalog;
        this.shell = shell;

        this.Profiles = new ProfilesViewModel(appState, switchService, configurationStore, switchNotice);
        this.Settings = new SettingsViewModel(appState, configurationStore, permissionsChecker, shell, updates, diagnostics, preferredLanguages, loginItem: loginItem, installedApps: installedAppsService);
        this.Stats = new StatsViewModel(appState, analyticsService);

        this.Profiles.EditRequested += this.OnEditRequested;

        this.currentPage = this.Profiles;

        this.NavigateToProfilesCommand = new RelayCommand(() => this.CurrentPage = this.Profiles);
        this.NavigateToSettingsCommand = new RelayCommand(() => this.CurrentPage = this.Settings);
        this.NavigateToStatsCommand = new RelayCommand(() => this.CurrentPage = this.Stats);
    }

    public ProfilesViewModel Profiles { get; }

    public SettingsViewModel Settings { get; }

    public StatsViewModel Stats { get; }

    public object CurrentPage
    {
        get => this.currentPage;
        private set
        {
            if (this.SetProperty(ref this.currentPage, value))
            {
                this.OnPropertyChanged(nameof(this.IsProfilesActive));
                this.OnPropertyChanged(nameof(this.IsSettingsActive));
                this.OnPropertyChanged(nameof(this.IsStatsActive));
            }
        }
    }

    public bool IsProfilesActive => ReferenceEquals(this.CurrentPage, this.Profiles);

    public bool IsSettingsActive => ReferenceEquals(this.CurrentPage, this.Settings);

    public bool IsStatsActive => ReferenceEquals(this.CurrentPage, this.Stats);

    /// <summary>Opens the Settings page - for the menu bar's "Update to …", whose button is there.</summary>
    public void ShowSettings() => this.CurrentPage = this.Settings;

    public RelayCommand NavigateToProfilesCommand { get; }

    public RelayCommand NavigateToSettingsCommand { get; }

    public RelayCommand NavigateToStatsCommand { get; }

    public void Dispose()
    {
        this.Profiles.EditRequested -= this.OnEditRequested;
        this.Profiles.Dispose();
        this.Settings.Dispose();
        this.Stats.Dispose();
    }

    private void OnEditRequested(object? sender, ContextDefinition? context)
    {
        ProfileSetupViewModel setup = new(this.appState, this.configurationStore, this.installedAppsService, this.catalog, this.shell, this.focusShortcuts, context, this.defaultBrowser);
        setup.Saved += this.OnProfileSetupFinished;
        setup.CancelRequested += this.OnProfileSetupFinished;
        this.CurrentPage = setup;
    }

    private void OnProfileSetupFinished(object? sender, EventArgs e)
    {
        if (sender is ProfileSetupViewModel setup)
        {
            setup.Saved -= this.OnProfileSetupFinished;
            setup.CancelRequested -= this.OnProfileSetupFinished;
        }

        this.CurrentPage = this.Profiles;
    }
}
