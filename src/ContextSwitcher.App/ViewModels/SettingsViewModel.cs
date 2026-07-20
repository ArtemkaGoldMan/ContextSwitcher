using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Settings page (agent.md section 11.1.2): app-level settings that apply across every
/// profile, plus automation permission status and a read-only view of every profile's hotkey.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private const string AccessibilitySettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility";
    private const string AutomationSettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_Automation";

    private readonly ConfigurationStore configurationStore;
    private readonly IPermissionsChecker permissionsChecker;
    private readonly IProcessRunner processRunner;

    private string defaultSwitchTimeoutSecondsText;
    private bool showDockIcon;
    private bool analyticsEnabled;
    private string analyticsRetentionDaysText;
    private bool accessibilityGranted;
    private bool? automationGranted;
    private bool isCheckingPermissions;
    private string? errorMessage;
    private IReadOnlyList<HotkeyRowViewModel> hotkeys = [];

    public SettingsViewModel(ConfigurationStore configurationStore, IPermissionsChecker permissionsChecker, IProcessRunner processRunner)
    {
        this.configurationStore = configurationStore;
        this.permissionsChecker = permissionsChecker;
        this.processRunner = processRunner;

        this.defaultSwitchTimeoutSecondsText = AppHost.Configuration.DefaultSwitchTimeoutSeconds.ToString();
        this.showDockIcon = AppHost.Configuration.ShowDockIcon;
        this.analyticsEnabled = AppHost.Configuration.Analytics.Enabled;
        this.analyticsRetentionDaysText = AppHost.Configuration.Analytics.RetentionDays.ToString();
        this.RefreshHotkeys();

        this.SaveCommand = new AsyncRelayCommand(this.SaveAsync);
        this.RefreshPermissionsCommand = new AsyncRelayCommand(this.RefreshPermissionsAsync);
        this.OpenAccessibilitySettingsCommand = new RelayCommand(() => this.OpenUrl(AccessibilitySettingsUrl));
        this.OpenAutomationSettingsCommand = new RelayCommand(() => this.OpenUrl(AutomationSettingsUrl));
        this.SupportDeveloperCommand = new RelayCommand(() => { });

        AppHost.ConfigurationChanged += this.OnConfigurationChanged;
        _ = this.RefreshPermissionsAsync();
    }

    public string DefaultSwitchTimeoutSecondsText
    {
        get => this.defaultSwitchTimeoutSecondsText;
        set => this.SetProperty(ref this.defaultSwitchTimeoutSecondsText, value);
    }

    public bool ShowDockIcon
    {
        get => this.showDockIcon;
        set => this.SetProperty(ref this.showDockIcon, value);
    }

    public bool AnalyticsEnabled
    {
        get => this.analyticsEnabled;
        set => this.SetProperty(ref this.analyticsEnabled, value);
    }

    public string AnalyticsRetentionDaysText
    {
        get => this.analyticsRetentionDaysText;
        set => this.SetProperty(ref this.analyticsRetentionDaysText, value);
    }

    public bool AccessibilityGranted
    {
        get => this.accessibilityGranted;
        private set => this.SetProperty(ref this.accessibilityGranted, value);
    }

    public bool? AutomationGranted
    {
        get => this.automationGranted;
        private set => this.SetProperty(ref this.automationGranted, value);
    }

    public bool IsCheckingPermissions
    {
        get => this.isCheckingPermissions;
        private set => this.SetProperty(ref this.isCheckingPermissions, value);
    }

    public IReadOnlyList<HotkeyRowViewModel> Hotkeys
    {
        get => this.hotkeys;
        private set => this.SetProperty(ref this.hotkeys, value);
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

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand RefreshPermissionsCommand { get; }

    public RelayCommand OpenAccessibilitySettingsCommand { get; }

    public RelayCommand OpenAutomationSettingsCommand { get; }

    public RelayCommand SupportDeveloperCommand { get; }

    public void Dispose()
    {
        AppHost.ConfigurationChanged -= this.OnConfigurationChanged;
    }

    private void OnConfigurationChanged(object? sender, EventArgs e) => this.RefreshHotkeys();

    private void RefreshHotkeys()
    {
        this.Hotkeys = AppHost.Configuration.Hotkeys
            .Select(hotkey =>
            {
                string contextName = AppHost.Configuration.Contexts
                    .FirstOrDefault(context => context.Id == hotkey.ContextId)?.DisplayName ?? hotkey.ContextId;
                return new HotkeyRowViewModel(contextName, hotkey.Accelerator, hotkey.Enabled);
            })
            .ToList();
    }

    private async Task RefreshPermissionsAsync()
    {
        this.IsCheckingPermissions = true;
        try
        {
            this.AccessibilityGranted = this.permissionsChecker.IsAccessibilityPermissionGranted();
            this.AutomationGranted = await this.permissionsChecker
                .IsAutomationPermissionGrantedAsync(CancellationToken.None)
                .ConfigureAwait(true);
        }
        finally
        {
            this.IsCheckingPermissions = false;
        }
    }

    private void OpenUrl(string url)
    {
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", [url], TimeSpan.FromSeconds(5)), CancellationToken.None);
    }

    private async Task SaveAsync()
    {
        this.ErrorMessage = null;

        if (!int.TryParse(this.DefaultSwitchTimeoutSecondsText, out int timeoutSeconds) || timeoutSeconds < 1)
        {
            this.ErrorMessage = "Default switch timeout must be a positive number of seconds.";
            return;
        }

        if (!int.TryParse(this.AnalyticsRetentionDaysText, out int retentionDays) || retentionDays < 1)
        {
            this.ErrorMessage = "Analytics retention must be a positive number of days.";
            return;
        }

        AppConfiguration updated = AppHost.Configuration with
        {
            DefaultSwitchTimeoutSeconds = timeoutSeconds,
            ShowDockIcon = this.ShowDockIcon,
            Analytics = new AnalyticsConfiguration { Enabled = this.AnalyticsEnabled, RetentionDays = retentionDays }
        };

        ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            this.ErrorMessage = string.Join(" ", result.Errors);
        }
    }
}
