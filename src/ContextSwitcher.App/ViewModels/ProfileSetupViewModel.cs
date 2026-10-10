using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Profile Setup editor (agent.md section 11.1.2), reachable only from the Profiles
/// page. Edits every <see cref="ContextDefinition"/> field for one profile. Nothing is written to disk until
/// <see cref="SaveCommand"/> succeeds, so a cancelled "Add new profile" never touches config.
/// </summary>
public sealed class ProfileSetupViewModel : ViewModelBase
{
    private readonly ConfigurationStore configurationStore;
    private readonly string originalContextId;

    private readonly ISystemCatalog catalog;

    private readonly AccentChoiceViewModel customAccent;

    private string displayName;
    private string accentColor;
    private bool customAccentChosen;
    private string icon;

    private BrowserManagementMode browserMode;
    private BrowserKind browserKind;
    private bool avoidDuplicateTabs;

    private string notesText;

    private bool continueOnNonCriticalFailure;


    private string? errorMessage;
    private bool isSaving;

    public ProfileSetupViewModel(
        ConfigurationStore configurationStore,
        IInstalledAppsService installedAppsService,
        ISystemCatalog catalog,
        IProcessRunner processRunner,
        IFocusShortcutInstaller focusShortcuts,
        ContextDefinition? existing)
    {
        this.configurationStore = configurationStore;
        this.catalog = catalog;
        this.IsNew = existing is null;

        ContextDefinition source = existing ?? CreateDefaultContext(AppHost.Configuration.Contexts);
        this.originalContextId = source.Id;

        this.displayName = source.DisplayName;
        this.accentColor = source.AccentColor;
        this.customAccent = AccentChoiceViewModel.Custom(source.AccentColor);
        this.AccentChoices = [.. AccentPalette.Select(entry => AccentChoiceViewModel.Preset(entry.Hex, entry.Name)), this.customAccent];
        this.icon = source.Icon;
        this.IconChoices = ProfileIcons.All.Select(icon => new IconChoiceViewModel(icon)).ToList();

        this.Apps = new ObservableCollection<AppRowViewModel>(
            source.LaunchApps.Concat(source.CloseApps)
                .Distinct(StringComparer.Ordinal)
                .Select(name => new AppRowViewModel(
                    name,
                    source.LaunchApps.Contains(name),
                    source.CloseApps.Contains(name),
                    this.RemoveApp)));

        // Constructed after Apps, since its "already added" callback reads that collection.
        this.AppPicker = new AppPickerViewModel(
            installedAppsService,
            () => this.Apps.Select(row => row.Name),
            this.AddAppByName);

        this.browserMode = source.BrowserManagement.Mode;
        this.browserKind = source.BrowserManagement.Browser;
        this.avoidDuplicateTabs = source.BrowserManagement.AvoidDuplicateTabs;
        this.BrowserUrls = new ObservableCollection<EditableStringRowViewModel>(
            source.BrowserManagement.Urls.Select(url => this.UrlRow(url)));
        this.TabGroups = new ObservableCollection<EditableStringRowViewModel>(
            source.BrowserManagement.TabGroups.Select(group => this.TabGroupRow(group)));
        this.BrowserProfiles = new ObservableCollection<BrowserProfileRowViewModel>(
            source.BrowserManagement.Profiles.Select(profile => new BrowserProfileRowViewModel(
                profile.Browser, profile.ProfileDirectory, profile.Urls, catalog.GetBrowserProfiles, this.RemoveBrowserProfile)));
        this.UrlTabPicker = new ChoicePickerViewModel(
            this.LoadOpenTabsAsync,
            () => this.BrowserUrls.Select(row => row.Value.Trim()),
            choice => this.BrowserUrls.Add(this.UrlRow(choice.Value)),
            OpenTabsEmptyText,
            OpenTabsUnavailableText,
            "IconGlobe",
            OpenTabsSearchText);

        this.Focus = new FocusSettingsViewModel(catalog, processRunner, focusShortcuts, source.Focus);
        this.Media = new MediaSettingsViewModel(catalog, source.Media);

        // Containers already in config are names Docker gave them; shown, not offered for editing.
        this.DockerStart = new ObservableCollection<EditableStringRowViewModel>(
            source.Docker.Start.Select(name => this.ContainerRow(name, this.RemoveDockerStart, isEditable: false)));
        this.DockerStop = new ObservableCollection<EditableStringRowViewModel>(
            source.Docker.Stop.Select(name => this.ContainerRow(name, this.RemoveDockerStop, isEditable: false)));
        this.DockerStartPicker = new ChoicePickerViewModel(
            this.LoadContainersAsync,
            () => this.DockerStart.Select(row => row.Value.Trim()),
            choice => this.DockerStart.Add(this.ContainerRow(choice.Value, this.RemoveDockerStart, isEditable: false)),
            ContainersEmptyText,
            ContainersUnavailableText,
            "IconBox",
            ContainersSearchText);
        this.DockerStopPicker = new ChoicePickerViewModel(
            this.LoadContainersAsync,
            () => this.DockerStop.Select(row => row.Value.Trim()),
            choice => this.DockerStop.Add(this.ContainerRow(choice.Value, this.RemoveDockerStop, isEditable: false)),
            ContainersEmptyText,
            ContainersUnavailableText,
            "IconBox",
            ContainersSearchText);

        this.QuickLinks = new ObservableCollection<QuickLinkRowViewModel>(
            source.QuickLinks.Select(link => new QuickLinkRowViewModel(link.Title, link.Url, link.Icon, this.RemoveQuickLink)));
        this.QuickLinkTabPicker = new ChoicePickerViewModel(
            this.LoadOpenTabsAsync,
            () => this.QuickLinks.Select(row => row.Url.Trim()),
            choice => this.QuickLinks.Add(new QuickLinkRowViewModel(choice.Title, choice.Value, "link", this.RemoveQuickLink)),
            OpenTabsEmptyText,
            OpenTabsUnavailableText,
            "IconGlobe",
            OpenTabsSearchText);

        this.notesText = string.Join(Environment.NewLine, source.Notes);

        this.continueOnNonCriticalFailure = source.SwitchPolicy.ContinueOnNonCriticalFailure;
        this.CriticalStepOptions = CriticalStepOptionViewModel.SelectableSteps
            .Select(step => new CriticalStepOptionViewModel(
                step.Type,
                step.Label,
                source.SwitchPolicy.CriticalSteps.Contains(step.Type.ToString())))
            .ToList();

        this.AddAppCommand = new RelayCommand(() => this.Apps.Add(new AppRowViewModel(string.Empty, true, true, this.RemoveApp, isEditable: true)));
        this.AddBrowserUrlCommand = new RelayCommand(() => this.BrowserUrls.Add(this.UrlRow(string.Empty)));
        this.AddTabGroupCommand = new RelayCommand(() => this.TabGroups.Add(this.TabGroupRow(string.Empty)));
        this.AddBrowserProfileCommand = new RelayCommand(() => this.BrowserProfiles.Add(
            new BrowserProfileRowViewModel(BrowserKind.Chrome, string.Empty, [], catalog.GetBrowserProfiles, this.RemoveBrowserProfile)));
        this.AddDockerStartCommand = new RelayCommand(() => this.DockerStart.Add(this.ContainerRow(string.Empty, this.RemoveDockerStart, isEditable: true)));
        this.AddDockerStopCommand = new RelayCommand(() => this.DockerStop.Add(this.ContainerRow(string.Empty, this.RemoveDockerStop, isEditable: true)));
        this.AddQuickLinkCommand = new RelayCommand(() => this.QuickLinks.Add(new QuickLinkRowViewModel(string.Empty, string.Empty, "link", this.RemoveQuickLink)));

        this.SaveCommand = new AsyncRelayCommand(this.SaveAsync, () => !this.isSaving);
        this.CancelCommand = new RelayCommand(() => this.CancelRequested?.Invoke(this, EventArgs.Empty));

        _ = this.LoadInstalledAppsAsync();
    }

