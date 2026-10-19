using System.Collections.ObjectModel;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Profile Setup's browser section: which browser, what it does on a switch - nothing, open pages,
/// or open one of its profiles - and the pages and profiles themselves. What a browser can do here is
/// the <see cref="PlatformCapabilities"/>' to say.
/// </summary>
public sealed class BrowserSettingsViewModel : ViewModelBase
{
    /// <summary>What every browser can do on a switch: nothing, or open pages.</summary>
    private static readonly IReadOnlyList<Choice<BrowserManagementMode>> PageModes =
    [
        new(BrowserManagementMode.None, Strings.Browser_Mode_None),
        new(BrowserManagementMode.Urls, Strings.Browser_Mode_Urls)
    ];

    /// <summary>What a browser whose profiles can be opened can do besides: open one of them - a different signed-in person.</summary>
    private static readonly IReadOnlyList<Choice<BrowserManagementMode>> ProfileModes =
    [
        .. PageModes,
        new(BrowserManagementMode.Profiles, Strings.Browser_Mode_Profiles)
    ];

    private readonly ISystemCatalog catalog;
    private readonly PlatformCapabilities capabilities;

    /// <summary>The browser the system opens links in, which "Default browser" stands for.</summary>
    private readonly DefaultBrowser? systemDefault;

    private BrowserManagementMode browserMode;
    private BrowserKind browserKind;
    private bool avoidDuplicateTabs;

    public BrowserSettingsViewModel(
        ISystemCatalog catalog,
        IShell shell,
        PlatformCapabilities capabilities,
        DefaultBrowser? systemDefault,
        BrowserManagementConfig source)
    {
        this.catalog = catalog;
        this.capabilities = capabilities;
        this.systemDefault = systemDefault;

        this.browserMode = source.Mode;
        this.browserKind = source.Browser;

        // A profiles profile names its browser on each row, and the section's browser was not
        // asked for; it is now the one the rows open in.
        if (this.browserMode == BrowserManagementMode.Profiles &&
            source.Profiles.Count > 0 &&
            this.EffectiveBrowser != source.Profiles[0].Browser)
        {
            this.browserKind = source.Profiles[0].Browser;
        }

        this.BrowserKindChoices = this.BrowserChoices();

        // Only reachable from settings the editor never wrote - profiles with no rows, say.
        if (!this.BrowserModeChoices.Any(choice => choice.Value == this.browserMode))
        {
            this.browserMode = BrowserManagementMode.Urls;
        }

        this.avoidDuplicateTabs = source.AvoidDuplicateTabs;
        this.BrowserUrls = new ObservableCollection<BrowserPageRowViewModel>(
            source.Urls.Concat(source.CloseUrls)
                .Distinct(StringComparer.Ordinal)
                .Select(url => this.UrlRow(url, source.Urls.Contains(url), source.CloseUrls.Contains(url))));
        this.BrowserProfiles = new ObservableCollection<BrowserProfileRowViewModel>(
            source.Profiles.Select(profile => new BrowserProfileRowViewModel(
                this.capabilities.OpensBrowserProfiles(profile.Browser) ? profile.Browser : this.ProfilesBrowser,
                profile.ProfileDirectory,
                profile.Urls,
                catalog.GetBrowserProfiles,
                this.RemoveBrowserProfile)));
        this.UrlTabPicker = new ChoicePickerViewModel(
            this.LoadOpenTabsAsync,
            () => this.BrowserUrls.Select(row => row.Url.Trim()),
            choice => this.BrowserUrls.Add(this.UrlRow(choice.Value)),
            Strings.Picker_OpenTabsEmpty,
            Strings.Picker_OpenTabsUnavailable,
            "IconGlobe",
            Strings.Picker_SearchOpenTabs);

        this.AddBrowserUrlCommand = new RelayCommand(() => this.BrowserUrls.Add(this.UrlRow(string.Empty)));
        this.AddBrowserProfileCommand = new RelayCommand(() => this.BrowserProfiles.Add(
            new BrowserProfileRowViewModel(this.ProfilesBrowser, string.Empty, [], catalog.GetBrowserProfiles, this.RemoveBrowserProfile)));
        this.OpenFocusSettingsCommand = new RelayCommand(() => shell.OpenSystemPage(SystemPage.FocusSettings));
    }

    /// <summary>
    /// What the chosen browser can be asked to do, and nothing it can't: opening one of its profiles
    /// is for the browsers whose profiles can be opened here - Chrome and Brave, on a Mac. Safari's
    /// tab groups and profiles, and either browser's tab groups, can't be opened by another app at
    /// all (docs/browser-control.md).
    /// </summary>
    public IReadOnlyList<Choice<BrowserManagementMode>> BrowserModeChoices =>
        this.CanOpenBrowserProfiles ? ProfileModes : PageModes;

