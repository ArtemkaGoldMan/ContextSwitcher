using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Hotkeys;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Backs the Profile Setup editor (agent.md section 11.1.2), reachable only from the Profiles
/// page. Edits every <see cref="ContextDefinition"/> field for one profile, plus this profile's
/// entry (if any) in the app-level <c>hotkeys[]</c> list. Nothing is written to disk until
/// <see cref="SaveCommand"/> succeeds, so a cancelled "Add new profile" never touches config.
/// </summary>
public sealed class ProfileSetupViewModel : ViewModelBase
{
    private readonly ConfigurationStore configurationStore;
    private readonly string originalContextId;

    private string id;
    private string displayName;
    private string menuBarLabel;
    private string accentColor;
    private string icon;

    private BrowserManagementMode browserMode;
    private BrowserKind browserKind;
    private bool avoidDuplicateTabs;

    private ThemeMode themeMode;

    private string wallpaperPath;
    private bool wallpaperAllSpaces;

    private bool focusEnabled;
    private string focusModeName;

    private MediaPlayerKind mediaPlayer;
    private string mediaPlaylist;
    private bool mediaAutoPlay;

    private string notesText;

    private bool continueOnNonCriticalFailure;

    private string hotkeyAccelerator;
    private bool hotkeyEnabled;

    private string? errorMessage;
    private bool isSaving;

