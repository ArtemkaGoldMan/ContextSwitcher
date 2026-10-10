using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Settings page (agent.md section 11.1.2): app-level settings that apply across every
/// profile, plus the permission that switching depends on.
///
/// Every change is saved the moment it is made, the way macOS's own settings behave. A Save button
/// at the top of a page of toggles left it unclear whether a flipped switch had taken effect - and a
/// change made and then navigated away from was simply lost.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private const string AutomationSettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_Automation";

    private static readonly int[] RetentionPresets = [7, 30, 90, 180, 365, 730];

    private readonly ConfigurationStore configurationStore;
    private readonly IPermissionsChecker permissionsChecker;
    private readonly IProcessRunner processRunner;

    private bool showDockIcon;
    private bool checkForUpdates;
    private bool analyticsEnabled;
    private Choice<int> selectedRetention;
    private bool? automationGranted;
    private bool isCheckingPermissions;
    private string? errorMessage;
    private Task saving = Task.CompletedTask;

    public SettingsViewModel(ConfigurationStore configurationStore, IPermissionsChecker permissionsChecker, IProcessRunner processRunner, UpdatesViewModel updates)
    {
        this.configurationStore = configurationStore;
        this.permissionsChecker = permissionsChecker;
        this.processRunner = processRunner;
        this.Updates = updates;

        this.showDockIcon = AppHost.Configuration.ShowDockIcon;
        this.checkForUpdates = AppHost.Configuration.CheckForUpdates;
        this.analyticsEnabled = AppHost.Configuration.Analytics.Enabled;
        this.RetentionChoices = WithCurrent(RetentionPresets, AppHost.Configuration.Analytics.RetentionDays, FormatDays);
        this.selectedRetention = this.RetentionChoices.First(c => c.Value == AppHost.Configuration.Analytics.RetentionDays);

        this.RefreshPermissionsCommand = new AsyncRelayCommand(this.RefreshPermissionsAsync);
        this.OpenAutomationSettingsCommand = new RelayCommand(() => this.OpenUrl(AutomationSettingsUrl));
        this.SupportDeveloperCommand = new RelayCommand(() => this.OpenUrl(AppLinks.Support));

        _ = this.RefreshPermissionsAsync();
    }

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
    public IReadOnlyList<Choice<int>> RetentionChoices { get; }

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
        true => "Allowed. macOS may still ask once for each app, the first time a switch controls it.",
        false => "Not allowed. Turn on Context Switcher in System Settings → Privacy & Security → Automation.",
        _ => "Checking…"
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

    private async Task RefreshPermissionsAsync()
    {
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

    private void OpenUrl(string url)
    {
        _ = this.processRunner.RunAsync(new ProcessStartOptions("open", [url], TimeSpan.FromSeconds(5)), CancellationToken.None);
    }

    private async Task SaveAsync()
    {
        this.ErrorMessage = null;

        // Built on the configuration as it is now, so a profile saved elsewhere meanwhile is kept.
        // The switch timeout is carried over untouched: nothing reads it yet, so it is not offered.
        AppConfiguration updated = AppHost.Configuration with
        {
            ShowDockIcon = this.ShowDockIcon,
            CheckForUpdates = this.CheckForUpdates,
            Analytics = new AnalyticsConfiguration { Enabled = this.AnalyticsEnabled, RetentionDays = this.SelectedRetention.Value }
        };

        try
        {
            ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                this.ErrorMessage = "Couldn't save: " + string.Join(" ", result.Errors);
            }
        }
        catch (Exception ex)
        {
            this.ErrorMessage = $"Couldn't save: {ex.Message}";
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
        7 => "1 week",
        180 => "6 months",
        365 => "1 year",
        >= 730 when days % 365 == 0 => $"{days / 365} years",
        1 => "1 day",
        _ => $"{days} days"
    };
}
