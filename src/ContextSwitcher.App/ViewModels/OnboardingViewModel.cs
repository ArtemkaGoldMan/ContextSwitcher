using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// The first-run wizard. A fresh install otherwise lands on a single empty profile called
/// "Default", which means a context <em>switcher</em> that cannot switch - the worst possible first
/// impression for the product thesis in <c>docs/idea.md</c> (beat Bunch on approachability).
/// This walks the user to two working profiles and a successful switch.
/// </summary>
public sealed class OnboardingViewModel : ViewModelBase
{
    /// <summary>
    /// Apps commonly associated with each context, pre-ticked only when actually installed. Purely
    /// a starting point - every suggestion is removable, and the picker covers everything else.
    /// </summary>
    private static readonly string[] WorkSuggestions =
    [
        "Slack", "Microsoft Teams", "Microsoft Outlook", "Visual Studio Code", "Cursor",
        "DataGrip", "Docker", "Xcode", "Zoom", "Notion"
    ];

    private static readonly string[] PersonalSuggestions =
    [
        "Spotify", "Discord", "Steam", "Telegram", "WhatsApp", "Obsidian", "Photos", "Music"
    ];

    private readonly ConfigurationStore configurationStore;
    private readonly IContextSwitchService switchService;

    private int stepIndex;
    private string? errorMessage;
    private bool isFinishing;

    public OnboardingViewModel(
        ConfigurationStore configurationStore,
        IInstalledAppsService installedAppsService,
        IContextSwitchService switchService)
    {
        this.configurationStore = configurationStore;
        this.switchService = switchService;

        this.Work = new OnboardingProfileViewModel(
            "work", "Work", "WORK", "#2F6FED",
            new AppPickerViewModel(installedAppsService, () => this.Work!.Apps.Select(a => a.Name), name => this.Work!.AddApp(name)));

        this.Personal = new OnboardingProfileViewModel(
            "personal", "Personal", "HOME", "#20A67A",
            new AppPickerViewModel(installedAppsService, () => this.Personal!.Apps.Select(a => a.Name), name => this.Personal!.AddApp(name)));

        this.NextCommand = new RelayCommand(this.GoNext, () => this.CanGoNext);
        this.BackCommand = new RelayCommand(this.GoBack, () => this.CanGoBack);
        this.FinishCommand = new AsyncRelayCommand(this.FinishAsync, () => !this.isFinishing);
        this.SkipCommand = new AsyncRelayCommand(this.SkipAsync, () => !this.isFinishing);

        _ = this.LoadPickersAsync();
    }

    /// <summary>Raised once the wizard is done (finished or skipped) so the host window can close.</summary>
    public event EventHandler? Completed;

    public OnboardingProfileViewModel Work { get; }

    public OnboardingProfileViewModel Personal { get; }

    public int StepIndex
    {
        get => this.stepIndex;
        private set
        {
            if (this.SetProperty(ref this.stepIndex, value))
            {
                this.OnPropertyChanged(nameof(this.IsWelcomeStep));
                this.OnPropertyChanged(nameof(this.IsWorkStep));
                this.OnPropertyChanged(nameof(this.IsPersonalStep));
                this.OnPropertyChanged(nameof(this.IsDoneStep));
                this.OnPropertyChanged(nameof(this.CanGoNext));
                this.OnPropertyChanged(nameof(this.CanGoBack));
                this.OnPropertyChanged(nameof(this.StepLabel));
                this.NextCommand.RaiseCanExecuteChanged();
                this.BackCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsWelcomeStep => this.StepIndex == 0;

    public bool IsWorkStep => this.StepIndex == 1;

    public bool IsPersonalStep => this.StepIndex == 2;

    public bool IsDoneStep => this.StepIndex == 3;

    public string StepLabel => this.IsWelcomeStep ? string.Empty : $"Step {this.StepIndex} of 3";

    public bool CanGoNext => this.StepIndex < 3;

    /// <summary>
    /// Back stays available on the final step too - reaching "You're set" and realising you want
    /// to change a profile is a normal thing to do, and the only alternative would be finishing and
    /// re-editing afterwards.
    /// </summary>
    public bool CanGoBack => this.StepIndex > 0;

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

    public RelayCommand NextCommand { get; }

    public RelayCommand BackCommand { get; }

    public AsyncRelayCommand FinishCommand { get; }

    public AsyncRelayCommand SkipCommand { get; }

    private async Task LoadPickersAsync()
    {
        await this.Work.AppPicker.LoadAsync().ConfigureAwait(true);
        await this.Personal.AppPicker.LoadAsync().ConfigureAwait(true);

        SeedSuggestions(this.Work, WorkSuggestions);
        SeedSuggestions(this.Personal, PersonalSuggestions);
    }

    private static void SeedSuggestions(OnboardingProfileViewModel profile, IReadOnlyList<string> suggestions)
    {
        HashSet<string> installed = profile.AppPicker.AllApps
            .Select(app => app.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string suggestion in suggestions.Where(installed.Contains))
        {
            profile.AddApp(suggestion);
        }
    }

    private void GoNext() => this.StepIndex++;

    private void GoBack() => this.StepIndex--;

    /// <summary>
    /// Writes both profiles, marks onboarding complete, and switches into Work so the very first
    /// thing the user sees is the app actually doing its job.
    /// </summary>
    private async Task FinishAsync()
    {
        this.isFinishing = true;
        this.FinishCommand.RaiseCanExecuteChanged();
        this.ErrorMessage = null;

        try
        {
            AppConfiguration updated = AppHost.Configuration with
            {
                OnboardingCompleted = true,
                ActiveContextId = this.Work.Id,
                Contexts = [this.Work.ToContextDefinition(), this.Personal.ToContextDefinition()]
            };

            ConfigurationSaveResult result = await this.configurationStore
                .SaveAsync(updated, CancellationToken.None)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                this.ErrorMessage = string.Join(" ", result.Errors);
                return;
            }

            await this.switchService
                .SwitchAsync(new ContextSwitchRequest(this.Work.Id, ContextSwitchSource.Dashboard), CancellationToken.None)
                .ConfigureAwait(true);

            this.Completed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            this.isFinishing = false;
            this.FinishCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Leaves the default configuration untouched but records that the wizard was shown, so it
    /// does not reappear on every launch.
    /// </summary>
    private async Task SkipAsync()
    {
        this.ErrorMessage = null;
        AppConfiguration updated = AppHost.Configuration with { OnboardingCompleted = true };
        await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        this.Completed?.Invoke(this, EventArgs.Empty);
    }
}