    public ProfileSetupViewModel(
        ConfigurationStore configurationStore,
        IInstalledAppsService installedAppsService,
        ContextDefinition? existing)
    {
        this.configurationStore = configurationStore;
        this.AppPicker = new AppPickerViewModel(
            installedAppsService,
            () => this.Apps.Select(row => row.Name),
            this.AddAppByName);
        this.IsNew = existing is null;

        ContextDefinition source = existing ?? CreateDefaultContext(AppHost.Configuration.Contexts);
        this.originalContextId = source.Id;

        this.id = source.Id;
        this.displayName = source.DisplayName;
        this.menuBarLabel = source.MenuBarLabel;
        this.accentColor = source.AccentColor;
        this.icon = source.Icon;

        this.Apps = new ObservableCollection<AppRowViewModel>(
            source.LaunchApps.Concat(source.CloseApps)
                .Distinct(StringComparer.Ordinal)
                .Select(name => new AppRowViewModel(
                    name,
                    source.LaunchApps.Contains(name),
                    source.CloseApps.Contains(name),
                    this.RemoveApp)));

        this.browserMode = source.BrowserManagement.Mode;
        this.browserKind = source.BrowserManagement.Browser;
        this.avoidDuplicateTabs = source.BrowserManagement.AvoidDuplicateTabs;
        this.BrowserUrls = new ObservableCollection<EditableStringRowViewModel>(
            source.BrowserManagement.Urls.Select(url => new EditableStringRowViewModel(url, this.RemoveBrowserUrl)));
        this.TabGroups = new ObservableCollection<EditableStringRowViewModel>(
            source.BrowserManagement.TabGroups.Select(group => new EditableStringRowViewModel(group, this.RemoveTabGroup)));
        this.BrowserProfiles = new ObservableCollection<BrowserProfileRowViewModel>(
            source.BrowserManagement.Profiles.Select(profile => new BrowserProfileRowViewModel(
                profile.Browser, profile.ProfileDirectory, profile.Urls, this.RemoveBrowserProfile)));

        this.themeMode = source.Theme.Mode;

        this.wallpaperPath = source.Wallpaper.Path;
        this.wallpaperAllSpaces = source.Wallpaper.AllSpaces;

        this.focusEnabled = source.Focus.Enabled;
        this.focusModeName = source.Focus.ModeName;

        this.mediaPlayer = source.Media.Player;
        this.mediaPlaylist = source.Media.Playlist;
        this.mediaAutoPlay = source.Media.AutoPlay;

        this.DockerStart = new ObservableCollection<EditableStringRowViewModel>(
            source.Docker.Start.Select(name => new EditableStringRowViewModel(name, this.RemoveDockerStart)));
        this.DockerStop = new ObservableCollection<EditableStringRowViewModel>(
            source.Docker.Stop.Select(name => new EditableStringRowViewModel(name, this.RemoveDockerStop)));

        this.QuickLinks = new ObservableCollection<QuickLinkRowViewModel>(
            source.QuickLinks.Select(link => new QuickLinkRowViewModel(link.Title, link.Url, link.Icon, this.RemoveQuickLink)));

        this.notesText = string.Join(Environment.NewLine, source.Notes);

        this.continueOnNonCriticalFailure = source.SwitchPolicy.ContinueOnNonCriticalFailure;
        this.CriticalStepOptions = CriticalStepOptionViewModel.SelectableStepTypes
            .Select(stepType => new CriticalStepOptionViewModel(
                stepType,
                FormatStepTypeName(stepType),
                source.SwitchPolicy.CriticalSteps.Contains(stepType.ToString())))
            .ToList();

        HotkeyConfig? hotkey = AppHost.Configuration.Hotkeys.FirstOrDefault(h => h.ContextId == this.originalContextId);
        this.hotkeyAccelerator = hotkey?.Accelerator ?? string.Empty;
        this.hotkeyEnabled = hotkey?.Enabled ?? true;

        this.AddAppCommand = new RelayCommand(() => this.Apps.Add(new AppRowViewModel(string.Empty, true, true, this.RemoveApp)));
        this.AddBrowserUrlCommand = new RelayCommand(() => this.BrowserUrls.Add(new EditableStringRowViewModel(string.Empty, this.RemoveBrowserUrl)));
        this.AddTabGroupCommand = new RelayCommand(() => this.TabGroups.Add(new EditableStringRowViewModel(string.Empty, this.RemoveTabGroup)));
        this.AddBrowserProfileCommand = new RelayCommand(() => this.BrowserProfiles.Add(
            new BrowserProfileRowViewModel(BrowserKind.Chrome, string.Empty, [], this.RemoveBrowserProfile)));
        this.AddDockerStartCommand = new RelayCommand(() => this.DockerStart.Add(new EditableStringRowViewModel(string.Empty, this.RemoveDockerStart)));
        this.AddDockerStopCommand = new RelayCommand(() => this.DockerStop.Add(new EditableStringRowViewModel(string.Empty, this.RemoveDockerStop)));
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
    /// The id is only editable for a brand-new profile; existing profiles keep a stable id since
    /// hotkeys, analytics sessions, and <c>state.json</c> reference it (agent.md section 6.1).
    /// </summary>
    public bool IsIdEditable => this.IsNew;

    public string Id
    {
        get => this.id;
        set => this.SetProperty(ref this.id, value);
    }

    public string DisplayName
    {
        get => this.displayName;
        set
        {
            if (this.SetProperty(ref this.displayName, value))
            {
                this.OnPropertyChanged(nameof(this.HeaderText));
            }
        }
    }

    public string MenuBarLabel
    {
        get => this.menuBarLabel;
        set => this.SetProperty(ref this.menuBarLabel, value);
    }

    public string AccentColor
    {
        get => this.accentColor;
        set
        {
            if (this.SetProperty(ref this.accentColor, value))
            {
                this.OnPropertyChanged(nameof(this.AccentBrush));
            }
        }
    }

    public Avalonia.Media.IBrush AccentBrush => AccentColorParser.ToBrush(this.AccentColor);

    public string Icon
    {
        get => this.icon;
        set => this.SetProperty(ref this.icon, value);
    }

    public ObservableCollection<AppRowViewModel> Apps { get; }

    /// <summary>
    /// The shared installed-app picker backing the Apps section's flyout.
    /// </summary>
    public AppPickerViewModel AppPicker { get; }

    public IReadOnlyList<BrowserManagementMode> BrowserModes { get; } = Enum.GetValues<BrowserManagementMode>();

    public IReadOnlyList<BrowserKind> BrowserKinds { get; } = Enum.GetValues<BrowserKind>();

    public BrowserManagementMode BrowserMode
    {
        get => this.browserMode;
        set
        {
            if (this.SetProperty(ref this.browserMode, value))
            {
                this.OnPropertyChanged(nameof(this.IsUrlsMode));
                this.OnPropertyChanged(nameof(this.IsGroupsMode));
                this.OnPropertyChanged(nameof(this.IsProfilesMode));
            }
        }
    }

    public bool IsUrlsMode => this.BrowserMode == BrowserManagementMode.Urls;

    public bool IsGroupsMode => this.BrowserMode == BrowserManagementMode.Groups;

    public bool IsProfilesMode => this.BrowserMode == BrowserManagementMode.Profiles;

    public BrowserKind BrowserKind
    {
        get => this.browserKind;
        set => this.SetProperty(ref this.browserKind, value);
    }

    public bool AvoidDuplicateTabs
    {
        get => this.avoidDuplicateTabs;
        set => this.SetProperty(ref this.avoidDuplicateTabs, value);
    }

    public ObservableCollection<EditableStringRowViewModel> BrowserUrls { get; }

    public ObservableCollection<EditableStringRowViewModel> TabGroups { get; }

    public ObservableCollection<BrowserProfileRowViewModel> BrowserProfiles { get; }

    public IReadOnlyList<ThemeMode> ThemeModes { get; } = Enum.GetValues<ThemeMode>();

    public ThemeMode ThemeMode
    {
        get => this.themeMode;
        set => this.SetProperty(ref this.themeMode, value);
    }

    public string WallpaperPath
    {
        get => this.wallpaperPath;
        set => this.SetProperty(ref this.wallpaperPath, value);
    }

    public bool WallpaperAllSpaces
    {
        get => this.wallpaperAllSpaces;
        set => this.SetProperty(ref this.wallpaperAllSpaces, value);
    }

    public bool FocusEnabled
    {
        get => this.focusEnabled;
        set => this.SetProperty(ref this.focusEnabled, value);
    }

    public string FocusModeName
    {
        get => this.focusModeName;
        set => this.SetProperty(ref this.focusModeName, value);
    }

    public IReadOnlyList<MediaPlayerKind> MediaPlayers { get; } = Enum.GetValues<MediaPlayerKind>();

    public MediaPlayerKind MediaPlayer
    {
        get => this.mediaPlayer;
        set => this.SetProperty(ref this.mediaPlayer, value);
    }

    public string MediaPlaylist
    {
        get => this.mediaPlaylist;
        set => this.SetProperty(ref this.mediaPlaylist, value);
    }

    public bool MediaAutoPlay
    {
        get => this.mediaAutoPlay;
        set => this.SetProperty(ref this.mediaAutoPlay, value);
    }

    public ObservableCollection<EditableStringRowViewModel> DockerStart { get; }

    public ObservableCollection<EditableStringRowViewModel> DockerStop { get; }

    public ObservableCollection<QuickLinkRowViewModel> QuickLinks { get; }

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

    public string HotkeyAccelerator
    {
        get => this.hotkeyAccelerator;
        set
        {
            if (this.SetProperty(ref this.hotkeyAccelerator, value))
            {
                this.OnPropertyChanged(nameof(this.HotkeyAcceleratorLooksInvalid));
            }
        }
    }

    /// <summary>
    /// Non-blocking hint only - like <see cref="Core.Configuration.Validation.ConfigurationValidator"/>,
    /// this never rejects a save; an unparseable accelerator is just logged as a warning when
    /// hotkeys register at next startup.
    /// </summary>
    public bool HotkeyAcceleratorLooksInvalid =>
        !string.IsNullOrWhiteSpace(this.HotkeyAccelerator) &&
        !AcceleratorParser.TryParse(this.HotkeyAccelerator, out _, out _);

    public bool HotkeyEnabled
    {
        get => this.hotkeyEnabled;
        set => this.SetProperty(ref this.hotkeyEnabled, value);
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

    private void RemoveBrowserUrl(EditableStringRowViewModel row) => this.BrowserUrls.Remove(row);

    private void RemoveTabGroup(EditableStringRowViewModel row) => this.TabGroups.Remove(row);

    private void RemoveBrowserProfile(BrowserProfileRowViewModel row) => this.BrowserProfiles.Remove(row);

    private void RemoveDockerStart(EditableStringRowViewModel row) => this.DockerStart.Remove(row);

    private void RemoveDockerStop(EditableStringRowViewModel row) => this.DockerStop.Remove(row);

    private void RemoveQuickLink(QuickLinkRowViewModel row) => this.QuickLinks.Remove(row);

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

            List<HotkeyConfig> hotkeys = AppHost.Configuration.Hotkeys
                .Where(h => h.ContextId != this.originalContextId)
                .ToList();
            if (!string.IsNullOrWhiteSpace(this.HotkeyAccelerator))
            {
                hotkeys.Add(new HotkeyConfig
                {
                    Id = $"switch-{edited.Id}",
                    ContextId = edited.Id,
                    Accelerator = this.HotkeyAccelerator.Trim(),
                    Enabled = this.HotkeyEnabled
                });
            }

            AppConfiguration updated = AppHost.Configuration with
            {
                Contexts = contexts,
                Hotkeys = hotkeys,
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
            MenuBarLabel = this.MenuBarLabel.Trim(),
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
            Theme = new ThemeConfig { Mode = this.ThemeMode },
            Wallpaper = new WallpaperConfig { Path = this.WallpaperPath.Trim(), AllSpaces = this.WallpaperAllSpaces },
            Focus = new FocusConfig { Enabled = this.FocusEnabled, ModeName = this.FocusModeName.Trim() },
            Media = new MediaConfig { Player = this.MediaPlayer, Playlist = this.MediaPlaylist.Trim(), AutoPlay = this.MediaAutoPlay },
            Docker = new DockerResourceConfig
            {
                Start = this.DockerStart.Where(d => !string.IsNullOrWhiteSpace(d.Value)).Select(d => d.Value.Trim()).ToList(),
                Stop = this.DockerStop.Where(d => !string.IsNullOrWhiteSpace(d.Value)).Select(d => d.Value.Trim()).ToList()
            },
            QuickLinks = this.QuickLinks
                .Where(q => !string.IsNullOrWhiteSpace(q.Url))
                .Select(q => new QuickLinkConfig { Title = q.Title.Trim(), Url = q.Url.Trim(), Icon = string.IsNullOrWhiteSpace(q.Icon) ? "link" : q.Icon.Trim() })
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

    private static string FormatStepTypeName(Core.Automation.AutomationStepType stepType)
    {
        // Insert a space before each interior capital: "CloseApplications" -> "Close Applications".
        return string.Concat(stepType.ToString().Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));
    }
}
