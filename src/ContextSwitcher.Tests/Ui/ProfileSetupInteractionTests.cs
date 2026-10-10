using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
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
    /// <remarks>
    /// Lists with something to pick from open a picker first, and adding by hand is its footer -
    /// the same for every such list - so for those this clicks through to the footer.
    /// </remarks>
    [Theory]
    [InlineData("+ Add URL", "Type a URL…", BrowserManagementMode.Urls)]
    [InlineData("+ Add tab group", null, BrowserManagementMode.Groups)]
    [InlineData("+ Add browser profile", null, BrowserManagementMode.Profiles)]
    [InlineData("+ Add quick link", "Type a URL…", BrowserManagementMode.None)]
    [InlineData("+ Add container", "Type a name…", BrowserManagementMode.None)]
    public async Task EachAddRowButtonAddsToItsOwnSection(string label, string? footer, BrowserManagementMode mode)
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            viewModel.BrowserMode = mode;
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2400);
            ExpandSections(window);

            int before = CountRows(viewModel);
            Button add = FindAll<Button>(window).First(b => b.Content as string == label && IsClickable(b));
            Click(window, add);

            if (footer is not null)
            {
                Control flyout = Assert.IsAssignableFrom<Control>(((Flyout)add.Flyout!).Content);
                ClickInPopup(window, flyout.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == footer));
                PumpUntil(WaitUntil(() => !add.Flyout.IsOpen));
                Assert.False(add.Flyout.IsOpen, "typing by hand left the picker covering the new row");
            }

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
    public async Task ChoosingAnAccentFromItsDropdownRecolorsThePreview()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });
            Border preview = FindControl<Border>(window, b => Avalonia.Automation.AutomationProperties.GetName(b) == "Profile preview");

            ChooseFromDropdown(window, Dropdown(window, "Accent color"), "Violet");

            Assert.Equal("#7B61FF", viewModel.AccentColor);
            Assert.Equal(Avalonia.Media.Color.Parse("#7B61FF"), Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(preview.Background).Color);
            Assert.False(IsClickable(FindControl<ColorView>(window, _ => true)), "the custom color picker shows for a preset");
        });
    }

    /// <summary>
    /// "Custom" is an entry in the same dropdown, and choosing it reveals the full color picker under
    /// it - the way choosing Apple Music reveals a playlist.
    /// </summary>
    [Fact]
    public async Task ChoosingCustomRevealsTheColorPicker()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            ChooseFromDropdown(window, Dropdown(window, "Accent color"), "Custom");

            Assert.True(viewModel.IsCustomAccent);
            Assert.True(IsClickable(FindControl<ColorView>(window, _ => true)), "the custom color picker did not appear");
        });
    }

    [Fact]
    public async Task ChoosingAnIconFromItsDropdownRedrawsThePreview()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });
            Border preview = FindControl<Border>(window, b => Avalonia.Automation.AutomationProperties.GetName(b) == "Profile preview");
            Avalonia.Controls.Shapes.Path glyph = preview.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
            object? before = glyph.Data;

            ChooseFromDropdown(window, Dropdown(window, "Icon"), "Music");

            Assert.Equal("music", viewModel.Icon);
            Assert.NotSame(before, glyph.Data);
        });
    }

    private static ComboBox Dropdown(Window window, string name) =>
        FindControl<ComboBox>(window, c => Avalonia.Automation.AutomationProperties.GetName(c) == name);

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

        return (new ProfileSetupViewModel(store, new FakeInstalledAppsService(), new FakeSystemCatalog(), new FakeProcessRunner(), new FakeFocusShortcutInstaller(), configuration.Contexts[0]), store);
    }
}
