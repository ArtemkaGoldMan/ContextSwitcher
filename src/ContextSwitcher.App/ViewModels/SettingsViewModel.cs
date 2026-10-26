using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Settings page (docs/architecture.md, "The main window"): app-level settings that apply
/// across every profile, plus the permission that switching depends on.
///
/// Every change is saved the moment it is made, the way macOS's own settings behave. A Save button
/// at the top of a page of toggles left it unclear whether a flipped switch had taken effect - and a
/// change made and then navigated away from was simply lost.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly int[] RetentionPresets = [7, 30, 90, 180, 365, 730];

    private readonly IAppState appState;
    private readonly ConfigurationStore configurationStore;
    private readonly IPermissionsChecker permissionsChecker;
    private readonly IShell shell;

    private readonly IPreferredLanguages preferredLanguages;
    private readonly PlatformCapabilities capabilities;
    private readonly ILoginItem? loginItem;
    private LoginItemStatus loginItemStatus;
    private string? loginItemProblem;
    private Choice<AppearanceMode> selectedAppearance;
    private Choice<string> selectedLanguage;
    private bool showDockIcon;
    private bool checkForUpdates;
    private bool analyticsEnabled;
    private Choice<int> selectedRetention;
    private bool? automationGranted;
    private bool isCheckingPermissions;
    private bool askOnSwitch;
    private string? errorMessage;
    private Task saving = Task.CompletedTask;

    public SettingsViewModel(
        IAppState appState,
        ConfigurationStore configurationStore,
        IPermissionsChecker permissionsChecker,
        IShell shell,
        UpdatesViewModel updates,
        DiagnosticsSources diagnostics,
        IPreferredLanguages preferredLanguages,
        PlatformCapabilities capabilities,
        ILoginItem loginItem,
        IInstalledAppsService installedApps)
    {
        this.appState = appState;
        this.loginItem = loginItem;
        this.loginItemStatus = loginItem.Status;
        this.preferredLanguages = preferredLanguages;
        this.configurationStore = configurationStore;
        this.permissionsChecker = permissionsChecker;
        this.shell = shell;
        this.Updates = updates;
        this.capabilities = capabilities;
        this.Transfer = new SettingsTransferViewModel(appState, configurationStore, capabilities);
        this.IgnoredApps = new IgnoredAppsViewModel(appState, configurationStore, installedApps);
        this.Diagnostics = new SettingsDiagnosticsViewModel(appState, configurationStore, shell, diagnostics, () => this.AutomationGranted);
        this.askOnSwitch = this.appState.Configuration.AskOnSwitch;

        this.appState.ConfigurationChanged += this.OnConfigurationChanged;
        this.Transfer.Imported += (_, _) =>
        {
            this.ReloadFromConfiguration();
            this.Diagnostics.Refresh();
        };

        this.selectedAppearance = this.AppearanceChoices.First(c => c.Value == this.appState.Configuration.Appearance);
        this.LanguageChoices =
        [
            new(AppLanguages.System, Strings.Settings_Language_MatchMacOS),
            .. AppLanguages.Supported.Select(language => new Choice<string>(language.Code, language.NativeName))
        ];
        this.selectedLanguage = this.LanguageChoices.FirstOrDefault(c => string.Equals(c.Value, this.appState.Configuration.Language, StringComparison.OrdinalIgnoreCase))
            ?? this.LanguageChoices[0];
        this.showDockIcon = this.appState.Configuration.ShowDockIcon;
        this.checkForUpdates = this.appState.Configuration.CheckForUpdates;
        this.analyticsEnabled = this.appState.Configuration.Analytics.Enabled;
        this.RetentionChoices = WithCurrent(RetentionPresets, this.appState.Configuration.Analytics.RetentionDays, FormatDays);
        this.selectedRetention = this.RetentionChoices.First(c => c.Value == this.appState.Configuration.Analytics.RetentionDays);

        this.RefreshPermissionsCommand = new AsyncRelayCommand(this.RefreshPermissionsAsync);
        this.OpenAutomationSettingsCommand = new RelayCommand(() => this.shell.OpenSystemPage(SystemPage.AutomationPermission));
        this.SupportDeveloperCommand = new RelayCommand(() => _ = this.shell.OpenLinkAsync(AppLinks.Support, CancellationToken.None));
        this.OpenLoginItemsCommand = new RelayCommand(() => this.loginItem?.OpenSystemSettings());

        _ = this.RefreshPermissionsAsync();
    }

    /// <summary>Match macOS, light or dark - applied the moment it is saved.</summary>
    public IReadOnlyList<Choice<AppearanceMode>> AppearanceChoices { get; } =
    [
        new(AppearanceMode.System, Strings.Settings_Theme_MatchMacOS),
        new(AppearanceMode.Light, Strings.Settings_Theme_Light),
        new(AppearanceMode.Dark, Strings.Settings_Theme_Dark)
    ];

    public Choice<AppearanceMode> SelectedAppearance
    {
        get => this.selectedAppearance;
        set
        {
            // A ComboBox writes null while its items are being swapped; that is not a choice.
            if (value is not null && this.SetProperty(ref this.selectedAppearance, value))
            {
                this.QueueSave();
            }
        }
    }

    /// <summary>Match macOS, then each translation named in its own language.</summary>
    public IReadOnlyList<Choice<string>> LanguageChoices { get; }

    public Choice<string> SelectedLanguage
    {
        get => this.selectedLanguage;
        set
        {
            if (value is not null && this.SetProperty(ref this.selectedLanguage, value))
            {
                this.OnPropertyChanged(nameof(this.IsLanguagePending));
                this.OnPropertyChanged(nameof(this.LanguagePendingText));
                this.QueueSave();
            }
        }
    }

    /// <summary>
    /// The chosen language is not the one showing: it takes effect the next time the app starts, which
    /// the page says rather than leaving the choice looking ignored.
    /// </summary>
    public bool IsLanguagePending =>
        !string.Equals(AppLanguages.Resolve(this.SelectedLanguage.Value, this.preferredLanguages.Get()), AppLanguages.Current, StringComparison.OrdinalIgnoreCase);

    public string LanguagePendingText =>
        Localized.Format(
            Strings.Settings_Language_RestartHint,
            AppLanguages.Supported.First(l => l.Code == AppLanguages.Resolve(this.SelectedLanguage.Value, this.preferredLanguages.Get())).NativeName);

    /// <summary>Whether a switch first asks about the apps and tabs neither profile has.</summary>
    public bool AskOnSwitch
    {
        get => this.askOnSwitch;
        set
        {
            if (this.SetProperty(ref this.askOnSwitch, value))
            {
                this.QueueSave();
            }
        }
    }

    /// <summary>The apps never touched, and adding to them.</summary>
    public IgnoredAppsViewModel IgnoredApps { get; }

    /// <summary>The detailed log, and the report to send when something is wrong.</summary>
    public SettingsDiagnosticsViewModel Diagnostics { get; }

    /// <summary>Whether this computer has a Dock for the app to show in.</summary>
    public bool CanShowDockIcon => this.capabilities.DockIcon;

    /// <summary>Whether this computer asks before the app drives another, so there is a permission to show.</summary>
    public bool ShowsAutomationPermission => this.capabilities.AutomationPermission;

    public bool ShowDockIcon
    {
        get => this.showDockIcon;
        set
        {
            if (this.SetProperty(ref this.showDockIcon, value))
            {
                this.QueueSave();
            }
        }
    }

    /// <summary>
    /// Whether the app opens when the person logs in. macOS keeps this, not the settings file: it is
    /// read from macOS and changed there the moment the switch is flipped.
    /// </summary>
    public bool OpenAtLogin
    {
        get => this.loginItemStatus is LoginItemStatus.On or LoginItemStatus.NeedsApproval;
        set
        {
            if (value == this.OpenAtLogin || this.loginItem is null)
            {
                return;
            }

            this.loginItemProblem = this.loginItem.SetEnabled(value);
            this.RefreshLoginItem();
        }
    }

    /// <summary>Only an installed copy can open at login; run from source, the switch is off and says so.</summary>
    public bool CanOpenAtLogin => this.loginItemStatus != LoginItemStatus.Unavailable;

    /// <summary>Under the switch: why it didn't change, what macOS still wants, or why it can't be used.</summary>
    public string? OpenAtLoginHint => this.loginItemProblem is { } problem
        ? Localized.Format(Strings.Settings_OpenAtLogin_Failed, problem)
        : this.loginItemStatus switch
        {
            LoginItemStatus.NeedsApproval => Strings.Settings_OpenAtLogin_NeedsApproval,
            LoginItemStatus.Unavailable => Strings.Settings_OpenAtLogin_Unavailable,
            _ => null
        };

    public bool HasOpenAtLoginHint => this.OpenAtLoginHint is not null;

    /// <summary>macOS waits for the person to allow it in Login Items; this opens that page.</summary>
    public bool NeedsLoginItemApproval => this.loginItemStatus == LoginItemStatus.NeedsApproval;

    public RelayCommand OpenLoginItemsCommand { get; }

    /// <summary>Whether the daily check for a new version runs; "Check now" works either way.</summary>
    public bool CheckForUpdates
    {
        get => this.checkForUpdates;
        set
        {
            if (this.SetProperty(ref this.checkForUpdates, value))
            {
                this.QueueSave();
            }
        }
    }

    /// <summary>The app-wide update state, shown in the Updates section.</summary>
    public UpdatesViewModel Updates { get; }

    public bool AnalyticsEnabled
    {
        get => this.analyticsEnabled;
        set
        {
            if (this.SetProperty(ref this.analyticsEnabled, value))
            {
                this.QueueSave();
            }
        }
    }

    /// <summary>How long recorded time is kept, plus the configured period if it is none of these.</summary>
    public IReadOnlyList<Choice<int>> RetentionChoices { get; private set; }

    public Choice<int> SelectedRetention
    {
        get => this.selectedRetention;
        set
        {
            // A ComboBox writes null while its items are being swapped; that is not a choice.
            if (value is not null && this.SetProperty(ref this.selectedRetention, value))
            {
                this.QueueSave();
            }
        }
    }

    public bool? AutomationGranted
    {
        get => this.automationGranted;
        private set
        {
            if (this.SetProperty(ref this.automationGranted, value))
            {
                this.OnPropertyChanged(nameof(this.AutomationStatusText));
                this.OnPropertyChanged(nameof(this.IsAutomationAllowed));
                this.OnPropertyChanged(nameof(this.IsAutomationDenied));
            }
        }
    }

    /// <summary>
    /// What the check found, in a sentence. The check controls System Events, and macOS grants this
    /// per app, so "allowed" is not a promise for every app - it says so rather than overclaim.
    /// </summary>
    public string AutomationStatusText => this.AutomationGranted switch
    {
        true => Strings.Settings_Automation_AllowedDetail,
        false => Strings.Settings_Automation_NotAllowedDetail,
        _ => Strings.Common_Checking
    };

    public bool IsAutomationAllowed => this.AutomationGranted == true;

    public bool IsAutomationDenied => this.AutomationGranted == false;

    public bool IsCheckingPermissions
    {
        get => this.isCheckingPermissions;
        private set => this.SetProperty(ref this.isCheckingPermissions, value);
    }

    public string? ErrorMessage
    {
        get => this.errorMessage;
        private set
        {
            if (this.SetProperty(ref this.errorMessage, value))
            {
                this.OnPropertyChanged(nameof(this.HasErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrEmpty(this.ErrorMessage);

    /// <summary>Completes when every change made so far has been written - for tests, and for shutdown.</summary>
    public Task Saving => this.saving;

    public AsyncRelayCommand RefreshPermissionsCommand { get; }

    public RelayCommand OpenAutomationSettingsCommand { get; }

    public RelayCommand SupportDeveloperCommand { get; }

    /// <summary>"Export and import": profiles and settings to and from a file.</summary>
    public SettingsTransferViewModel Transfer { get; }

    /// <summary>
    /// Lets go of the app's configuration events, for itself and the ignored-apps list it holds. The
    /// main window's page outlives the window otherwise, and keeps answering every save.
    /// </summary>
    public void Dispose()
    {
        this.appState.ConfigurationChanged -= this.OnConfigurationChanged;
        this.IgnoredApps.Dispose();
    }

    /// <summary>"Don't ask again", in the window that asks, turns this off from outside Settings.</summary>
    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        if (this.askOnSwitch != this.appState.Configuration.AskOnSwitch)
        {
            this.askOnSwitch = this.appState.Configuration.AskOnSwitch;
            this.OnPropertyChanged(nameof(this.AskOnSwitch));
        }
    }

    /// <summary>
    /// Shows the settings as they are now, without saving them again - after an import replaced them.
    /// </summary>
    private void ReloadFromConfiguration()
    {
        AppConfiguration configuration = this.appState.Configuration;
        this.selectedAppearance = this.AppearanceChoices.First(c => c.Value == configuration.Appearance);
        this.selectedLanguage = this.LanguageChoices.FirstOrDefault(c => string.Equals(c.Value, configuration.Language, StringComparison.OrdinalIgnoreCase))
            ?? this.LanguageChoices[0];
        this.showDockIcon = configuration.ShowDockIcon;
        this.checkForUpdates = configuration.CheckForUpdates;
        this.askOnSwitch = configuration.AskOnSwitch;
        this.analyticsEnabled = configuration.Analytics.Enabled;
        this.RetentionChoices = WithCurrent(RetentionPresets, configuration.Analytics.RetentionDays, FormatDays);
        this.selectedRetention = this.RetentionChoices.First(c => c.Value == configuration.Analytics.RetentionDays);

        foreach (string property in new[]
        {
            nameof(this.SelectedAppearance), nameof(this.SelectedLanguage), nameof(this.IsLanguagePending), nameof(this.LanguagePendingText),
            nameof(this.ShowDockIcon), nameof(this.CheckForUpdates), nameof(this.AskOnSwitch), nameof(this.AnalyticsEnabled), nameof(this.RetentionChoices), nameof(this.SelectedRetention)
        })
        {
            this.OnPropertyChanged(property);
        }
    }

    /// <summary>
    /// Saves after whatever save is already under way, so quick changes are written in the order
    /// they were made and the last one always wins.
    /// </summary>
    private void QueueSave()
    {
        Task previous = this.saving;
        this.saving = SaveAfterAsync(previous);

        async Task SaveAfterAsync(Task before)
        {
            await before.ConfigureAwait(true);
            await this.SaveAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Reads opening at login from macOS again: it can be turned off in System Settings, or approved
    /// there, while this page is open.
    /// </summary>
    private void RefreshLoginItem()
    {
        this.loginItemStatus = this.loginItem?.Status ?? LoginItemStatus.Unavailable;
        this.OnPropertyChanged(nameof(this.OpenAtLogin));
        this.OnPropertyChanged(nameof(this.CanOpenAtLogin));
        this.OnPropertyChanged(nameof(this.OpenAtLoginHint));
        this.OnPropertyChanged(nameof(this.HasOpenAtLoginHint));
        this.OnPropertyChanged(nameof(this.NeedsLoginItemApproval));
    }

    private async Task RefreshPermissionsAsync()
    {
        this.loginItemProblem = null;
        this.RefreshLoginItem();
        this.IsCheckingPermissions = true;
        try
        {
            this.AutomationGranted = await this.permissionsChecker
                .IsAutomationPermissionGrantedAsync(CancellationToken.None)
                .ConfigureAwait(true);
        }
        finally
        {
            this.IsCheckingPermissions = false;
        }
    }


    private async Task SaveAsync()
    {
        this.ErrorMessage = null;

        // Built on the configuration as it is now, so a profile saved elsewhere meanwhile is kept.
        // The switch timeout is carried over untouched: nothing reads it yet, so it is not offered.
        AppConfiguration updated = this.appState.Configuration with
        {
            Appearance = this.SelectedAppearance.Value,
            Language = this.SelectedLanguage.Value,
            ShowDockIcon = this.ShowDockIcon,
            CheckForUpdates = this.CheckForUpdates,
            AskOnSwitch = this.AskOnSwitch,
            Analytics = new AnalyticsConfiguration { Enabled = this.AnalyticsEnabled, RetentionDays = this.SelectedRetention.Value }
        };

        try
        {
            ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                this.ErrorMessage = Localized.Format(Strings.Settings_CouldNotSave, string.Join(" ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            this.ErrorMessage = Localized.Format(Strings.Settings_CouldNotSave, ex.Message);
        }
    }

    /// <summary>
    /// The presets in order, with <paramref name="current"/> slotted in where it falls when it is
    /// not one of them - a hand-edited or older config must still show what it has.
    /// </summary>
    private static IReadOnlyList<Choice<int>> WithCurrent(int[] presets, int current, Func<int, string> format) =>
        presets.Append(current).Distinct().Order().Select(value => new Choice<int>(value, format(value))).ToList();

    private static string FormatDays(int days) => days switch
    {
        7 => Plurals.Format("Duration_Weeks", 1),
        180 => Plurals.Format("Duration_Months", 6),
        >= 365 when days % 365 == 0 => Plurals.Format("Duration_Years", days / 365),
        _ => Plurals.Format("Duration_Days", days)
    };
}