    /// <summary>
    /// The default browser, naming the one it is when known - "Default browser (Google Chrome)" -
    /// then each supported browser that is installed. One the profile already uses but that has
    /// since been removed stays, marked, rather than the profile quietly being changed.
    /// </summary>
    public IReadOnlyList<Choice<BrowserKind>> BrowserKindChoices { get; }

    public BrowserManagementMode BrowserMode
    {
        get => this.browserMode;
        set
        {
            // Only what the chosen browser can do; anything else - which only code can ask for -
            // opens pages, as every browser can.
            if (!this.BrowserModeChoices.Any(choice => choice.Value == value))
            {
                value = BrowserManagementMode.Urls;
            }

            if (this.SetProperty(ref this.browserMode, value))
            {
                this.OnPropertyChanged(nameof(this.SelectedBrowserMode));
                this.OnPropertyChanged(nameof(this.IsUrlsMode));
                this.OnPropertyChanged(nameof(this.IsProfilesMode));
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

    public bool IsUrlsMode => this.BrowserMode == BrowserManagementMode.Urls;

    public bool IsProfilesMode => this.BrowserMode == BrowserManagementMode.Profiles;

    /// <summary>Whether the browser has profiles another app can open here.</summary>
    public bool CanOpenBrowserProfiles => this.capabilities.OpensBrowserProfiles(this.EffectiveBrowser);

    /// <summary>
    /// For a browser whose tab groups and profiles no other app can open - Safari, on a Mac: the
    /// system switches it to one when a Focus with that filter turns on, and this profile can turn
    /// the Focus on.
    /// </summary>
    public bool ShowsFocusFilterTip => this.capabilities.FocusFiltersBrowser(this.EffectiveBrowser);

    /// <summary>Opens the system's Focus settings, where a Focus's browser filter is set.</summary>
    public RelayCommand OpenFocusSettingsCommand { get; }

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
                this.OnPropertyChanged(nameof(this.CanCloseTabs));
                this.OnPropertyChanged(nameof(this.CloseTabsHint));
                this.OnPropertyChanged(nameof(this.CanOpenBrowserProfiles));
                this.OnPropertyChanged(nameof(this.ShowsFocusFilterTip));
                foreach (BrowserPageRowViewModel row in this.BrowserUrls)
                {
                    row.CanClose = this.CanCloseTabs;
                }

                // Profiles are opened in the section's browser; a row lists that browser's own.
                if (this.CanOpenBrowserProfiles)
                {
                    foreach (BrowserProfileRowViewModel row in this.BrowserProfiles)
                    {
                        row.Browser = this.ProfilesBrowser;
                    }
                }

                // A browser that can't open profiles leaves the pages - what every browser can do.
                if (!this.BrowserModeChoices.Any(choice => choice.Value == this.BrowserMode))
                {
                    this.BrowserMode = BrowserManagementMode.Urls;
                }

                this.OnPropertyChanged(nameof(this.BrowserModeChoices));
                this.OnPropertyChanged(nameof(this.SelectedBrowserMode));
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
    /// scripting the browser, which works for Chrome, Brave and Safari - chosen, or the default -
    /// and not for others, such as Firefox. Surfacing that here is the difference between a toggle
    /// that lies and one that explains.
    /// </summary>
    public bool CanAvoidDuplicateTabs => this.EffectiveBrowser != BrowserKind.Default;

    public string AvoidDuplicateTabsHint => this.CanAvoidDuplicateTabs
        ? Strings.Browser_AvoidDuplicates_On
        : this.UnscriptableDefault is { } name
            ? Localized.Format(Strings.Browser_AvoidDuplicates_Unsupported, name)
            : Strings.Browser_AvoidDuplicates_NeedsBrowser;

    /// <summary>Why Close is off, under the list of pages.</summary>
    public string CloseTabsHint => this.UnscriptableDefault is { } name
        ? Localized.Format(Strings.ProfileSetup_CloseTabsUnsupported, name)
        : Strings.ProfileSetup_CloseTabsNeedsBrowser;

    /// <summary>
    /// Whether pages can be closed on leaving: as with duplicates, it takes asking a named browser
    /// for its tabs. The rows' Close stays visible but off, with the reason under the list.
    /// </summary>
    public bool CanCloseTabs => this.EffectiveBrowser != BrowserKind.Default;

    public bool AvoidDuplicateTabs
    {
        get => this.avoidDuplicateTabs;
        set => this.SetProperty(ref this.avoidDuplicateTabs, value);
    }

    public ObservableCollection<BrowserPageRowViewModel> BrowserUrls { get; }

    public ObservableCollection<BrowserProfileRowViewModel> BrowserProfiles { get; }

    /// <summary>"From open tabs" under Startup URLs: the pages open in the chosen browser right now.</summary>
    public ChoicePickerViewModel UrlTabPicker { get; }

    public RelayCommand AddBrowserUrlCommand { get; }

    public RelayCommand AddBrowserProfileCommand { get; }

    /// <summary>The browser a profiles row opens in: the chosen one when it has profiles, else the first that does.</summary>
    private BrowserKind ProfilesBrowser => this.CanOpenBrowserProfiles ? this.EffectiveBrowser : this.capabilities.BrowsersWithProfiles.FirstOrDefault();

    /// <summary>
    /// The browser the profile's pages open in: the chosen one, or for "Default browser" the one
    /// the system opens links in - <see cref="BrowserKind.Default"/> still when that can't be scripted.
    /// </summary>
    private BrowserKind EffectiveBrowser => this.BrowserKind == BrowserKind.Default
        ? this.systemDefault?.Kind ?? BrowserKind.Default
        : this.BrowserKind;

    /// <summary>The default browser's name when it is the one in use and can't be scripted.</summary>
    private string? UnscriptableDefault =>
        this.BrowserKind == BrowserKind.Default && this.systemDefault is { Kind: BrowserKind.Default, Name.Length: > 0 } known
            ? known.Name
            : null;

    /// <summary>The section as the profile keeps it.</summary>
    public BrowserManagementConfig ToConfig() => new()
    {
        Mode = this.BrowserMode,
        Browser = this.BrowserKind,
        Urls = this.BrowserUrls.Where(u => u.OpenOnEnter && !string.IsNullOrWhiteSpace(u.Url)).Select(u => u.Url.Trim()).ToList(),

        // Kept only where it can work, so switching to the default browser can't save a profile
        // that fails its check.
        CloseUrls = this.CanCloseTabs
            ? this.BrowserUrls.Where(u => u.CloseOnLeave && !string.IsNullOrWhiteSpace(u.Url)).Select(u => u.Url.Trim()).ToList()
            : [],
        Profiles = this.BrowserProfiles
            .Where(p => !string.IsNullOrWhiteSpace(p.ProfileDirectory))
            .Select(p => new BrowserProfileConfig { Browser = p.Browser, ProfileDirectory = p.ProfileDirectory.Trim(), Urls = p.ParseUrls() })
            .ToList(),
        AvoidDuplicateTabs = this.AvoidDuplicateTabs
    };

    /// <summary>The tabs open in the chosen browser now, to pick a page - or a quick link - from.</summary>
    public async Task<IReadOnlyList<PickerChoice>?> LoadOpenTabsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OpenTab> tabs = await this.catalog.GetOpenTabsAsync(this.BrowserKind, cancellationToken).ConfigureAwait(true);
        return tabs.Select(tab => new PickerChoice(tab.Url, tab.Title, HostOf(tab.Url))).ToList();
    }

    /// <summary>The site of <paramref name="url"/>, or empty for something that isn't an address.</summary>
    internal static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host : string.Empty;

    private List<Choice<BrowserKind>> BrowserChoices()
    {
        List<Choice<BrowserKind>> choices =
        [
            new(BrowserKind.Default, this.systemDefault is { Name.Length: > 0 } known
                ? Localized.Format(Strings.Browser_DefaultIs, known.Name)
                : Strings.Browser_Default)
        ];

        // The browsers this computer can have, and one the profile names that it can't - from
        // another computer's settings - marked as not installed rather than changed.
        IEnumerable<BrowserKind> offered = this.capabilities.HasBrowser(this.browserKind)
            ? this.capabilities.Browsers
            : [.. this.capabilities.Browsers, this.browserKind];
        foreach (BrowserKind kind in offered)
        {
            string name = KnownBrowsers.Name(kind);
            if (this.capabilities.HasBrowser(kind) && this.catalog.IsBrowserInstalled(kind))
            {
                choices.Add(new(kind, name));
            }
            else if (kind == this.browserKind)
            {
                choices.Add(new(kind, Localized.Format(Strings.Browser_NotInstalled, name)));
            }
        }

        return choices;
    }

    /// <summary>A page added now opens, as pages always did; closing it is opted into.</summary>
    private BrowserPageRowViewModel UrlRow(string url, bool open = true, bool close = false) =>
        new(url, open, close, this.CanCloseTabs, this.RemoveBrowserUrl);

    private void RemoveBrowserUrl(BrowserPageRowViewModel row)
    {
        this.BrowserUrls.Remove(row);
        this.UrlTabPicker.Refresh();
    }

    private void RemoveBrowserProfile(BrowserProfileRowViewModel row) => this.BrowserProfiles.Remove(row);
}
