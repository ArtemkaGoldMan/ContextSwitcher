using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

/// <summary>
/// The fields Profile Setup used to make you type - Focus mode, playlist, browser profile folder,
/// Docker containers, URLs - and the lists that replaced them.
/// </summary>
[Collection(AppHostTestCollection.Name)]
public sealed class EnvironmentChoicesTests
{
    private const string Prefix = FocusSettingsViewModel.ShortcutPrefix;

    [Fact]
    public void FocusListsModesWithAShortcutFirstThenTheBuiltInOnes()
    {
        FakeSystemCatalog catalog = new();
        catalog.ShortcutNames.AddRange([$"{Prefix}Writing", "Play/Pause", $"{Prefix}Work", $"{Prefix}Off", FocusModes.OffShortcutName("Work")]);

        FocusSettingsViewModel focus = new(catalog, new FakeProcessRunner(), new FakeFocusShortcutInstaller(), new FocusConfig());

        Assert.Equal(
            ["Don't use Focus", "Work", "Writing", "Do Not Disturb", "Personal", "Sleep"],
            focus.Choices.Take(6).Select(choice => choice.Label));
        // Neither Off Shortcut is a mode to choose.
        Assert.DoesNotContain(focus.Choices, choice => choice.ModeName.StartsWith("Off", StringComparison.Ordinal));
        Assert.Equal("Ready", focus.Choices.Single(c => c.ModeName == "Work").Status);
        Assert.Equal("Needs Shortcut", focus.Choices.Single(c => c.ModeName == "Sleep").Status);
        Assert.True(focus.Selected.IsNone);
        Assert.False(focus.HasHint);
    }

    [Fact]
    public void ChoosingAModeWithoutItsShortcutSaysWhichShortcutToMake()
    {
        FakeSystemCatalog catalog = new();
        catalog.ShortcutNames.Add(FocusModes.OffShortcutName("Reading"));
        FocusSettingsViewModel focus = new(catalog, new FakeProcessRunner(), new FakeFocusShortcutInstaller(), new FocusConfig());

        focus.Selected = focus.Choices.Single(c => c.ModeName == "Reading");

        Assert.True(focus.IsShortcutMissing);
        Assert.False(focus.IsOffShortcutMissing);
        Assert.Contains($"“{Prefix}Reading”", focus.MissingShortcutText, StringComparison.Ordinal);
        Assert.Equal(new FocusConfig { Enabled = true, ModeName = "Reading" }, focus.ToConfig());
    }

    [Fact]
    public async Task CheckingAgainPicksUpAShortcutMadeMeanwhile()
    {
        FakeSystemCatalog catalog = new();
        FocusSettingsViewModel focus = new(catalog, new FakeProcessRunner(), new FakeFocusShortcutInstaller(), new FocusConfig { Enabled = true, ModeName = "Work" });
        Assert.True(focus.IsShortcutMissing);
        Assert.True(focus.IsOffShortcutMissing);

        catalog.ShortcutNames.AddRange([$"{Prefix}Work", FocusModes.OffShortcutName("Work")]);
        await focus.LoadShortcutsAsync();

        Assert.False(focus.HasHint);
        Assert.Equal("Work", focus.Selected.ModeName);
        Assert.Contains(focus.Selected, focus.Choices);
    }

    [Fact]
    public void AModeNameFromConfigIsKeptEvenIfNoListHasIt()
    {
        FocusSettingsViewModel focus = new(new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), new FocusConfig { Enabled = true, ModeName = "Deep Work" });

        Assert.Equal("Deep Work", focus.Selected.ModeName);
        Assert.Equal(new FocusConfig { Enabled = true, ModeName = "Deep Work" }, focus.ToConfig());