    /// <summary>Raised after a successful save, so <see cref="MainAppViewModel"/> returns to Profiles.</summary>
    public event EventHandler? Saved;

    /// <summary>Raised when the user cancels without saving.</summary>
    public event EventHandler? CancelRequested;

    public bool IsNew { get; }

    public string HeaderText => this.IsNew ? "New Profile" : $"Edit {this.displayName}";

    /// <summary>
    /// The profile's id, which is no longer shown or edited. An existing profile keeps the id it
    /// has - analytics sessions, <c>state.json</c>, the CLI and Shortcuts all refer to it, so a
    /// rename must not move it. A new one takes a lowercase slug of its display name, made unique
    /// against the other profiles, so <c>switch --context deep-work</c> reads the way the profile
    /// is named instead of <c>profile-3</c>.
    /// </summary>
    public string Id => this.IsNew ? UniqueSlug(this.DisplayName, AppHost.Configuration.Contexts) : this.originalContextId;

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

    public string AccentColor
    {
        get => this.accentColor;
        set
        {
            if (this.SetProperty(ref this.accentColor, value))
            {
                this.customAccent.Brush = this.AccentBrush;
                this.OnPropertyChanged(nameof(this.AccentBrush));
                this.OnPropertyChanged(nameof(this.AccentColorValue));
                this.OnPropertyChanged(nameof(this.IsCustomAccent));
                this.OnPropertyChanged(nameof(this.SelectedAccent));
            }
        }
    }

