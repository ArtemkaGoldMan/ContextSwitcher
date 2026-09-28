using Avalonia;
using Avalonia.Controls;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.App;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class ProfileSetupInteractionTests : UiTest
{
    /// <summary>
    /// "+ Add app" opens the installed-apps picker rather than adding anything itself - the row
    /// comes from the picker or from "Add manually…" inside that flyout. A test that clicks the
    /// outer button and expects a row is asserting the wrong thing about this UI, which is what the
    /// first version of this test did.
    /// </summary>
    [Fact]
    public async Task AddAppOpensThePickerRatherThanAddingARowDirectly()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            Button addApp = FindButton(window, "+ Add app");
            Assert.NotNull(addApp.Flyout);

            Click(window, addApp);

            Assert.True(addApp.Flyout!.IsOpen, "the app picker did not open");
            Assert.Empty(viewModel.Apps);
        });
    }

    [Fact]
    public async Task ClickingARowsRemoveButtonTakesThatAppAway()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            viewModel.Apps.Add(new AppRowViewModel("Slack", true, true, row => viewModel.Apps.Remove(row)));
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            Button remove = FindControl<Button>(window, b => b.Classes.Contains("rowAction"));
            Click(window, remove);

            Assert.Empty(viewModel.Apps);
        });
    }

    [Fact]
    public async Task TypingIntoDisplayNameReachesTheViewModel()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            // Found by content rather than position: removing the Id row shifted every field up
            // one, and a positional lookup silently started typing into Menu bar label instead.
            TextBox displayName = FindAll<TextBox>(window).First(t => t.Text == viewModel.DisplayName);
            displayName.Text = string.Empty;
            Type(window, displayName, "Deep Work");

            Assert.Equal("Deep Work", viewModel.DisplayName);
        });
    }

    /// <summary>
    /// The crash this suite exists for: adding apps and saving published the new configuration from
    /// a thread-pool thread, so the view models rebuilt in response built their brushes there, and
    /// the next frame drawn threw "the calling thread cannot access this object because a different
    /// thread owns it" from inside Border.Render. Settle() renders, so a regression fails here.
    /// </summary>
    [Fact]
    public async Task AddingAppsAndSavingLeavesTheUiRenderable()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, ConfigurationStore store) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            // A subscriber that rebuilds view models on ConfigurationChanged, the way the real
            // Profiles and Dashboard pages do - that rebuild is what produced the stray brushes.
            List<ProfileRowViewModel> rebuilt = [];
            void Rebuild(object? sender, EventArgs e) =>
                rebuilt = AppHost.Configuration.Contexts.Select(c => new ProfileRowViewModel(c, false, _ => Task.CompletedTask, _ => { }, _ => Task.CompletedTask, _ => Task.CompletedTask)).ToList();
            AppHost.ConfigurationChanged += Rebuild;

            try
            {
                viewModel.AddAppCommand.Execute(null);
                viewModel.Apps[0].Name = "Calculator";
                Settle(window);

                TaskCompletionSource saved = new(TaskCreationOptions.RunContinuationsAsynchronously);
                viewModel.Saved += (_, _) => saved.TrySetResult();

                Click(window, FindButton(window, "Save"));

                PumpUntil(saved.Task);
                Assert.False(viewModel.HasErrorMessage, viewModel.ErrorMessage ?? string.Empty);

                // The frame that used to throw.
                Settle(window);
                Assert.NotEmpty(rebuilt);
            }
            finally
            {
                AppHost.ConfigurationChanged -= Rebuild;
            }
        });
    }

    /// <summary>
    /// Each "+ Add" row button is bound to a different collection, and each lives behind two gates:
    /// a collapsed tier, and - for the browser rows - the browser mode that section belongs to.
    /// </summary>
    [Theory]
    [InlineData("+ Add URL", BrowserManagementMode.Urls)]
    [InlineData("+ Add tab group", BrowserManagementMode.Groups)]
    [InlineData("+ Add browser profile", BrowserManagementMode.Profiles)]
    [InlineData("+ Add quick link", BrowserManagementMode.None)]
    public async Task EachAddRowButtonAddsToItsOwnSection(string label, BrowserManagementMode mode)
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            viewModel.BrowserMode = mode;
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2400);
            ExpandSections(window);

            int before = CountRows(viewModel);
            Click(window, FindVisibleButton(window, label));
            Settle(window);

            Assert.Equal(before + 1, CountRows(viewModel));
        });
    }

    /// <summary>
    /// Choosing a browser mode swaps which section is on screen: URLs, tab groups and browser
    /// profiles are mutually exclusive, and None shows none of them.
    /// </summary>
    [Fact]
    public async Task TheBrowserModeDecidesWhichSectionIsShown()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2400);
            ExpandSections(window);

            viewModel.BrowserMode = BrowserManagementMode.None;
            Settle(window);
            Assert.Empty(VisibleButtons(window, "+ Add URL"));

            viewModel.BrowserMode = BrowserManagementMode.Urls;
            Settle(window);
            Assert.NotEmpty(VisibleButtons(window, "+ Add URL"));
            Assert.Empty(VisibleButtons(window, "+ Add tab group"));

            viewModel.BrowserMode = BrowserManagementMode.Groups;
            Settle(window);
            Assert.NotEmpty(VisibleButtons(window, "+ Add tab group"));
            Assert.Empty(VisibleButtons(window, "+ Add URL"));
        });
    }

    private static IReadOnlyList<Button> VisibleButtons(Window window, string content) =>
        FindAll<Button>(window).Where(b => b.Content as string == content && IsClickable(b)).ToList();

    /// <summary>
    /// The lower tiers are collapsed on arrival - their contents are not even in the visual tree
    /// until the header is clicked, which is what keeps the page approachable.
    /// </summary>
    [Fact]
    public async Task CollapsedSectionsRevealTheirContentsWhenOpened()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2400);

            Assert.DoesNotContain(FindAll<Button>(window), b => b.Content as string == "+ Add quick link");

            ExpandSections(window);

            Assert.Contains(FindAll<Button>(window), b => b.Content as string == "+ Add quick link");
        });
    }

    [Fact]
    public async Task ClickingASwatchChangesTheAccentColor()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            Button violet = FindControl<Button>(window, b => b.Classes.Contains("swatch") && ToolTip.GetTip(b) as string == "Violet");
            Click(window, violet);

            Assert.Equal("#7B61FF", viewModel.AccentColor);
            Assert.Contains("selected", violet.Classes);
        });
    }

    [Fact]
    public async Task ClickingAnIconTileChoosesItAndUpdatesThePreview()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            Button music = FindControl<Button>(window, b => b.Classes.Contains("iconChoice") && ToolTip.GetTip(b) as string == "Music");
            Click(window, music);

            Assert.Equal("music", viewModel.Icon);
            Assert.Contains("selected", music.Classes);
        });
    }

    /// <summary>
    /// Every "+ Add" button sits on the same left edge as the rest of its card. The link-styled ones
    /// used to size to their text and float in the middle of the card - reported as the UI not being
    /// straight - while "+ Add app" alone was left-aligned.
    /// </summary>
    [Theory]
    [InlineData(BrowserManagementMode.Urls)]
    [InlineData(BrowserManagementMode.Groups)]
    [InlineData(BrowserManagementMode.Profiles)]
    public async Task EveryAddButtonSharesTheSameLeftEdge(BrowserManagementMode mode)
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            viewModel.BrowserMode = mode;
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2600);
            ExpandSections(window);

            // A list, not a dictionary: Docker has two "+ Add container" buttons, one per direction.
            List<(string Label, double Left)> lefts = FindAll<Button>(window)
                .Where(b => (b.Content as string)?.StartsWith("+ Add", StringComparison.Ordinal) == true && IsClickable(b))
                .Select(b => ((string)b.Content!, Math.Round(b.TranslatePoint(new Point(0, 0), window)!.Value.X)))
                .ToList();

            Assert.True(lefts.Count >= 5, $"expected the add buttons to be on screen, found {lefts.Count}");
            double appLeft = lefts.Single(entry => entry.Label == "+ Add app").Left;
            Assert.All(lefts, entry => Assert.True(entry.Left == appLeft, $"{entry.Label} is at x={entry.Left}, not {appLeft}"));
        });
    }

    [Fact]
    public async Task CancelLeavesTheConfigurationUntouched()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            string originalName = AppHost.Configuration.Contexts[0].DisplayName;
            viewModel.DisplayName = "Renamed But Not Saved";
            Settle(window);

            bool cancelled = false;
            viewModel.CancelRequested += (_, _) => cancelled = true;

            Click(window, FindVisibleButton(window, "Cancel"));
            Settle(window);

            Assert.True(cancelled);
            Assert.Equal(originalName, AppHost.Configuration.Contexts[0].DisplayName);
        });
    }

    [Fact]
    public async Task SavingWithAnEmptyDisplayNameShowsAnErrorRatherThanSaving()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            viewModel.DisplayName = string.Empty;
            Settle(window);

            Click(window, FindVisibleButton(window, "Save"));
            PumpUntil(WaitUntil(() => viewModel.HasErrorMessage));
            Settle(window);

            Assert.True(viewModel.HasErrorMessage);
        });
    }

    private static int CountRows(ProfileSetupViewModel viewModel) =>
        viewModel.BrowserUrls.Count + viewModel.TabGroups.Count + viewModel.BrowserProfiles.Count
        + viewModel.QuickLinks.Count + viewModel.DockerStart.Count + viewModel.DockerStop.Count;

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }

    private static (ProfileSetupViewModel ViewModel, ConfigurationStore Store) CreateViewModel()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" }]
        };
        ConfigurationValidator validator = new();
        AppHost.UpdateConfiguration(configuration, validator.Validate(configuration));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths paths = new("/tmp/context-switcher-ui-tests");
        jsonStore.Seed(paths.SettingsPath, configuration);
        // Deliberately the yielding wrapper: the plain in-memory store never suspends, so a save
        // that resumes on the wrong thread would look identical to one that does not.
        ConfigurationStore store = new(new YieldingJsonStore(jsonStore), paths, validator);

        return (new ProfileSetupViewModel(store, new FakeInstalledAppsService(), configuration.Contexts[0]), store);
    }
}