        focus.Selected = FocusChoiceViewModel.None;
        Assert.Equal(new FocusConfig { Enabled = false, ModeName = string.Empty }, focus.ToConfig());
    }

    /// <summary>
    /// For a mode macOS ships with, "Create it" offers the Shortcut, then watches for the user to add
    /// it - and the moment it shows up, the mode reads Ready with no "Check again" needed.
    /// </summary>
    [Fact]
    public async Task CreatingAShortcutForABuiltInModeEndsWithItReady()
    {
        FakeSystemCatalog catalog = new();
        FakeFocusShortcutInstaller installer = new() { AddsTo = catalog };
        FocusSettingsViewModel focus = new(catalog, new FakeProcessRunner(), installer, new FocusConfig { Enabled = true, ModeName = "Sleep" })
        {
            PollInterval = TimeSpan.FromMilliseconds(1)
        };
        Assert.True(focus.CanCreateShortcut);
        Assert.True(focus.CanCreateOffShortcut);

        await focus.OfferShortcutAsync("Sleep", turnOff: false);
        await focus.OfferShortcutAsync("Sleep", turnOff: true);

        Assert.Equal([$"{Prefix}Sleep", $"{Prefix}Off - Sleep"], installer.Offered);
        Assert.False(focus.HasHint);
        Assert.Equal("Ready", focus.Selected.Status);
        Assert.Equal(string.Empty, focus.ShortcutStatus);
    }

    /// <summary>
    /// The single "Focus Off" Shortcut - which turned off every built-in mode and failed on the first
    /// one this Mac never had set up - does not stand in for a built-in mode's own Off Shortcut. It
    /// still does for a Focus the user made, which cannot be given one automatically.
    /// </summary>
    [Fact]
    public void EachBuiltInModeWantsItsOwnOffShortcut()
    {
        FakeSystemCatalog catalog = new();
        catalog.ShortcutNames.AddRange([$"{Prefix}Do Not Disturb", $"{Prefix}Off", $"{Prefix}Deep Work"]);

        FocusSettingsViewModel builtIn = new(catalog, new FakeProcessRunner(), new FakeFocusShortcutInstaller(), new FocusConfig { Enabled = true, ModeName = "Do Not Disturb" });
        Assert.True(builtIn.IsOffShortcutMissing);
        Assert.True(builtIn.CanCreateOffShortcut);
        Assert.Contains($"\u201c{Prefix}Off - Do Not Disturb\u201d", builtIn.MissingOffShortcutText, StringComparison.Ordinal);

        FocusSettingsViewModel custom = new(catalog, new FakeProcessRunner(), new FakeFocusShortcutInstaller(), new FocusConfig { Enabled = true, ModeName = "Deep Work" });
        Assert.False(custom.IsOffShortcutMissing);
    }

    [Fact]
    public void AFocusTheUserMadeCannotBeCreatedForThem()
    {
        FocusSettingsViewModel focus = new(new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), new FocusConfig { Enabled = true, ModeName = "Deep Work" });

        Assert.True(focus.IsShortcutMissing);
        Assert.False(focus.CanCreateShortcut);
    }

    [Fact]
    public async Task AFailedOfferSaysWhy()
    {
        FakeFocusShortcutInstaller installer = new() { Problem = "Couldn't prepare the Shortcut." };
        FocusSettingsViewModel focus = new(new FakeSystemCatalog(), new FakeProcessRunner(), installer, new FocusConfig { Enabled = true, ModeName = "Work" });

        await focus.OfferShortcutAsync("Work", turnOff: false);

        Assert.Equal("Couldn't prepare the Shortcut.", focus.ShortcutStatus);
        Assert.True(focus.IsShortcutMissing);
    }

    [Fact]
    public void OpenShortcutsOpensTheShortcutsApp()
    {
        FakeProcessRunner runner = new();
        FocusSettingsViewModel focus = new(new FakeSystemCatalog(), runner, new FakeFocusShortcutInstaller(), new FocusConfig());

        focus.OpenShortcutsCommand.Execute(null);

        ProcessStartOptions call = Assert.Single(runner.Calls);
        Assert.Equal("open", call.FileName);
        Assert.Equal(["-a", "Shortcuts"], call.Arguments);
    }

    [Fact]
    public void PlaylistsComeStraightFromMusicWhenItIsOpen()
    {
        FakeSystemCatalog catalog = new() { IsMusicRunning = true };
        catalog.MusicPlaylists!.AddRange(["Deep Focus", "Running"]);

        MediaSettingsViewModel media = new(catalog, new MediaConfig { Player = MediaPlayerKind.AppleMusic, Playlist = "Running" });

        Assert.Equal(["Deep Focus", "Running"], media.MusicPlaylists);
        Assert.Equal("Running", media.Playlist);
        Assert.False(media.CanLoadMusicPlaylists);
        Assert.Equal([false], catalog.MusicRequests);
    }

    /// <summary>
    /// Reading playlists means scripting Music, which opens it. Opening Profile Setup must not do
    /// that on its own; the user asks, with the button.
    /// </summary>
    [Fact]
    public void MusicIsOnlyOpenedWhenTheUserAsks()
    {
        FakeSystemCatalog catalog = new() { IsMusicRunning = false };
        catalog.MusicPlaylists!.Add("Deep Focus");

        MediaSettingsViewModel media = new(catalog, new MediaConfig { Player = MediaPlayerKind.AppleMusic, Playlist = "Old Favourite" });

        Assert.Equal([false], catalog.MusicRequests);
        Assert.True(media.CanLoadMusicPlaylists);
        Assert.Equal(["Old Favourite"], media.MusicPlaylists);

        media.LoadMusicPlaylistsCommand.Execute(null);

        Assert.Equal([false, true], catalog.MusicRequests);
        Assert.False(media.CanLoadMusicPlaylists);
        Assert.Equal(["Old Favourite", "Deep Focus"], media.MusicPlaylists);
        Assert.Equal("Old Favourite", media.Playlist);
    }

    [Fact]
    public void MusicThatCannotBeReadSaysWhy()
    {
        FakeSystemCatalog catalog = new() { MusicPlaylists = null };
        MediaSettingsViewModel media = new(catalog, new MediaConfig { Player = MediaPlayerKind.AppleMusic });

        media.LoadMusicPlaylistsCommand.Execute(null);

        Assert.Contains("Automation", media.MusicStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangingPlayerDropsAPlaylistTheNewPlayerCannotUse()
    {
        MediaSettingsViewModel media = new(new FakeSystemCatalog(), new MediaConfig { Player = MediaPlayerKind.Spotify, Playlist = "spotify:playlist:abc", AutoPlay = true });

        media.SelectedPlayer = media.Players.Single(p => p.Value == MediaPlayerKind.AppleMusic);

        Assert.Equal(string.Empty, media.Playlist);
        Assert.True(media.IsAppleMusic);
        Assert.Equal(new MediaConfig { Player = MediaPlayerKind.AppleMusic, Playlist = string.Empty, AutoPlay = true }, media.ToConfig());
    }

    [Fact]
    public void APastedSpotifyLinkIsSavedAsTheUriSpotifyPlays()
    {
        MediaSettingsViewModel media = new(new FakeSystemCatalog(), new MediaConfig { Player = MediaPlayerKind.Spotify })
        {
            Playlist = "https://open.spotify.com/playlist/37i9dQZF1DX0XUsuxWHRQd?si=x"
        };

        Assert.Equal("spotify:playlist:37i9dQZF1DX0XUsuxWHRQd", media.ToConfig().Playlist);
    }

    [Fact]
    public void BrowserProfilesAreOfferedByTheNameTheBrowserShows()
    {
        FakeSystemCatalog catalog = new();
        catalog.BrowserProfiles[BrowserKind.Chrome] = [new("Default", "Artem"), new("Profile 2", "Client")];

        BrowserProfileRowViewModel row = new(BrowserKind.Chrome, "Profile 2", [], catalog.GetBrowserProfiles, _ => { });

        Assert.Equal(["Artem (Default)", "Client (Profile 2)"], row.ProfileChoices.Select(c => c.Label));
        Assert.Equal("Profile 2", row.ProfileDirectory);
    }

    [Fact]
    public void ASavedProfileTheBrowserNoLongerHasStaysListed()
    {
        FakeSystemCatalog catalog = new();
        catalog.BrowserProfiles[BrowserKind.Chrome] = [new("Default", "Artem")];

        BrowserProfileRowViewModel row = new(BrowserKind.Chrome, "Profile 7", [], catalog.GetBrowserProfiles, _ => { });

        Assert.Equal("Profile 7", row.ProfileDirectory);
        Assert.Equal("Profile 7 (not found)", row.SelectedProfile!.Label);
    }

    [Fact]
    public void SwitchingBrowserListsThatBrowsersProfilesAndPicksOne()
    {
        FakeSystemCatalog catalog = new();
        catalog.BrowserProfiles[BrowserKind.Chrome] = [new("Default", "Artem"), new("Profile 2", "Client")];

        BrowserProfileRowViewModel row = new(BrowserKind.Chrome, "Profile 2", [], catalog.GetBrowserProfiles, _ => { });
        row.SelectedBrowser = BrowserProfileRowViewModel.BrowserChoices.Single(c => c.Value == BrowserKind.Brave);

        // Brave has no profiles readable here, but every Chromium install has Default.
        Assert.Equal(["Default"], row.ProfileChoices.Select(c => c.Value));
        Assert.Equal("Default", row.ProfileDirectory);
        Assert.Equal(BrowserKind.Brave, row.Browser);
    }

    [Fact]
    public async Task APickerTellsUnavailableFromEmptyAndHidesWhatIsAlreadyAdded()
    {
        List<string> added = ["redis"];
        IReadOnlyList<PickerChoice>? source = null;
        ChoicePickerViewModel picker = new(
            _ => Task.FromResult(source),
            () => added,
            choice => added.Add(choice.Value),
            "nothing left",
            "start docker",
            "IconBox",
            "Search containers…");

        await picker.LoadAsync();
        Assert.True(picker.IsUnavailable);
        Assert.False(picker.IsEmpty);

        source = [new("redis", "redis"), new("api-db", "api-db")];
        await picker.LoadAsync();
        Assert.False(picker.IsUnavailable);
        PickerItemViewModel only = Assert.Single(picker.Items);
        Assert.Equal("api-db", only.Title);

        only.PickCommand.Execute(null);
        Assert.Equal(["redis", "api-db"], added);
        Assert.Empty(picker.Items);
        Assert.True(picker.IsEmpty);
    }

    [Fact]
    public void PickedContainersAreShownNotEditedButTypedOnesAreEditable()
    {
        FakeSystemCatalog catalog = new() { DockerContainers = ["api-db", "redis"] };
        ProfileSetupViewModel viewModel = NewProfile(catalog);

        viewModel.DockerStartPicker.LoadCommand.Execute(null);
        viewModel.DockerStartPicker.Items.Single(item => item.Title == "redis").PickCommand.Execute(null);
        viewModel.AddDockerStartCommand.Execute(null);

        Assert.Equal(["redis", string.Empty], viewModel.DockerStart.Select(row => row.Value));
        Assert.Equal([false, true], viewModel.DockerStart.Select(row => row.IsEditable));
        Assert.Equal(["api-db"], viewModel.DockerStartPicker.Items.Select(item => item.Title));
        Assert.Equal(1, catalog.DockerReads);
    }

    [Fact]
    public void AnOpenTabBecomesAStartupUrlOrAQuickLinkWithItsTitle()
    {
        FakeSystemCatalog catalog = new();
        catalog.OpenTabs.Add(new OpenTab("Pull requests", "https://github.com/pulls"));
        ProfileSetupViewModel viewModel = NewProfile(catalog);
        viewModel.BrowserKind = BrowserKind.Safari;

        viewModel.UrlTabPicker.LoadCommand.Execute(null);
        Assert.Equal("github.com", Assert.Single(viewModel.UrlTabPicker.Items).Detail);
        viewModel.UrlTabPicker.Items[0].PickCommand.Execute(null);

        viewModel.QuickLinkTabPicker.LoadCommand.Execute(null);
        viewModel.QuickLinkTabPicker.Items[0].PickCommand.Execute(null);

        Assert.Equal(["https://github.com/pulls"], viewModel.BrowserUrls.Select(row => row.Value));
        QuickLinkRowViewModel link = Assert.Single(viewModel.QuickLinks);
        Assert.Equal(("Pull requests", "https://github.com/pulls"), (link.Title, link.Url));
        Assert.Equal([BrowserKind.Safari, BrowserKind.Safari], catalog.OpenTabRequests);
        Assert.Empty(viewModel.UrlTabPicker.Items);
    }

    [Fact]
    public void AnAppAddedByHandIsTheOnlyEditableAppRow()
    {
        ProfileSetupViewModel viewModel = NewProfile(new FakeSystemCatalog());
        viewModel.AppPicker.SearchText = string.Empty;

        viewModel.AddAppCommand.Execute(null);

        Assert.True(Assert.Single(viewModel.Apps).IsEditable);
        Assert.False(new AppRowViewModel("Slack", true, true, _ => { }).IsEditable);
    }

    /// <summary>
    /// Saving fills in what is no longer typed: the menu bar label follows the name, and a quick
    /// link left untitled is named after its site rather than drawn as a blank button.
    /// </summary>
    [Fact]
    public async Task SavingFillsInTheFieldsThatAreNoLongerTyped()
    {
        FakeSystemCatalog catalog = new();
        catalog.ShortcutNames.Add($"{Prefix}Work");
        ProfileSetupViewModel viewModel = NewProfile(catalog, out InMemoryJsonStore jsonStore, out ConfigPaths paths);
        viewModel.DisplayName = "Deep Work";
        viewModel.AddQuickLinkCommand.Execute(null);
        viewModel.QuickLinks[0].Url = "https://linear.app/team/inbox";
        viewModel.Focus.Selected = viewModel.Focus.Choices.Single(c => c.ModeName == "Work");
        viewModel.Media.SelectedPlayer = viewModel.Media.Players.Single(p => p.Value == MediaPlayerKind.Spotify);
        viewModel.Media.Playlist = "https://open.spotify.com/playlist/abc?si=1";

        TaskCompletionSource saved = new();
        viewModel.Saved += (_, _) => saved.TrySetResult();
        viewModel.SaveCommand.Execute(null);
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        ContextDefinition context = jsonStore.Get<AppConfiguration>(paths.SettingsPath)!.Contexts.Single(c => c.Id == "deep-work");
        Assert.Equal("DEEP WORK", context.MenuBarLabel);
        Assert.Equal("linear.app", Assert.Single(context.QuickLinks).Title);
        Assert.Equal(new FocusConfig { Enabled = true, ModeName = "Work" }, context.Focus);
        Assert.Equal("spotify:playlist:abc", context.Media.Playlist);
    }

    private static ProfileSetupViewModel NewProfile(FakeSystemCatalog catalog) => NewProfile(catalog, out _, out _);

    private static ProfileSetupViewModel NewProfile(FakeSystemCatalog catalog, out InMemoryJsonStore jsonStore, out ConfigPaths paths)
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" }]
        };
        ConfigurationValidator validator = new();
        AppHost.UpdateConfiguration(configuration, validator.Validate(configuration));

        jsonStore = new InMemoryJsonStore();
        paths = new ConfigPaths("/tmp/context-switcher-tests");
        jsonStore.Seed(paths.SettingsPath, configuration);
        ConfigurationStore store = new(jsonStore, paths, validator);

        return new ProfileSetupViewModel(store, new FakeInstalledAppsService(), catalog, new FakeProcessRunner(), new FakeFocusShortcutInstaller(), existing: null);
    }
}
