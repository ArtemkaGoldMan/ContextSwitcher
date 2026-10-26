using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class ProfileSetupViewModelTests
{
    private readonly AppState appState = new();

    /// <summary>
    /// The id field is gone from the editor, so a new profile's id follows its name - which keeps
    /// the id the CLI and Shortcuts use recognisable - and steps past ids already taken.
    /// </summary>
    [Fact]
    public void NewProfileTakesItsIdFromItsNameAndAvoidsTakenOnes()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "deep-work",
            Contexts = [new ContextDefinition { Id = "deep-work", DisplayName = "Deep Work" }]
        };
        this.appState.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), existing: null);

        Assert.True(viewModel.IsNew);
        Assert.Equal("new-profile", viewModel.Id);
        Assert.Equal("New Profile", viewModel.HeaderText);

        viewModel.DisplayName = "Deep Work";
        Assert.Equal("deep-work-2", viewModel.Id);

        viewModel.DisplayName = "  Side Project #2!  ";
        Assert.Equal("side-project-2", viewModel.Id);

        viewModel.DisplayName = "Работа";
        Assert.Equal("profile", viewModel.Id);
    }

    /// <summary>
    /// Renaming an existing profile must not move its id: analytics, state.json, the CLI and any
    /// Shortcut the user built all refer to it.
    /// </summary>
    [Fact]
    public void RenamingAnExistingProfileKeepsItsId()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);
        viewModel.DisplayName = "Office";

        Assert.Equal("work", viewModel.Id);
    }

    [Fact]
    public void ExistingProfileMergesLaunchAndCloseAppsIntoDualToggleRows()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            LaunchApps = ["Slack", "Both App"],
            CloseApps = ["Spotify", "Both App"]
        };
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        Assert.Equal("work", viewModel.Id);
        Assert.Equal(3, viewModel.Apps.Count);
        AppRowViewModel both = Assert.Single(viewModel.Apps, a => a.Name == "Both App");
        Assert.True(both.LaunchOnEnter);
        Assert.True(both.CloseOnLeave);
        AppRowViewModel slack = Assert.Single(viewModel.Apps, a => a.Name == "Slack");
        Assert.True(slack.LaunchOnEnter);
        Assert.False(slack.CloseOnLeave);
    }

    [Fact]
    public void SaveCommandRoundTripsIdentityAndAppsToConfiguration()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);

        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context)
        {
            DisplayName = "Work Renamed"
        };
        viewModel.Apps.Add(new AppRowViewModel("Slack", launchOnEnter: true, closeOnLeave: false, _ => { }));

        bool saved = false;
        viewModel.Saved += (_, _) => saved = true;
        viewModel.SaveCommand.Execute(null);

        Assert.True(saved);
        Assert.False(viewModel.HasErrorMessage);
        AppConfiguration? persisted = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        ContextDefinition updated = Assert.Single(persisted!.Contexts);
        Assert.Equal("Work Renamed", updated.DisplayName);
        Assert.Equal(["Slack"], updated.LaunchApps);
        Assert.Empty(updated.CloseApps);
    }

    [Fact]
    public void PickingAPresetFromTheDropdownSetsTheAccent()
    {
        ProfileSetupViewModel viewModel = NewProfile();

        viewModel.Look.SelectedAccent = viewModel.Look.AccentChoices.Single(c => c.Name == "Teal");

        Assert.Equal("#0FA3B1", viewModel.Look.AccentColor);
        Assert.Equal("Teal", viewModel.Look.SelectedAccent.Name);
        Assert.False(viewModel.Look.IsCustomAccent);
    }

    /// <summary>
    /// The custom picker works in Avalonia colors; config keeps the plain #RRGGBB string, and the
    /// picker's alpha must not leak into it.
    /// </summary>
    [Fact]
    public void ACustomColorIsStoredAsPlainHexAndShownAsCustom()
    {
        ProfileSetupViewModel viewModel = NewProfile();

        viewModel.Look.AccentColorValue = Avalonia.Media.Color.FromArgb(0x80, 0x12, 0xAB, 0xEF);

        Assert.Equal("#12ABEF", viewModel.Look.AccentColor);
        Assert.True(viewModel.Look.IsCustomAccent);
        Assert.True(viewModel.Look.SelectedAccent.IsCustom);
    }

    /// <summary>
    /// Choosing "Custom" reveals the picker without changing the color, and the picker stays put
    /// while it is dragged - even onto a preset's exact color - until a preset is chosen again.
    /// </summary>
    [Fact]
    public void CustomStaysChosenUntilAPresetIsPicked()
    {
        ProfileSetupViewModel viewModel = NewProfile();
        string before = viewModel.Look.AccentColor;

        viewModel.Look.SelectedAccent = viewModel.Look.AccentChoices.Single(c => c.IsCustom);
        Assert.True(viewModel.Look.IsCustomAccent);
        Assert.Equal(before, viewModel.Look.AccentColor);

        viewModel.Look.AccentColor = "#20A67A";
        Assert.True(viewModel.Look.IsCustomAccent);
        Assert.True(viewModel.Look.SelectedAccent.IsCustom);

        viewModel.Look.SelectedAccent = viewModel.Look.AccentChoices.Single(c => c.Name == "Violet");
        Assert.False(viewModel.Look.IsCustomAccent);
        Assert.Equal("#7B61FF", viewModel.Look.AccentColor);
    }

    /// <summary>Hand-edited configs may use lowercase hex; the matching preset is still the one shown.</summary>
    [Fact]
    public void AnExistingLowercaseAccentStillMatchesItsPreset()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work", AccentColor = "#2f6fed" };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));

        ProfileSetupViewModel viewModel = new(this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        Assert.Equal("Blue", viewModel.Look.SelectedAccent.Name);
        Assert.False(viewModel.Look.IsCustomAccent);
    }

    [Fact]
    public void PickingAnIconFromTheDropdownStoresItsName()
    {
        ProfileSetupViewModel viewModel = NewProfile();

        viewModel.Look.SelectedIcon = viewModel.Look.IconChoices.Single(c => c.Name == "coffee");

        Assert.Equal("coffee", viewModel.Look.Icon);
        Assert.Equal("coffee", viewModel.Look.SelectedIcon.Name);
    }

    /// <summary>
    /// An icon name this build does not know is kept, not silently rewritten, until the user picks
    /// something else; meanwhile the dropdown shows the circle it is drawn as.
    /// </summary>
    [Fact]
    public void AnUnknownStoredIconIsKeptAndShownAsTheCircle()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work", Icon = "rocket" };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));

        ProfileSetupViewModel viewModel = new(this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        Assert.Equal("rocket", viewModel.Look.Icon);
        Assert.Equal("circle", viewModel.Look.SelectedIcon.Name);
    }

    /// <summary>
    /// A computer without Focus, Apple Music or Safari offers none of them. A profile that names one
    /// anyway - from a Mac's settings - keeps it, marked, rather than being changed.
    /// </summary>
    [Fact]
    public void OnlyWhatTheComputerHasIsOffered()
    {
        ConfigurationStore store = CreateStore(out _, out _);
        PlatformCapabilities lesser = Platform.PlatformCapabilitiesTests.Lesser;

        ProfileSetupViewModel blank = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), lesser, new FakeUnknownDefaultBrowser(), existing: null);
        ProfileSetupViewModel fromAMac = new(
            this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), lesser, new FakeUnknownDefaultBrowser(),
            new ContextDefinition
            {
                Id = "work",
                DisplayName = "Work",
                Media = new MediaConfig { Player = MediaPlayerKind.AppleMusic, Playlist = "Deep Focus" },
                BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Browser = BrowserKind.Safari, Urls = ["https://linear.app/"] }
            });

        Assert.False(blank.ShowsFocus);
        Assert.Equal([MediaPlayerKind.None, MediaPlayerKind.Spotify], blank.Media.Players.Select(choice => choice.Value));
        Assert.Equal([BrowserKind.Default, BrowserKind.Chrome], blank.Browser.BrowserKindChoices.Select(choice => choice.Value));
        Assert.Equal(MediaPlayerKind.AppleMusic, fromAMac.Media.SelectedPlayer.Value);
        Assert.Equal("Safari (not installed)", fromAMac.Browser.SelectedBrowserKind.Label);
        Assert.False(fromAMac.Browser.ShowsFocusFilterTip);
    }

    /// <summary>A computer with apps alone shows no browser, Focus, music or Docker section; a Mac shows them all.</summary>
    [Fact]
    public void OnlyTheSectionsTheComputerCanDoShow()
    {
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel appsOnly = new(
            this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(),
            Platform.PlatformCapabilitiesTests.AppsOnly, new FakeUnknownDefaultBrowser(), existing: null);
        ProfileSetupViewModel mac = new(
            this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(),
            MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), existing: null);

        Assert.False(appsOnly.ShowsBrowser || appsOnly.ShowsFocus || appsOnly.ShowsMusic || appsOnly.ShowsFocusOrMusic || appsOnly.ShowsDocker);
        Assert.True(mac.ShowsBrowser && mac.ShowsFocus && mac.ShowsMusic && mac.ShowsFocusOrMusic && mac.ShowsDocker);
    }

    private ProfileSetupViewModel NewProfile()
    {
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }] },
            new ConfigurationValidationResult([]));
        return new ProfileSetupViewModel(this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), existing: null);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequestedWithoutSaving()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);

        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), existing: null) { DisplayName = "Should not persist" };
        bool cancelled = false;
        viewModel.CancelRequested += (_, _) => cancelled = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(cancelled);
        Assert.Null(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath));
    }

    [Fact]
    public void AppPickerExcludesAppsAlreadyAddedAndFiltersBySearch()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work", LaunchApps = ["Slack"] };
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Slack", null));
        installed.Apps.Add(new InstalledApp("Discord", null));
        installed.Apps.Add(new InstalledApp("Docker", null));

        ProfileSetupViewModel viewModel = new(this.appState, store, installed, new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        // Slack is already on the profile, so the picker must not offer it again.
        Assert.DoesNotContain(viewModel.AppPicker.FilteredApps, app => app.Name == "Slack");
        Assert.Contains(viewModel.AppPicker.FilteredApps, app => app.Name == "Discord");

        viewModel.AppPicker.SearchText = "doc";
        InstalledAppViewModel match = Assert.Single(viewModel.AppPicker.FilteredApps);
        Assert.Equal("Docker", match.Name);
    }

    [Fact]
    public void PickingAnAppAddsItWithBothTogglesOnAndRemovesItFromThePicker()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Discord", null));

        ProfileSetupViewModel viewModel = new(this.appState, store, installed, new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);
        InstalledAppViewModel discord = Assert.Single(viewModel.AppPicker.FilteredApps);

        discord.PickCommand.Execute(null);

        AppRowViewModel added = Assert.Single(viewModel.Apps);
        Assert.Equal("Discord", added.Name);
        Assert.True(added.LaunchOnEnter);
        Assert.True(added.CloseOnLeave);
        Assert.Empty(viewModel.AppPicker.FilteredApps);
    }


    /// <summary>
    /// Pages show the way apps do - one row, Open and Close - over the two lists in the file, and
    /// a page with neither ticked is dropped.
    /// </summary>
    [Fact]
    public void PagesAreRowsWithOpenAndCloseOverTheTwoLists()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                Urls = ["https://github.com/", "https://linear.app/"],
                CloseUrls = ["https://linear.app/", "https://youtube.com/"]
            }
        };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        Assert.Equal(
            [("https://github.com/", true, false), ("https://linear.app/", true, true), ("https://youtube.com/", false, true)],
            viewModel.Browser.BrowserUrls.Select(row => (row.Url, row.OpenOnEnter, row.CloseOnLeave)));

        viewModel.Browser.BrowserUrls[0].CloseOnLeave = true;
        viewModel.Browser.BrowserUrls[2].CloseOnLeave = false;
        viewModel.SaveCommand.Execute(null);

        BrowserManagementConfig saved = Assert.Single(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.Contexts).BrowserManagement;
        Assert.Equal(["https://github.com/", "https://linear.app/"], saved.Urls);
        Assert.Equal(["https://github.com/", "https://linear.app/"], saved.CloseUrls);
    }

    /// <summary>
    /// The default browser can't be asked for its tabs: Close goes off on every row, with the
    /// reason, and isn't saved - so the profile still passes its check.
    /// </summary>
    [Fact]
    public void TheDefaultBrowserCantCloseTabs()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Urls,
                Browser = BrowserKind.Chrome,
                Urls = ["https://github.com/"],
                CloseUrls = ["https://github.com/"]
            }
        };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);
        Assert.True(viewModel.Browser.BrowserUrls[0].CanClose);

        viewModel.Browser.BrowserKind = BrowserKind.Default;
        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Browser.CanCloseTabs);
        Assert.False(viewModel.Browser.BrowserUrls[0].CanClose);
        Assert.False(viewModel.HasErrorMessage);
        BrowserManagementConfig saved = Assert.Single(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.Contexts).BrowserManagement;
        Assert.Equal(["https://github.com/"], saved.Urls);
        Assert.Empty(saved.CloseUrls);
    }

    [Fact]
    public void APageAddedNowOpensAndDoesntClose()
    {
        ProfileSetupViewModel viewModel = NewProfile();

        viewModel.Browser.AddBrowserUrlCommand.Execute(null);

        BrowserPageRowViewModel row = Assert.Single(viewModel.Browser.BrowserUrls);
        Assert.True(row.OpenOnEnter);
        Assert.False(row.CloseOnLeave);
    }
    /// <summary>
    /// "Default browser" says which browser it is, and when that is Chrome, Brave or Safari it can
    /// close tabs and avoid duplicates like a named one - and saves that way.
    /// </summary>
    [Fact]
    public void TheDefaultBrowserWorksLikeTheBrowserItIs()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Browser = BrowserKind.Default, Urls = ["https://github.com/"] }
        };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(
            this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities,
            new FakeDefaultBrowser(BrowserKind.Chrome, "Google Chrome"), context);

        Assert.Equal("Default browser (Google Chrome)", viewModel.Browser.SelectedBrowserKind.Label);
        Assert.True(viewModel.Browser.CanAvoidDuplicateTabs);
        Assert.True(viewModel.Browser.CanCloseTabs);

        viewModel.Browser.BrowserUrls[0].CloseOnLeave = true;
        viewModel.SaveCommand.Execute(null);

        BrowserManagementConfig saved = Assert.Single(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.Contexts).BrowserManagement;
        Assert.Equal(BrowserKind.Default, saved.Browser);
        Assert.Equal(["https://github.com/"], saved.CloseUrls);
    }

    /// <summary>A default browser that can't be scripted is named in the reason, rather than left unexplained.</summary>
    [Fact]
    public void AnUnscriptableDefaultBrowserIsNamedInTheReason()
    {
        ProfileSetupViewModel viewModel = new(
            this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities,
            new FakeDefaultBrowser(BrowserKind.Default, "Firefox"), existing: null);
        viewModel.Browser.BrowserKind = BrowserKind.Default;

        Assert.False(viewModel.Browser.CanCloseTabs);
        Assert.Equal("Closing tabs needs Chrome, Brave or Safari - Firefox can't be checked for open tabs.", viewModel.Browser.CloseTabsHint);
        Assert.Equal("Needs Chrome, Brave or Safari - Firefox can't be checked for open tabs.", viewModel.Browser.AvoidDuplicateTabsHint);
    }

    /// <summary>
    /// Opening one of a browser's profiles is offered only for Chrome and Brave - the default
    /// browser included, when it is one of them. Safari gets pages, and the Focus filter tip.
    /// </summary>
    [Theory]
    [InlineData(BrowserKind.Chrome, BrowserKind.Default, true, false)]
    [InlineData(BrowserKind.Brave, BrowserKind.Default, true, false)]
    [InlineData(BrowserKind.Safari, BrowserKind.Default, false, true)]
    [InlineData(BrowserKind.Default, BrowserKind.Chrome, true, false)]
    [InlineData(BrowserKind.Default, BrowserKind.Safari, false, true)]
    [InlineData(BrowserKind.Default, BrowserKind.Default, false, false)]
    public void EachBrowserOffersWhatItCanDo(BrowserKind chosen, BrowserKind systemDefault, bool offersProfiles, bool showsFocusTip)
    {
        ProfileSetupViewModel viewModel = new(
            this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities,
            new FakeDefaultBrowser(systemDefault, systemDefault == BrowserKind.Default ? "Firefox" : systemDefault.ToString()), existing: null);
        viewModel.Browser.BrowserKind = chosen;

        BrowserManagementMode[] expected = offersProfiles
            ? [BrowserManagementMode.None, BrowserManagementMode.Urls, BrowserManagementMode.Profiles]
            : [BrowserManagementMode.None, BrowserManagementMode.Urls];
        Assert.Equal(expected, viewModel.Browser.BrowserModeChoices.Select(choice => choice.Value));
        Assert.DoesNotContain(viewModel.Browser.BrowserModeChoices, choice => choice.Value == BrowserManagementMode.Groups);
        Assert.Equal(showsFocusTip, viewModel.Browser.ShowsFocusFilterTip);
    }

    /// <summary>
    /// Choosing a browser that can't open profiles while profiles are chosen falls back to pages,
    /// which every browser opens - rather than keeping a choice the list no longer shows.
    /// </summary>
    [Fact]
    public void ABrowserWithoutProfilesFallsBackToPages()
    {
        ProfileSetupViewModel viewModel = NewProfileViewModel();
        viewModel.Browser.BrowserKind = BrowserKind.Chrome;
        viewModel.Browser.BrowserMode = BrowserManagementMode.Profiles;

        viewModel.Browser.BrowserKind = BrowserKind.Safari;

        Assert.Equal(BrowserManagementMode.Urls, viewModel.Browser.BrowserMode);
        Assert.Equal(BrowserManagementMode.Urls, viewModel.Browser.SelectedBrowserMode.Value);

        // Only code can ask for what the list doesn't offer; it gets pages too.
        viewModel.Browser.BrowserMode = BrowserManagementMode.Groups;
        Assert.Equal(BrowserManagementMode.Urls, viewModel.Browser.BrowserMode);
    }

    /// <summary>
    /// The browser is chosen once, for the section: each profile row lists that browser's own
    /// profiles and is saved as that browser. A profile saved before - each row naming its browser,
    /// the section's left at Default - opens with the rows' browser chosen.
    /// </summary>
    [Fact]
    public void ProfileRowsFollowTheChosenBrowser()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig
            {
                Mode = BrowserManagementMode.Profiles,
                Browser = BrowserKind.Default,
                Profiles = [new BrowserProfileConfig { Browser = BrowserKind.Brave, ProfileDirectory = "Default" }]
            }
        };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        FakeSystemCatalog catalog = new();
        catalog.BrowserProfiles[BrowserKind.Chrome] = [new("Default", "Artem"), new("Profile 2", "Client")];
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), catalog, new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        Assert.Equal(BrowserKind.Brave, viewModel.Browser.BrowserKind);
        Assert.Equal(BrowserManagementMode.Profiles, viewModel.Browser.BrowserMode);

        viewModel.Browser.BrowserKind = BrowserKind.Chrome;
        viewModel.Browser.AddBrowserProfileCommand.Execute(null);

        Assert.All(viewModel.Browser.BrowserProfiles, row => Assert.Equal(BrowserKind.Chrome, row.Browser));
        Assert.Equal(["Default", "Profile 2"], viewModel.Browser.BrowserProfiles[0].ProfileChoices.Select(choice => choice.Value));

        viewModel.SaveCommand.Execute(null);
        BrowserManagementConfig saved = Assert.Single(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.Contexts).BrowserManagement;
        Assert.Equal(BrowserKind.Chrome, saved.Browser);
        Assert.All(saved.Profiles, profile => Assert.Equal(BrowserKind.Chrome, profile.Browser));
    }

    /// <summary>
    /// Only browsers that are on this Mac can be chosen - and the default browser, which always is.
    /// </summary>
    [Fact]
    public void OnlyInstalledBrowsersAreOffered()
    {
        FakeSystemCatalog catalog = new();
        catalog.InstalledBrowsers.Remove(BrowserKind.Brave);
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }] }, new ConfigurationValidationResult([]));

        ProfileSetupViewModel viewModel = new(
            this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), catalog, new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities,
            new FakeDefaultBrowser(BrowserKind.Safari, "Safari"), existing: null);

        Assert.Equal(["Default browser (Safari)", "Safari", "Google Chrome"], viewModel.Browser.BrowserKindChoices.Select(choice => choice.Label));
    }

    /// <summary>
    /// A profile set to a browser since removed keeps it, marked - quietly switching it to another
    /// browser would change what the profile does - while every other choice is one that's there.
    /// </summary>
    [Fact]
    public void ABrowserTheProfileUsesButIsGoneIsKeptAndMarked()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Browser = BrowserKind.Brave, Urls = ["https://github.com/"] }
        };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        FakeSystemCatalog catalog = new();
        catalog.InstalledBrowsers.Remove(BrowserKind.Brave);
        catalog.InstalledBrowsers.Remove(BrowserKind.Chrome);

        ProfileSetupViewModel viewModel = new(
            this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), catalog, new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        Assert.Equal(["Default browser", "Safari", "Brave (not installed)"], viewModel.Browser.BrowserKindChoices.Select(choice => choice.Label));
        Assert.Equal("Brave (not installed)", viewModel.Browser.SelectedBrowserKind.Label);
    }

    /// <summary>For Safari, the page opens System Settings' Focus pane, where its Safari filter is set.</summary>
    [Fact]
    public void TheSafariTipOpensFocusSettings()
    {
        FakeShell shell = new();
        ProfileSetupViewModel viewModel = new(
            this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), shell, new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), existing: null);
        viewModel.Browser.BrowserKind = BrowserKind.Safari;

        viewModel.Browser.OpenFocusSettingsCommand.Execute(null);

        Assert.Equal([SystemPage.FocusSettings], shell.Pages);
    }

    /// <summary>An app is quit or hidden on leaving, never both: ticking one unticks the other.</summary>
    [Fact]
    public void CloseAndHideTakeTurns()
    {
        AppRowViewModel row = new("Slack", launchOnEnter: true, closeOnLeave: true, _ => { });

        row.HideOnLeave = true;
        Assert.Equal((false, true), (row.CloseOnLeave, row.HideOnLeave));

        row.CloseOnLeave = true;
        Assert.Equal((true, false), (row.CloseOnLeave, row.HideOnLeave));
    }

    [Fact]
    public void HiddenAppsAreReadAndSavedWithTheirRows()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            LaunchApps = ["Visual Studio Code"],
            HideApps = ["Visual Studio Code", "Notion"]
        };
        this.appState.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), context);

        Assert.Equal(
            [("Visual Studio Code", true, false, true), ("Notion", false, false, true)],
            viewModel.Apps.Select(row => (row.Name, row.LaunchOnEnter, row.CloseOnLeave, row.HideOnLeave)));

        viewModel.Apps[1].CloseOnLeave = true;
        viewModel.SaveCommand.Execute(null);

        ContextDefinition saved = Assert.Single(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.Contexts);
        Assert.Equal(["Visual Studio Code"], saved.HideApps);
        Assert.Equal(["Notion"], saved.CloseApps);
    }

    private ConfigurationStore CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths)
    {
        jsonStore = new InMemoryJsonStore();
        configPaths = new ConfigPaths("/tmp/context-switcher-tests");
        return new ConfigurationStore(jsonStore, configPaths, new ConfigurationValidator(), this.appState, new TestLogger());
    }

    /// <summary>
    /// The two shipping defaults are <c>browser: Default</c> and <c>avoidDuplicateTabs: true</c>,
    /// which combine into a toggle that is on and inert: the default browser cannot be scripted to
    /// find an already-open tab, so a second tab opens on every switch.
    /// </summary>
    [Fact]
    public void AvoidDuplicateTabsIsUnavailableForTheDefaultBrowser()
    {
        ProfileSetupViewModel viewModel = NewProfileViewModel();

        viewModel.Browser.BrowserKind = BrowserKind.Default;

        Assert.False(viewModel.Browser.CanAvoidDuplicateTabs);
        Assert.Contains("default browser", viewModel.Browser.AvoidDuplicateTabsHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvoidDuplicateTabsBecomesAvailableWhenAConcreteBrowserIsChosen()
    {
        ProfileSetupViewModel viewModel = NewProfileViewModel();
        viewModel.Browser.BrowserKind = BrowserKind.Default;

        List<string> changed = [];
        viewModel.Browser.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        viewModel.Browser.BrowserKind = BrowserKind.Chrome;

        Assert.True(viewModel.Browser.CanAvoidDuplicateTabs);
        Assert.Contains(nameof(BrowserSettingsViewModel.CanAvoidDuplicateTabs), changed);
        Assert.Contains(nameof(BrowserSettingsViewModel.AvoidDuplicateTabsHint), changed);
    }


    private ProfileSetupViewModel NewProfileViewModel()
    {
        this.appState.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "work",
                Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
            },
            new ConfigurationValidationResult([]));

        return new ProfileSetupViewModel(this.appState, CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeShell(), new FakeFocusShortcutInstaller(), MacPlatform.Capabilities, new FakeUnknownDefaultBrowser(), existing: null);
    }

}
