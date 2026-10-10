using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ContextSwitcher.App.Controls;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// The same kind of control has to look the same wherever it appears. Each of these was once built
/// two or three different ways on different screens - dropdowns of different widths, pickers of
/// different widths with differently weighted entries, list rows with the remove button inside on
/// one list and outside on the next - so they are measured here rather than left to review.
/// </summary>
[Collection(AppHostTestCollection.Name)]
public sealed class UiConsistencyTests : UiTest
{
    /// <summary>
    /// Every labelled setting on every page is one FormRow: dropdowns and text boxes all the same
    /// width, and they and the toggles all ending on the same right edge.
    /// </summary>
    [Theory]
    [InlineData(BrowserManagementMode.Urls, MediaPlayerKind.AppleMusic)]
    [InlineData(BrowserManagementMode.Groups, MediaPlayerKind.Spotify)]
    public async Task ProfileSetupFieldsShareOneWidthAndOneRightEdge(BrowserManagementMode mode, MediaPlayerKind player)
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = Scenario();
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            viewModel.BrowserMode = mode;
            viewModel.Media.SelectedPlayer = viewModel.Media.Players.Single(p => p.Value == player);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, width: 760, height: 2800);
            ExpandSections(window);

            AssertRowsLineUp(window, minimumFields: 7);
        });
    }

    [Fact]
    public async Task SettingsFieldsShareTheSameWidthAndRightEdge()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = Scenario();
            Window window = ShowWindow(new SettingsPage { DataContext = scenario.Settings() }, width: 760, height: 1000);

            AssertRowsLineUp(window, minimumFields: 1);
        });
    }

    [Fact]
    public async Task EveryAddActionIsALink()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = Scenario();
            List<Button> adds = [];
            foreach (BrowserManagementMode mode in Enum.GetValues<BrowserManagementMode>())
            {
                ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
                viewModel.BrowserMode = mode;
                Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2800);
                ExpandSections(window);
                adds.AddRange(AddButtons(window));
            }

            OnboardingViewModel onboarding = scenario.Onboarding();
            OnboardingWindow onboardingWindow = new() { DataContext = onboarding };
            onboardingWindow.Show();
            onboarding.NextCommand.Execute(null);
            Settle(onboardingWindow);
            adds.AddRange(AddButtons(onboardingWindow));

            string[] labels = adds.Select(b => (string)b.Content!).Distinct().Order().ToArray();
            Assert.Equal(
                ["+ Add app", "+ Add browser profile", "+ Add container", "+ Add quick link", "+ Add tab group", "+ Add URL"],
                labels);
            Assert.All(adds, b => Assert.True(b.Classes.Contains("linkButton"), $"\"{b.Content}\" is not a link"));
        });
    }

    /// <summary>
    /// One card for every row in every list: the same height for every one-line row - apps, URLs,
    /// tab groups, containers, quick links - and the remove button always inside the card.
    /// </summary>
    [Fact]
    public async Task EveryListRowIsTheSameCard()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = Scenario();
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            viewModel.AddTabGroupCommand.Execute(null);
            viewModel.AddDockerStopCommand.Execute(null);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2800);
            ExpandSections(window);

            List<Border> rows = FindAll<Border>(window).Where(b => b.Classes.Contains("listRow") && IsClickable(b)).ToList();
            Assert.True(rows.Count >= 5, $"expected an app, a URL, two containers and a quick link on screen, found {rows.Count} rows");

            double height = rows[0].Bounds.Height;
            Assert.True(
                rows.All(row => Math.Abs(row.Bounds.Height - height) < 0.5),
                "row heights differ: " + string.Join(", ", rows.Select(row => $"{(row.DataContext?.GetType().Name ?? "?")}={row.Bounds.Height}")));
            Assert.All(rows, row => Assert.Contains(
                row.GetVisualDescendants().OfType<Button>(),
                b => Avalonia.Automation.AutomationProperties.GetName(b) == "Remove" && IsClickable(b)));

            // Nothing in a list is drawn outside a card any more.
            Assert.All(
                FindAll<Button>(window).Where(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Remove" && IsClickable(b)),
                remove => Assert.NotNull(FindRowAncestor(remove)));
        });
    }

    /// <summary>
    /// Every "add from a list" flyout - apps, URLs, containers, quick links, and onboarding's apps -
    /// is the same width, starts with a search box, and lists entries drawn the same way.
    /// </summary>
    [Fact]
    public async Task EveryPickerIsBuiltTheSameWay()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = Scenario();
            ProfileSetupViewModel viewModel = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 2800);
            ExpandSections(window);

            // Measured while each is open: a closed flyout's contents leave the visual tree.
            List<(string Name, double Width, double? OneLine, double? TwoLine)> pickers = [];
            foreach (string label in new[] { "+ Add app", "+ Add URL", "+ Add container", "+ Add quick link" })
            {
                Button opener = FindAll<Button>(window).First(b => b.Content as string == label && IsClickable(b));
                Click(window, opener);
                PumpUntil(WaitUntil(() => PickerEntries(Content(opener)).Count > 0));
                pickers.Add(Measure(label, Content(opener)));
                opener.Flyout!.Hide();
                Settle(window);
            }

            OnboardingViewModel onboarding = scenario.Onboarding();
            OnboardingWindow onboardingWindow = new() { DataContext = onboarding };
            onboardingWindow.Show();
            onboarding.NextCommand.Execute(null);
            Settle(onboardingWindow);
            Button onboardingAdd = FindAll<Button>(onboardingWindow).First(b => b.Content as string == "+ Add app" && IsClickable(b));
            Click(onboardingWindow, onboardingAdd);
            PumpUntil(WaitUntil(() => PickerEntries(Content(onboardingAdd)).Count > 0));
            pickers.Add(Measure("onboarding + Add app", Content(onboardingAdd)));

            double width = pickers[0].Width;
            Assert.All(pickers, picker => Assert.True(Math.Abs(picker.Width - width) < 0.5, $"{picker.Name} is {picker.Width} wide, not {width}"));

            // An open tab carries a second line - its site - so it is taller than a container or an
            // app; entries with the same number of lines must match wherever they appear.
            foreach (Func<(string Name, double Width, double? OneLine, double? TwoLine), double?> lines in
                     new Func<(string, double, double?, double?), double?>[] { p => p.Item3, p => p.Item4 })
            {
                List<(string Name, double Height)> measured = pickers.Where(p => lines(p) is not null).Select(p => (p.Name, lines(p)!.Value)).ToList();
                Assert.All(measured, m => Assert.True(Math.Abs(m.Height - measured[0].Height) < 0.5, $"{m.Name}'s entries are {m.Height} tall, not {measured[0].Height}"));
            }

            Assert.Contains(pickers, p => p.OneLine is not null);
            Assert.Contains(pickers, p => p.TwoLine is not null);
        });
    }

    /// <summary>
    /// A picker's width and the height of its one- and two-line entries, after checking it is built
    /// the shared way: a search box first, entries in regular weight.
    /// </summary>
    private static (string Name, double Width, double? OneLine, double? TwoLine) Measure(string name, Control content)
    {
        Assert.NotNull(content.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(IsClickable));
        List<Button> entries = PickerEntries(content);
        Assert.True(entries.Count > 0, $"{name} lists nothing");
        Assert.All(entries, entry => Assert.Equal(Avalonia.Media.FontWeight.Normal, entry.FontWeight));

        static bool HasSecondLine(Button entry) => entry.DataContext is PickerItemViewModel { HasDetail: true };
        double? Height(IEnumerable<Button> some) => some.Select(e => (double?)e.Bounds.Height).FirstOrDefault();
        return (name, content.Bounds.Width, Height(entries.Where(e => !HasSecondLine(e))), Height(entries.Where(HasSecondLine)));
    }

    /// <summary>Every page opens the same way: badge, title, then its action.</summary>
    [Fact]
    public async Task EveryPageHeaderHasItsBadge()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = Scenario();
            Control[] pages =
            [
                new ProfilesPage { DataContext = scenario.Profiles() },
                new SettingsPage { DataContext = scenario.Settings() },
                new StatsPage { DataContext = scenario.Stats() },
                new ProfileSetupPage { DataContext = scenario.ProfileSetup(AppHost.Configuration.Contexts[0]) }
            ];

            foreach (Control page in pages)
            {
                Window window = ShowWindow(page);
                Grid header = page.GetVisualDescendants().OfType<Grid>().First();
                Border badge = Assert.Single(header.Children.OfType<Border>(), b => b.Classes.Contains("badge"));
                Assert.Equal((36, 36), (badge.Bounds.Width, badge.Bounds.Height));
                Assert.Contains(header.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("header"));
                window.Close();
            }
        });
    }

    private static void AssertRowsLineUp(Window window, int minimumFields)
    {
        Assert.True(Application.Current!.TryFindResource("FieldWidth", out object? width) && width is double);
        double fieldWidth = (double)width!;

        List<FormRow> rows = FindAll<FormRow>(window).Where(IsClickable).ToList();
        List<Control> fields = rows
            .Select(row => row.Content)
            .OfType<Control>()
            .Where(control => control is ComboBox or TextBox && IsClickable(control))
            .ToList();
        Assert.True(fields.Count >= minimumFields, $"expected at least {minimumFields} fields in rows, found {fields.Count}");

        Assert.All(fields, field => Assert.Equal(fieldWidth, field.Bounds.Width, precision: 1));

        double RightEdge(Control control) => control.TranslatePoint(new Point(control.Bounds.Width, 0), window)!.Value.X;
        double edge = RightEdge(fields[0]);
        Assert.All(fields, field => Assert.Equal(edge, RightEdge(field), precision: 1));
        Assert.All(
            rows.Select(row => row.Content).OfType<ToggleSwitch>().Where(IsClickable),
            toggle => Assert.Equal(edge, RightEdge(toggle), precision: 1));
    }

    private static IEnumerable<Button> AddButtons(Window window) =>
        FindAll<Button>(window).Where(b =>
            (b.Content as string)?.StartsWith("+ Add", StringComparison.Ordinal) == true
            && b.Content as string != "+ Add new profile"
            && IsClickable(b));

    private static Control Content(Button opener)
    {
        Flyout flyout = Assert.IsType<Flyout>(opener.Flyout);
        Assert.True(flyout.IsOpen, "the picker did not open");
        return Assert.IsAssignableFrom<Control>(flyout.Content);
    }

    private static List<Button> PickerEntries(Control content) =>
        content.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("pickerItem") && IsClickable(b)).ToList();

    private static Border? FindRowAncestor(Control control) =>
        control.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("listRow"));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }

    /// <summary>A profile with something in every list, and something to pick in every picker.</summary>
    private static UiScenario Scenario()
    {
        UiScenario scenario = UiScenario.With(new AppConfiguration
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition
                {
                    Id = "work",
                    DisplayName = "Work",
                    AccentColor = "#2F6FED",
                    LaunchApps = ["Slack"],
                    CloseApps = ["Slack"],
                    BrowserManagement = new BrowserManagementConfig
                    {
                        Mode = BrowserManagementMode.Urls,
                        Browser = BrowserKind.Chrome,
                        Urls = ["https://github.com/pulls"],
                        TabGroups = ["Research"]
                    },
                    Docker = new DockerResourceConfig { Start = ["postgres"] },
                    QuickLinks = [new QuickLinkConfig { Title = "Board", Url = "https://linear.app/board" }]
                },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
            ]
        });
        scenario.InstalledApps.Apps.AddRange([new InstalledApp("Calendar", null), new InstalledApp("Mail", null)]);
        scenario.Catalog.DockerContainers = ["postgres", "redis"];
        scenario.Catalog.OpenTabs.Add(new OpenTab("Inbox", "https://linear.app/inbox"));
        return scenario;
    }
}
