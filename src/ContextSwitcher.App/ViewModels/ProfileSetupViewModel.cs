using System.Collections.ObjectModel;
using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Platform;
using System.Globalization;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Profile Setup editor (docs/architecture.md, "The main window"), reachable only from the
/// Profiles page. Edits every <see cref="ContextDefinition"/> field for one profile: the name, the
/// apps, the notes and what may fail here, and the rest through a view model per section - the look,
/// the browser, Focus, media, Docker and the quick links, each with its own rules. Nothing is written
/// to disk until <see cref="SaveCommand"/> succeeds, so a cancelled "Add new profile" never touches
/// config.
/// </summary>
public sealed class ProfileSetupViewModel : ViewModelBase
{
    private readonly IAppState appState;
    private readonly ConfigurationStore configurationStore;
    private readonly PlatformCapabilities capabilities;
    private readonly string originalContextId;

    private string displayName;
    private string notesText;
    private bool continueOnNonCriticalFailure;
    private string? errorMessage;
    private bool isSaving;

    public ProfileSetupViewModel(
        IAppState appState,
        ConfigurationStore configurationStore,
        IInstalledAppsService installedAppsService,
        ISystemCatalog catalog,
        IShell shell,
        IFocusShortcutInstaller focusShortcuts,
        PlatformCapabilities capabilities,
        IDefaultBrowser defaultBrowser,
        ContextDefinition? existing)
    {
        this.appState = appState;
        this.configurationStore = configurationStore;
        this.capabilities = capabilities;
        this.IsNew = existing is null;

        ContextDefinition source = existing ?? CreateDefaultContext(this.appState.Configuration.Contexts);
        this.originalContextId = source.Id;

        this.displayName = source.DisplayName;
        this.Look = new ProfileLookViewModel(source.AccentColor, source.Icon);

        this.Apps = new ObservableCollection<AppRowViewModel>(
            source.LaunchApps.Concat(source.CloseApps).Concat(source.HideApps)
                .Distinct(StringComparer.Ordinal)
                .Select(name => new AppRowViewModel(
                    name,
                    source.LaunchApps.Contains(name),
                    source.CloseApps.Contains(name),
                    this.RemoveApp,
                    hideOnLeave: source.HideApps.Contains(name))));

        // Constructed after Apps, since its "already added" callback reads that collection.
        this.AppPicker = new AppPickerViewModel(
            installedAppsService,
            () => this.Apps.Select(row => row.Name),
            this.AddAppByName);

        this.Browser = new BrowserSettingsViewModel(catalog, shell, capabilities, defaultBrowser.Get(), source.BrowserManagement);
        this.Focus = new FocusSettingsViewModel(catalog, shell, focusShortcuts, source.Focus);
        this.Media = new MediaSettingsViewModel(catalog, capabilities, source.Media);
        this.Docker = new DockerSettingsViewModel(catalog, source.Docker);
        this.Links = new QuickLinksSettingsViewModel(this.Browser.LoadOpenTabsAsync, source.QuickLinks);

        this.notesText = string.Join(Environment.NewLine, source.Notes);

        this.continueOnNonCriticalFailure = source.SwitchPolicy.ContinueOnNonCriticalFailure;
        this.CriticalStepOptions = CriticalStepOptionViewModel.SelectableSteps
            .Select(step => new CriticalStepOptionViewModel(
                step.Type,
                step.Label,
                source.SwitchPolicy.CriticalSteps.Contains(step.Type.ToString())))
            .ToList();

        this.AddAppCommand = new RelayCommand(() => this.Apps.Add(new AppRowViewModel(string.Empty, true, true, this.RemoveApp, isEditable: true)));
        this.SaveCommand = new AsyncRelayCommand(this.SaveAsync, () => !this.isSaving);
        this.CancelCommand = new RelayCommand(() => this.CancelRequested?.Invoke(this, EventArgs.Empty));

        _ = this.LoadInstalledAppsAsync();
    }

    /// <summary>Raised after a successful save, so <see cref="MainAppViewModel"/> returns to Profiles.</summary>
    public event EventHandler? Saved;

    /// <summary>Raised when the user cancels without saving.</summary>
    public event EventHandler? CancelRequested;

    public bool IsNew { get; }

    public string HeaderText => this.IsNew ? Strings.ProfileSetup_NewProfileHeader : Localized.Format(Strings.ProfileSetup_EditHeader, this.displayName);

    /// <summary>
    /// The profile's id, which is no longer shown or edited. An existing profile keeps the id it
    /// has - analytics sessions, <c>state.json</c>, the CLI and Shortcuts all refer to it, so a
    /// rename must not move it. A new one takes a lowercase slug of its display name, made unique
    /// against the other profiles, so <c>switch --context deep-work</c> reads the way the profile
    /// is named instead of <c>profile-3</c>.
    /// </summary>
    public string Id => this.IsNew ? ProfileIds.FromName(this.DisplayName, this.appState.Configuration.Contexts) : this.originalContextId;

    public string DisplayName
    {
        get => this.displayName;
        set
        {
            if (this.SetProperty(ref this.displayName, value))
            {
                this.OnPropertyChanged(nameof(this.HeaderText));
                this.OnPropertyChanged(nameof(this.Id));
            }
        }
    }