    public Avalonia.Media.IBrush AccentBrush => AccentColorParser.ToBrush(this.AccentColor);

    /// <summary>
    /// The accent dropdown: the presets, then "Custom". The presets are chosen to stay legible under
    /// the white glyph drawn on top of them, which rules out a yellow; the first and the green are
    /// the two colors onboarding gives its profiles.
    /// </summary>
    public IReadOnlyList<AccentChoiceViewModel> AccentChoices { get; }

    public AccentChoiceViewModel SelectedAccent
    {
        get => this.IsCustomAccent ? this.customAccent : this.MatchingPreset() ?? this.customAccent;
        set
        {
            // A ComboBox writes null while its items are being swapped; that is not a choice.
            if (value is null)
            {
                return;
            }

            this.customAccentChosen = value.IsCustom;
            if (value.Hex is not null)
            {
                this.AccentColor = value.Hex;
            }

            this.OnPropertyChanged(nameof(this.IsCustomAccent));
            this.OnPropertyChanged(nameof(this.SelectedAccent));
        }
    }

    /// <summary>
    /// The color is custom, so the color picker shows under the dropdown: either "Custom" was chosen,
    /// or the color is none of the presets (a saved custom color, or one typed into the config). It
    /// stays custom while the picker is dragged, even across a preset's exact color, so the picker
    /// does not vanish from under the pointer.
    /// </summary>
    public bool IsCustomAccent => this.customAccentChosen || this.MatchingPreset() is null;

