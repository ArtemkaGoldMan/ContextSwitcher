using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

[Collection(AppHostTestCollection.Name)]
public sealed class ProfileSetupViewModelTests
{
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
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), existing: null);

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
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);
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
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);

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
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context)
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

        viewModel.SelectedAccent = viewModel.AccentChoices.Single(c => c.Name == "Teal");

        Assert.Equal("#0FA3B1", viewModel.AccentColor);
        Assert.Equal("Teal", viewModel.SelectedAccent.Name);
        Assert.False(viewModel.IsCustomAccent);
    }

    /// <summary>
    /// The custom picker works in Avalonia colors; config keeps the plain #RRGGBB string, and the
    /// picker's alpha must not leak into it.
    /// </summary>
    [Fact]
    public void ACustomColorIsStoredAsPlainHexAndShownAsCustom()
    {
        ProfileSetupViewModel viewModel = NewProfile();

        viewModel.AccentColorValue = Avalonia.Media.Color.FromArgb(0x80, 0x12, 0xAB, 0xEF);

        Assert.Equal("#12ABEF", viewModel.AccentColor);
        Assert.True(viewModel.IsCustomAccent);
        Assert.True(viewModel.SelectedAccent.IsCustom);
    }

    /// <summary>
    /// Choosing "Custom" reveals the picker without changing the color, and the picker stays put
    /// while it is dragged - even onto a preset's exact color - until a preset is chosen again.
    /// </summary>
    [Fact]
    public void CustomStaysChosenUntilAPresetIsPicked()
    {
        ProfileSetupViewModel viewModel = NewProfile();
        string before = viewModel.AccentColor;

        viewModel.SelectedAccent = viewModel.AccentChoices.Single(c => c.IsCustom);
        Assert.True(viewModel.IsCustomAccent);
        Assert.Equal(before, viewModel.AccentColor);

        viewModel.AccentColor = "#20A67A";
        Assert.True(viewModel.IsCustomAccent);
        Assert.True(viewModel.SelectedAccent.IsCustom);

        viewModel.SelectedAccent = viewModel.AccentChoices.Single(c => c.Name == "Violet");
        Assert.False(viewModel.IsCustomAccent);
        Assert.Equal("#7B61FF", viewModel.AccentColor);
    }

    /// <summary>Hand-edited configs may use lowercase hex; the matching preset is still the one shown.</summary>
    [Fact]
    public void AnExistingLowercaseAccentStillMatchesItsPreset()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work", AccentColor = "#2f6fed" };
        AppHost.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));

        ProfileSetupViewModel viewModel = new(CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);

        Assert.Equal("Blue", viewModel.SelectedAccent.Name);
        Assert.False(viewModel.IsCustomAccent);
    }

    [Fact]
    public void PickingAnIconFromTheDropdownStoresItsName()
    {
        ProfileSetupViewModel viewModel = NewProfile();

        viewModel.SelectedIcon = viewModel.IconChoices.Single(c => c.Name == "coffee");

        Assert.Equal("coffee", viewModel.Icon);
        Assert.Equal("coffee", viewModel.SelectedIcon.Name);
    }

    /// <summary>
    /// An icon name this build does not know is kept, not silently rewritten, until the user picks
    /// something else; meanwhile the dropdown shows the circle it is drawn as.
    /// </summary>
    [Fact]
    public void AnUnknownStoredIconIsKeptAndShownAsTheCircle()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work", Icon = "rocket" };
        AppHost.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));

        ProfileSetupViewModel viewModel = new(CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);

        Assert.Equal("rocket", viewModel.Icon);
        Assert.Equal("circle", viewModel.SelectedIcon.Name);
    }

    private static ProfileSetupViewModel NewProfile()
    {
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }] },
            new ConfigurationValidationResult([]));
        return new ProfileSetupViewModel(CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), existing: null);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequestedWithoutSaving()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), existing: null) { DisplayName = "Should not persist" };
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
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Slack", null));
        installed.Apps.Add(new InstalledApp("Discord", null));
        installed.Apps.Add(new InstalledApp("Docker", null));

        ProfileSetupViewModel viewModel = new(store, installed, new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);

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
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Discord", null));

        ProfileSetupViewModel viewModel = new(store, installed, new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);
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
        AppHost.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);

        Assert.Equal(
            [("https://github.com/", true, false), ("https://linear.app/", true, true), ("https://youtube.com/", false, true)],
            viewModel.BrowserUrls.Select(row => (row.Url, row.OpenOnEnter, row.CloseOnLeave)));

        viewModel.BrowserUrls[0].CloseOnLeave = true;
        viewModel.BrowserUrls[2].CloseOnLeave = false;
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
        AppHost.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);
        Assert.True(viewModel.BrowserUrls[0].CanClose);

        viewModel.BrowserKind = BrowserKind.Default;
        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.CanCloseTabs);
        Assert.False(viewModel.BrowserUrls[0].CanClose);
        Assert.False(viewModel.HasErrorMessage);
        BrowserManagementConfig saved = Assert.Single(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.Contexts).BrowserManagement;
        Assert.Equal(["https://github.com/"], saved.Urls);
        Assert.Empty(saved.CloseUrls);
    }

    [Fact]
    public void APageAddedNowOpensAndDoesntClose()
    {
        ProfileSetupViewModel viewModel = NewProfile();

        viewModel.AddBrowserUrlCommand.Execute(null);

        BrowserPageRowViewModel row = Assert.Single(viewModel.BrowserUrls);
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
        AppHost.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(
            store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context,
            new FakeDefaultBrowser(BrowserKind.Chrome, "Google Chrome"));

        Assert.Equal("Default browser (Google Chrome)", viewModel.SelectedBrowserKind.Label);
        Assert.True(viewModel.CanAvoidDuplicateTabs);
        Assert.True(viewModel.CanCloseTabs);

        viewModel.BrowserUrls[0].CloseOnLeave = true;
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
            CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), existing: null,
            new FakeDefaultBrowser(BrowserKind.Default, "Firefox"));
        viewModel.BrowserKind = BrowserKind.Default;

        Assert.False(viewModel.CanCloseTabs);
        Assert.Equal("Closing tabs needs Chrome, Brave or Safari - Firefox can't be checked for open tabs.", viewModel.CloseTabsHint);
        Assert.Equal("Needs Chrome, Brave or Safari - Firefox can't be checked for open tabs.", viewModel.AvoidDuplicateTabsHint);
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
        AppHost.UpdateConfiguration(new AppConfiguration { ActiveContextId = "work", Contexts = [context] }, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);
        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), context);

        Assert.Equal(
            [("Visual Studio Code", true, false, true), ("Notion", false, false, true)],
            viewModel.Apps.Select(row => (row.Name, row.LaunchOnEnter, row.CloseOnLeave, row.HideOnLeave)));

        viewModel.Apps[1].CloseOnLeave = true;
        viewModel.SaveCommand.Execute(null);

        ContextDefinition saved = Assert.Single(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.Contexts);
        Assert.Equal(["Visual Studio Code"], saved.HideApps);
        Assert.Equal(["Notion"], saved.CloseApps);
    }

    private static ConfigurationStore CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths)
    {
        jsonStore = new InMemoryJsonStore();
        configPaths = new ConfigPaths("/tmp/context-switcher-tests");
        return new ConfigurationStore(jsonStore, configPaths, new ConfigurationValidator());
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

        viewModel.BrowserKind = BrowserKind.Default;

        Assert.False(viewModel.CanAvoidDuplicateTabs);
        Assert.Contains("default browser", viewModel.AvoidDuplicateTabsHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvoidDuplicateTabsBecomesAvailableWhenAConcreteBrowserIsChosen()
    {
        ProfileSetupViewModel viewModel = NewProfileViewModel();
        viewModel.BrowserKind = BrowserKind.Default;

        List<string> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        viewModel.BrowserKind = BrowserKind.Chrome;

        Assert.True(viewModel.CanAvoidDuplicateTabs);
        Assert.Contains(nameof(ProfileSetupViewModel.CanAvoidDuplicateTabs), changed);
        Assert.Contains(nameof(ProfileSetupViewModel.AvoidDuplicateTabsHint), changed);
    }


    private static ProfileSetupViewModel NewProfileViewModel()
    {
        AppHost.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "work",
                Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
            },
            new ConfigurationValidationResult([]));

        return new ProfileSetupViewModel(CreateStore(out _, out _), new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), existing: null);
    }

}