    /// <summary>The profile's colour and icon.</summary>
    public ProfileLookViewModel Look { get; }

    public ObservableCollection<AppRowViewModel> Apps { get; }

    /// <summary>
    /// The shared installed-app picker backing the Apps section's flyout.
    /// </summary>
    public AppPickerViewModel AppPicker { get; }

    public BrowserSettingsViewModel Browser { get; }

    /// <summary>Whether this computer has Focus modes for the profile to turn on.</summary>
    public bool ShowsFocus => this.capabilities.Focus;

    public FocusSettingsViewModel Focus { get; }

    public MediaSettingsViewModel Media { get; }

    public DockerSettingsViewModel Docker { get; }

    public QuickLinksSettingsViewModel Links { get; }

    public string NotesText
    {
        get => this.notesText;
        set => this.SetProperty(ref this.notesText, value);
    }

    public bool ContinueOnNonCriticalFailure
    {
        get => this.continueOnNonCriticalFailure;
        set => this.SetProperty(ref this.continueOnNonCriticalFailure, value);
    }

    public IReadOnlyList<CriticalStepOptionViewModel> CriticalStepOptions { get; }

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

    public RelayCommand AddAppCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// Loads the picker, then decorates already-configured rows with their real icons.
    /// </summary>
    private async Task LoadInstalledAppsAsync()
    {
        await this.AppPicker.LoadAsync().ConfigureAwait(true);

        foreach (AppRowViewModel row in this.Apps)
        {
            row.Icon = this.AppPicker.ResolveIcon(row.Name);
        }
    }

    /// <summary>
    /// Adds a picked app, defaulting to launch-on-enter and close-on-leave - the behavior someone
    /// choosing an app for a profile almost always wants, and both toggles stay editable.
    /// </summary>
    private void AddAppByName(string appName)
    {
        if (this.Apps.Any(row => string.Equals(row.Name, appName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        this.Apps.Add(new AppRowViewModel(appName, launchOnEnter: true, closeOnLeave: true, this.RemoveApp)
        {
            Icon = this.AppPicker.ResolveIcon(appName)
        });

        this.AppPicker.SearchText = string.Empty;
        this.AppPicker.Refresh();
    }

    private void RemoveApp(AppRowViewModel row)
    {
        this.Apps.Remove(row);
        this.AppPicker.Refresh();
    }

    private async Task SaveAsync()
    {
        this.isSaving = true;
        this.SaveCommand.RaiseCanExecuteChanged();
        this.ErrorMessage = null;

        try
        {
            ContextDefinition edited = this.BuildContextDefinition();

            List<ContextDefinition> contexts = [.. this.appState.Configuration.Contexts];
            int existingIndex = contexts.FindIndex(c => c.Id == this.originalContextId);
            if (existingIndex >= 0)
            {
                contexts[existingIndex] = edited;
            }
            else
            {
                contexts.Add(edited);
            }

            AppConfiguration updated = this.appState.Configuration with
            {
                Contexts = contexts,
                ActiveContextId = this.appState.Configuration.ActiveContextId == this.originalContextId
                    ? edited.Id
                    : this.appState.Configuration.ActiveContextId
            };

            ConfigurationSaveResult result = await this.configurationStore.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                this.ErrorMessage = string.Join(" ", result.Errors);
                return;
            }

            this.Saved?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            this.isSaving = false;
            this.SaveCommand.RaiseCanExecuteChanged();
        }
    }

    private ContextDefinition BuildContextDefinition()
    {
        return new ContextDefinition
        {
            Id = this.Id.Trim(),
            DisplayName = this.DisplayName.Trim(),

            // Not edited any more - nothing draws it. Kept in step with the name for the config
            // file's readers, the way onboarding has always written it.
            MenuBarLabel = this.DisplayName.Trim().ToUpperInvariant(),
            AccentColor = this.Look.AccentColor.Trim(),
            Icon = this.Look.Icon.Trim(),
            LaunchApps = this.Apps.Where(a => a.LaunchOnEnter && !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name.Trim()).ToList(),
            CloseApps = this.Apps.Where(a => a.CloseOnLeave && !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name.Trim()).ToList(),
            HideApps = this.Apps.Where(a => a.HideOnLeave && !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name.Trim()).ToList(),
            BrowserManagement = this.Browser.ToConfig(),
            Focus = this.Focus.ToConfig(),
            Media = this.Media.ToConfig(),
            Docker = this.Docker.ToConfig(),
            QuickLinks = this.Links.ToConfig(),
            Notes = this.NotesText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            SwitchPolicy = new SwitchPolicyConfig
            {
                ContinueOnNonCriticalFailure = this.ContinueOnNonCriticalFailure,
                CriticalSteps = this.CriticalStepOptions.Where(o => o.IsSelected).Select(o => o.StepType.ToString()).ToList()
            }
        };
    }

    private static ContextDefinition CreateDefaultContext(IReadOnlyList<ContextDefinition> existing)
    {
        return new ContextDefinition
        {
            Id = ProfileIds.ForNewProfile(existing),
            DisplayName = Strings.ProfileSetup_NewProfileName,
            MenuBarLabel = Strings.ProfileSetup_NewProfileName.ToUpper(CultureInfo.CurrentCulture),
            AccentColor = "#2F6FED",
            Icon = "circle"
        };
    }
}