    /// <summary>
    /// The accent as a <see cref="Avalonia.Media.Color"/> for the custom color picker. Config keeps
    /// the plain #RRGGBB string it always has; the picker's alpha is ignored.
    /// </summary>
    public Avalonia.Media.Color AccentColorValue
    {
        get => Avalonia.Media.Color.TryParse(this.AccentColor, out Avalonia.Media.Color color) ? color : Avalonia.Media.Colors.Gray;
        set => this.AccentColor = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    /// <summary>
    /// The stored icon name. An unknown name - a hand-edited config, or one saved by a build with more
    /// icons - is kept as-is until the user picks another, and is drawn as the fallback meanwhile.
    /// </summary>
    public string Icon
    {
        get => this.icon;
        set
        {
            if (this.SetProperty(ref this.icon, value))
            {
                this.OnPropertyChanged(nameof(this.SelectedIcon));
            }
        }
    }

    /// <summary>Every icon a profile can use, each drawn beside its name in the dropdown.</summary>
    public IReadOnlyList<IconChoiceViewModel> IconChoices { get; }

    /// <summary>
    /// The dropdown's entry for <see cref="Icon"/>, or the fallback's for a name this build does not
    /// know - which only shows it; the stored name changes only when another icon is picked.
    /// </summary>
    public IconChoiceViewModel SelectedIcon
    {
        get => this.IconChoices.First(choice => choice.Name == ProfileIcons.Find(this.Icon).Name);
        set
        {
            if (value is not null)
            {
                this.Icon = value.Name;
            }
        }
    }

    public ObservableCollection<AppRowViewModel> Apps { get; }

    /// <summary>
    /// The shared installed-app picker backing the Apps section's flyout.
    /// </summary>
    public AppPickerViewModel AppPicker { get; }

    public IReadOnlyList<Choice<BrowserManagementMode>> BrowserModeChoices { get; } =
    [
        new(BrowserManagementMode.None, "Don't open anything"),
        new(BrowserManagementMode.Urls, "Open web pages"),
        new(BrowserManagementMode.Groups, "Open tab groups"),
        new(BrowserManagementMode.Profiles, "Open browser profiles")
    ];

    public IReadOnlyList<Choice<BrowserKind>> BrowserKindChoices { get; } =
    [
        new(BrowserKind.Default, "Default browser"),
        new(BrowserKind.Safari, "Safari"),
        new(BrowserKind.Chrome, "Google Chrome"),
        new(BrowserKind.Brave, "Brave")
    ];

    public BrowserManagementMode BrowserMode
    {
        get => this.browserMode;
        set
        {
            if (this.SetProperty(ref this.browserMode, value))
            {
                this.OnPropertyChanged(nameof(this.SelectedBrowserMode));
                this.OnPropertyChanged(nameof(this.IsUrlsMode));
                this.OnPropertyChanged(nameof(this.IsGroupsMode));
                this.OnPropertyChanged(nameof(this.IsProfilesMode));
                this.OnPropertyChanged(nameof(this.ShowsBrowserChoice));
            }
        }
    }

    public Choice<BrowserManagementMode> SelectedBrowserMode
    {
        get => this.BrowserModeChoices.First(choice => choice.Value == this.BrowserMode);
        set
        {
            // A ComboBox writes null while its items are being swapped; that is not a choice.
            if (value is not null)
            {
                this.BrowserMode = value.Value;
            }
        }
    }

    /// <summary>
    /// Which browser only matters for opening pages and tab groups: profiles name their own browser
    /// per row, and None opens nothing.
    /// </summary>
    public bool ShowsBrowserChoice => this.IsUrlsMode || this.IsGroupsMode;

    public bool IsUrlsMode => this.BrowserMode == BrowserManagementMode.Urls;

    public bool IsGroupsMode => this.BrowserMode == BrowserManagementMode.Groups;

    public bool IsProfilesMode => this.BrowserMode == BrowserManagementMode.Profiles;

    public BrowserKind BrowserKind
    {
        get => this.browserKind;
        set
        {
            if (this.SetProperty(ref this.browserKind, value))
            {
                this.OnPropertyChanged(nameof(this.SelectedBrowserKind));
                this.OnPropertyChanged(nameof(this.CanAvoidDuplicateTabs));
                this.OnPropertyChanged(nameof(this.AvoidDuplicateTabsHint));
            }
        }
    }

    public Choice<BrowserKind> SelectedBrowserKind
    {
        get => this.BrowserKindChoices.First(choice => choice.Value == this.BrowserKind);
        set
        {
            if (value is not null)
            {
                this.BrowserKind = value.Value;
            }
        }
    }

    /// <summary>
    /// Whether duplicate-tab avoidance can do anything at all. Finding an already-open tab means
    /// scripting a named browser, and the default browser cannot be named ahead of time - so with
    /// <see cref="BrowserKind.Default"/> the setting is inert and a new tab opens every switch.
    /// Surfacing that here is the difference between a toggle that lies and one that explains.
    /// </summary>
    public bool CanAvoidDuplicateTabs => this.BrowserKind != BrowserKind.Default;

    public string AvoidDuplicateTabsHint => this.CanAvoidDuplicateTabs
        ? "Focuses a matching tab instead of opening a second one."
        : "Needs a specific browser - the default browser can't be checked for open tabs.";

    public bool AvoidDuplicateTabs
    {
        get => this.avoidDuplicateTabs;
        set => this.SetProperty(ref this.avoidDuplicateTabs, value);
    }

    public ObservableCollection<EditableStringRowViewModel> BrowserUrls { get; }

    public ObservableCollection<EditableStringRowViewModel> TabGroups { get; }

    public ObservableCollection<BrowserProfileRowViewModel> BrowserProfiles { get; }

    /// <summary>"From open tabs" under Startup URLs: the pages open in the chosen browser right now.</summary>
    public ChoicePickerViewModel UrlTabPicker { get; }

    public FocusSettingsViewModel Focus { get; }

    public MediaSettingsViewModel Media { get; }

    public ObservableCollection<EditableStringRowViewModel> DockerStart { get; }

    public ObservableCollection<EditableStringRowViewModel> DockerStop { get; }

    /// <summary>"+ Add container" for Start: the containers Docker has, or a note that it isn't running.</summary>
    public ChoicePickerViewModel DockerStartPicker { get; }

    /// <summary>"+ Add container" for Stop.</summary>
    public ChoicePickerViewModel DockerStopPicker { get; }

    public ObservableCollection<QuickLinkRowViewModel> QuickLinks { get; }

    /// <summary>"From open tabs" under Quick links: picking a tab fills in both its title and URL.</summary>
    public ChoicePickerViewModel QuickLinkTabPicker { get; }

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

    public RelayCommand AddBrowserUrlCommand { get; }

    public RelayCommand AddTabGroupCommand { get; }

    public RelayCommand AddBrowserProfileCommand { get; }

    public RelayCommand AddDockerStartCommand { get; }

    public RelayCommand AddDockerStopCommand { get; }

    public RelayCommand AddQuickLinkCommand { get; }

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

    private void RemoveBrowserUrl(EditableStringRowViewModel row)
    {
        this.BrowserUrls.Remove(row);
        this.UrlTabPicker.Refresh();
    }

    private void RemoveTabGroup(EditableStringRowViewModel row) => this.TabGroups.Remove(row);

    private void RemoveBrowserProfile(BrowserProfileRowViewModel row) => this.BrowserProfiles.Remove(row);

    private void RemoveDockerStart(EditableStringRowViewModel row)
    {
        this.DockerStart.Remove(row);
        this.DockerStartPicker.Refresh();
    }

    private void RemoveDockerStop(EditableStringRowViewModel row)
    {
        this.DockerStop.Remove(row);
        this.DockerStopPicker.Refresh();
    }

    private void RemoveQuickLink(QuickLinkRowViewModel row)
    {
        this.QuickLinks.Remove(row);
        this.QuickLinkTabPicker.Refresh();
    }

    private EditableStringRowViewModel UrlRow(string url) =>
        new(url, this.RemoveBrowserUrl, isEditable: true, "IconGlobe", "https://…");

    private EditableStringRowViewModel TabGroupRow(string name) =>
        new(name, this.RemoveTabGroup, isEditable: true, "IconLayers", "Tab group name");

    private EditableStringRowViewModel ContainerRow(string name, Action<EditableStringRowViewModel> remove, bool isEditable) =>
        new(name, remove, isEditable, "IconBox", "Container name");

    private async Task<IReadOnlyList<PickerChoice>?> LoadOpenTabsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OpenTab> tabs = await this.catalog.GetOpenTabsAsync(this.BrowserKind, cancellationToken).ConfigureAwait(true);
        return tabs.Select(tab => new PickerChoice(tab.Url, tab.Title, HostOf(tab.Url))).ToList();
    }

