using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Root view model for the Main App window (agent.md section 11.1.2): owns the left-hand
/// Profiles/Settings/Stats navigation and swaps in a <see cref="ProfileSetupViewModel"/> when a
/// profile is added or edited. Profile Setup is reachable only from the Profiles page, not from
/// the nav rail itself.
/// </summary>
public sealed class MainAppViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigurationStore configurationStore;

    private object currentPage;

    public MainAppViewModel(
        IContextSwitchService switchService,
        ConfigurationStore configurationStore,
        IJsonStore jsonStore,
        ConfigPaths configPaths,
        IPermissionsChecker permissionsChecker,
        IProcessRunner processRunner,
        IAnalyticsService analyticsService)
    {
        this.configurationStore = configurationStore;

        this.Profiles = new ProfilesViewModel(switchService, configurationStore, jsonStore, configPaths);
        this.Settings = new SettingsViewModel(configurationStore, permissionsChecker, processRunner);
        this.Stats = new StatsViewModel(analyticsService);

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
        ProfileSetupViewModel setup = new(this.configurationStore, context);
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
