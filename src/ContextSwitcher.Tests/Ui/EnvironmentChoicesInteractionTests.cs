using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// Profile Setup's choose-don't-type controls, driven the way a person uses them: open the
/// dropdown, click the entry; open the flyout, click the item; save; read what was written.
/// </summary>
[Collection(AppHostTestCollection.Name)]
public sealed class EnvironmentChoicesInteractionTests : UiTest
{
    private const string Prefix = FocusSettingsViewModel.ShortcutPrefix;

    [Fact]
    public async Task FocusIsChosenFromADropdownThatNamesTheMissingShortcut()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Catalog.ShortcutNames.Add(ContextSwitcher.Core.Configuration.FocusModes.OffShortcutName("Work"));
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);

            ChooseFromDropdown(window, Dropdown(window, "Focus mode"), "Work");

            TextBlock hint = FindControl<TextBlock>(window, t => t.Text?.Contains($"“{Prefix}Work”", StringComparison.Ordinal) == true);
            Assert.True(IsClickable(hint), "the missing Shortcut is not pointed out");
            Assert.True(IsClickable(FindButton(window, "Open Shortcuts ↗")));

            SaveAndWait(window, viewModel);
            Assert.Equal(new FocusConfig { Enabled = true, ModeName = "Work" }, AppHost.Configuration.Contexts[0].Focus);
        });
    }

    /// <summary>
    /// Pick a built-in Focus, click "Create it" under each missing Shortcut, and - once Shortcuts
    /// has them - the hints go away and the mode reads Ready.
    /// </summary>
    [Fact]
    public async Task CreateItSetsUpTheMissingShortcutsFromThePage()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);

            ChooseFromDropdown(window, Dropdown(window, "Focus mode"), "Reading");
            List<Button> creates = FindAll<Button>(window).Where(b => b.Content as string == "Create it" && IsClickable(b)).ToList();
            Assert.Equal(2, creates.Count);

            foreach (Button create in creates)
            {
                Click(window, create);
            }

            PumpUntil(WaitUntil(() => !viewModel.Focus.HasHint));
            Settle(window);

            Assert.Equal([$"{Prefix}Reading", $"{Prefix}Off - Reading"], scenario.FocusShortcuts.Offered);
            Assert.False(viewModel.Focus.HasHint);
            Assert.DoesNotContain(FindAll<Button>(window), b => b.Content as string == "Create it" && IsClickable(b));
            Assert.Equal("Ready", viewModel.Focus.Selected.Status);
        });
    }

    [Fact]
    public async Task AnAppleMusicPlaylistIsChosenFromMusicsOwnList()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Catalog.IsMusicRunning = true;
            scenario.Catalog.MusicPlaylists!.AddRange(["Deep Focus", "Running"]);
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);

            Assert.DoesNotContain(FindAll<ComboBox>(window), c => AutomationProperties.GetName(c) == "Playlist" && IsClickable(c));

            ChooseFromDropdown(window, Dropdown(window, "Player"), "Apple Music");
            ChooseFromDropdown(window, Dropdown(window, "Playlist"), "Deep Focus");

            SaveAndWait(window, viewModel);
            MediaConfig media = AppHost.Configuration.Contexts[0].Media;
            Assert.Equal((MediaPlayerKind.AppleMusic, "Deep Focus"), (media.Player, media.Playlist));
        });
    }

    /// <summary>
    /// With no player chosen the Environment tier has nothing to type at all; Spotify alone adds a
    /// box, for a pasted link, since Spotify will not list playlists to a script.
    /// </summary>
    [Fact]
    public async Task TheEnvironmentTierOnlyAsksForTypingForASpotifyLink()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);
            Expander environment = FindControl<Expander>(window, e => Avalonia.Automation.AutomationProperties.GetName(e) == "Focus and music");

            Assert.Empty(TypingBoxes(environment));

            ChooseFromDropdown(window, Dropdown(window, "Player"), "Spotify");

            TextBox link = Assert.Single(TypingBoxes(environment));
            Assert.Equal("Spotify link", AutomationProperties.GetName(link));
            ContextSwitcher.App.Controls.FormRow row = Assert.IsType<ContextSwitcher.App.Controls.FormRow>(link.Parent);
            Assert.Contains("Share", row.Description, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AnOpenTabIsAddedAsAStartupUrlFromTheFlyout()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Catalog.OpenTabs.Add(new OpenTab("Pull requests", "https://github.com/pulls"));
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            viewModel.BrowserMode = BrowserManagementMode.Urls;
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);

            Button fromTabs = FindVisibleButton(window, "+ Add URL");
            Click(window, fromTabs);
            Assert.True(fromTabs.Flyout!.IsOpen, "the open-tabs picker did not open");

            ClickInPopup(window, PickerItem(fromTabs, "Pull requests"));

            Assert.Equal(["https://github.com/pulls"], viewModel.BrowserUrls.Select(row => row.Value));

            // The picker stays open for picking several; a click elsewhere only dismisses it.
            fromTabs.Flyout.Hide();
            Settle(window);
            SaveAndWait(window, viewModel);
            Assert.Equal(["https://github.com/pulls"], AppHost.Configuration.Contexts[0].BrowserManagement.Urls);
        });
    }

    [Fact]
    public async Task AContainerPickedFromDockerIsShownAsTextNotABox()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Catalog.DockerContainers = ["api-db", "redis"];
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);

            Button addContainer = FindAll<Button>(window).First(b => b.Content as string == "+ Add container" && IsClickable(b));
            Click(window, addContainer);
            ClickInPopup(window, PickerItem(addContainer, "redis"));

            Assert.Equal(["redis"], viewModel.DockerStart.Select(row => row.Value));
            Assert.Contains(FindAll<TextBlock>(window), t => t.Text == "redis" && IsClickable(t));
            Assert.DoesNotContain(FindAll<TextBox>(window), t => t.Text == "redis" && IsClickable(t));
        });
    }

    [Fact]
    public async Task WithoutDockerTheFlyoutSaysSoAndStillLetsYouTypeAName()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Catalog.DockerContainers = null;
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);

            Button addContainer = FindAll<Button>(window).First(b => b.Content as string == "+ Add container" && IsClickable(b));
            Click(window, addContainer);
            Control flyout = FlyoutContent(addContainer);

            Assert.Contains(flyout.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Docker isn't running", StringComparison.Ordinal) == true && IsClickable(t));

            ClickInPopup(window, flyout.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Type a name…"));

            Assert.True(Assert.Single(viewModel.DockerStart).IsEditable);
            Assert.Contains(FindAll<TextBox>(window), t => t.PlaceholderText == "Container name" && IsClickable(t));
        });
    }

    [Fact]
    public async Task ABrowserProfileIsChosenByTheNameTheBrowserShows()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Catalog.BrowserProfiles[BrowserKind.Chrome] = [new("Default", "Artem"), new("Profile 2", "Client")];
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            viewModel.BrowserMode = BrowserManagementMode.Profiles;
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);

            Click(window, FindVisibleButton(window, "+ Add browser profile"));
            ChooseFromDropdown(window, Dropdown(window, "Browser profile"), "Client (Profile 2)");

            SaveAndWait(window, viewModel);
            BrowserProfileConfig profile = Assert.Single(AppHost.Configuration.Contexts[0].BrowserManagement.Profiles);
            Assert.Equal((BrowserKind.Chrome, "Profile 2"), (profile.Browser, profile.ProfileDirectory));
        });
    }

    /// <summary>
    /// An app chosen from the picker - or already saved - is shown by name. Only "Add manually…"
    /// gives a box to type in, and the menu bar label, which nothing displays, is gone.
    /// </summary>
    [Fact]
    public async Task OnlyAHandAddedAppHasANameToType()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.With(new AppConfiguration
            {
                ActiveContextId = "work",
                Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED", LaunchApps = ["Slack"] }]
            });
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            Assert.Contains(FindAll<TextBlock>(window), t => t.Text == "Slack" && IsClickable(t));
            Assert.DoesNotContain(FindAll<TextBox>(window), t => t.Text == "Slack" && IsClickable(t));
            Assert.DoesNotContain(FindAll<TextBlock>(window), t => t.Text == "Menu bar label");

            viewModel.AddAppCommand.Execute(null);
            Settle(window);

            Assert.Contains(FindAll<TextBox>(window), t => t.PlaceholderText?.StartsWith("App name", StringComparison.Ordinal) == true && IsClickable(t));
        });
    }

    /// <summary>
    /// The real catalog answers later and from a background thread - a process exiting, a script
    /// finishing - where the fake otherwise answers at once. Each picker has to cope with its list
    /// arriving after it was opened, and render once it does.
    ///
    /// This is not a guard against the brush crash: Avalonia 12 bindings take a property change
    /// raised off the UI thread without complaint (checked), so these lists - strings and records,
    /// nothing thread-bound - cannot reproduce it. A brush or bitmap added to a choice would have to
    /// be built on the UI thread, as ProfileIconConverter does for icons.
    /// </summary>
    [Fact]
    public async Task ListsThatArriveFromABackgroundThreadAreDrawnSafely()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Catalog.CompletesOffThread = true;
            scenario.Catalog.ShortcutNames.AddRange([$"{Prefix}Work", $"{Prefix}Off - Work"]);
            scenario.Catalog.IsMusicRunning = true;
            scenario.Catalog.MusicPlaylists!.Add("Deep Focus");
            scenario.Catalog.DockerContainers = ["redis"];
            scenario.Catalog.OpenTabs.Add(new OpenTab("Inbox", "https://linear.app/inbox"));
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            viewModel.BrowserMode = BrowserManagementMode.Urls;
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);

            PumpUntil(WaitUntil(() => viewModel.Focus.Choices.Any(c => c.IsReady)));
            Settle(window);

            ChooseFromDropdown(window, Dropdown(window, "Player"), "Apple Music");
            PumpUntil(WaitUntil(() => viewModel.Media.MusicPlaylists.Count > 0));
            Settle(window);

            Button fromTabs = FindVisibleButton(window, "+ Add URL");
            Click(window, fromTabs);
            PumpUntil(WaitUntil(() => viewModel.UrlTabPicker.Items.Count > 0));
            Settle(window);
            ClickInPopup(window, PickerItem(fromTabs, "Inbox"));
            fromTabs.Flyout!.Hide();
            Settle(window);

            Button addContainer = FindAll<Button>(window).First(b => b.Content as string == "+ Add container" && IsClickable(b));
            Click(window, addContainer);
            PumpUntil(WaitUntil(() => viewModel.DockerStartPicker.Items.Count > 0));
            Settle(window);
            ClickInPopup(window, PickerItem(addContainer, "redis"));

            Assert.Equal(["https://linear.app/inbox"], viewModel.BrowserUrls.Select(row => row.Value));
            Assert.Equal(["redis"], viewModel.DockerStart.Select(row => row.Value));
            Assert.Equal(["Deep Focus"], viewModel.Media.MusicPlaylists);
        });
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }

    private static ComboBox Dropdown(Window window, string name) =>
        FindControl<ComboBox>(window, c => AutomationProperties.GetName(c) == name);

    /// <summary>Text boxes a person could type into: visible, and not a dropdown's hidden editor.</summary>
    private static List<TextBox> TypingBoxes(Control root) =>
        root.GetVisualDescendants().OfType<TextBox>().Where(t => IsClickable(t) && t.FindAncestorOfType<ComboBox>() is null).ToList();

    private static Control FlyoutContent(Button button)
    {
        Flyout flyout = Assert.IsType<Flyout>(button.Flyout);
        Assert.True(flyout.IsOpen, "the picker did not open");
        return Assert.IsAssignableFrom<Control>(flyout.Content);
    }

    private static Button PickerItem(Button opener, string title) =>
        FlyoutContent(opener).GetVisualDescendants().OfType<Button>()
            .Single(b => b.DataContext is PickerItemViewModel item && item.Title == title);

    private static void SaveAndWait(Window window, ProfileSetupViewModel viewModel)
    {
        TaskCompletionSource saved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Saved += (_, _) => saved.TrySetResult();

        Click(window, FindVisibleButton(window, "Save"));
        PumpUntil(saved.Task);
        Assert.False(viewModel.HasErrorMessage, viewModel.ErrorMessage ?? string.Empty);
        Settle(window);
    }
}