    private async Task<IReadOnlyList<PickerChoice>?> LoadContainersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? containers = await this.catalog.GetDockerContainersAsync(cancellationToken).ConfigureAwait(true);
        return containers?.Select(name => new PickerChoice(name, name)).ToList();
    }

    private static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host : string.Empty;

    private async Task SaveAsync()
    {
        this.isSaving = true;
        this.SaveCommand.RaiseCanExecuteChanged();
        this.ErrorMessage = null;

        try
        {
            ContextDefinition edited = this.BuildContextDefinition();

            List<ContextDefinition> contexts = [.. AppHost.Configuration.Contexts];
            int existingIndex = contexts.FindIndex(c => c.Id == this.originalContextId);
            if (existingIndex >= 0)
            {
                contexts[existingIndex] = edited;
            }
            else
            {
                contexts.Add(edited);
            }

            AppConfiguration updated = AppHost.Configuration with
            {
                Contexts = contexts,
                ActiveContextId = AppHost.Configuration.ActiveContextId == this.originalContextId
                    ? edited.Id
                    : AppHost.Configuration.ActiveContextId
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
            AccentColor = this.AccentColor.Trim(),
            Icon = this.Icon.Trim(),
            LaunchApps = this.Apps.Where(a => a.LaunchOnEnter && !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name.Trim()).ToList(),
            CloseApps = this.Apps.Where(a => a.CloseOnLeave && !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name.Trim()).ToList(),
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = this.BrowserMode,
                Browser = this.BrowserKind,
                Urls = this.BrowserUrls.Where(u => !string.IsNullOrWhiteSpace(u.Value)).Select(u => u.Value.Trim()).ToList(),
                TabGroups = this.TabGroups.Where(g => !string.IsNullOrWhiteSpace(g.Value)).Select(g => g.Value.Trim()).ToList(),
                Profiles = this.BrowserProfiles
                    .Where(p => !string.IsNullOrWhiteSpace(p.ProfileDirectory))
                    .Select(p => new BrowserProfileConfig { Browser = p.Browser, ProfileDirectory = p.ProfileDirectory.Trim(), Urls = p.ParseUrls() })
                    .ToList(),
                AvoidDuplicateTabs = this.AvoidDuplicateTabs
            },
            Focus = this.Focus.ToConfig(),
            Media = this.Media.ToConfig(),
            Docker = new DockerResourceConfig
            {
                Start = this.DockerStart.Where(d => !string.IsNullOrWhiteSpace(d.Value)).Select(d => d.Value.Trim()).ToList(),
                Stop = this.DockerStop.Where(d => !string.IsNullOrWhiteSpace(d.Value)).Select(d => d.Value.Trim()).ToList()
            },
            QuickLinks = this.QuickLinks
                .Where(q => !string.IsNullOrWhiteSpace(q.Url))
                .Select(q => new QuickLinkConfig
                {
                    // The dashboard shows a link by its title alone, so an untitled one would be a
                    // blank button; the site's name is the obvious stand-in.
                    Title = string.IsNullOrWhiteSpace(q.Title) ? HostOf(q.Url.Trim()) : q.Title.Trim(),
                    Url = q.Url.Trim(),
                    Icon = string.IsNullOrWhiteSpace(q.Icon) ? "link" : q.Icon.Trim()
                })
                .ToList(),
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

    private const string OpenTabsEmptyText =
        "No other web pages are open. Open the ones you want in your browser, then try again.";

    private const string OpenTabsUnavailableText = "Couldn't read your browser's tabs.";

    private const string ContainersEmptyText = "No other containers to add.";

    private const string OpenTabsSearchText = "Search open tabs…";

    private const string ContainersSearchText = "Search containers…";

    private const string ContainersUnavailableText =
        "Docker isn't running. Start Docker Desktop to choose a container, or type its name.";

    private static readonly (string Hex, string Name)[] AccentPalette =
    [
        ("#2F6FED", "Blue"),
        ("#7B61FF", "Violet"),
        ("#D6409F", "Pink"),
        ("#E5484D", "Red"),
        ("#F2994A", "Orange"),
        ("#20A67A", "Green"),
        ("#0FA3B1", "Teal"),
        ("#6E7781", "Graphite")
    ];

    private AccentChoiceViewModel? MatchingPreset() =>
        this.AccentChoices.FirstOrDefault(choice => string.Equals(choice.Hex, this.AccentColor?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Lowercases the name and joins its letters and digits with hyphens, which is exactly what the
    /// id pattern accepts, then suffixes -2, -3... past any id already taken. A name with nothing
    /// usable in it (all punctuation, or a non-Latin script) falls back to "profile".
    /// </summary>
    private static string UniqueSlug(string displayName, IReadOnlyList<ContextDefinition> existing)
    {
        string slug = Regex.Replace(displayName.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0)
        {
            slug = "profile";
        }

        HashSet<string> taken = existing.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        string candidate = slug;
        for (int suffix = 2; taken.Contains(candidate); suffix++)
        {
            candidate = $"{slug}-{suffix}";
        }

        return candidate;
    }

    private static ContextDefinition CreateDefaultContext(IReadOnlyList<ContextDefinition> existing)
    {
        HashSet<string> existingIds = existing.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        string id = "profile";
        int suffix = 2;
        while (existingIds.Contains(id))
        {
            id = $"profile-{suffix}";
            suffix++;
        }

        return new ContextDefinition
        {
            Id = id,
            DisplayName = "New Profile",
            MenuBarLabel = "NEW",
            AccentColor = "#2F6FED",
            Icon = "circle"
        };
    }
}
