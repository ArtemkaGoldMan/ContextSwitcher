using Avalonia.Controls;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using Avalonia.VisualTree;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class MainAppWindowInteractionTests : UiTest
{
    [Fact]
    public async Task ClickingEachNavItemShowsThatPage()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            MainAppViewModel viewModel = scenario.MainApp();
            Window window = ShowMainWindow(viewModel);

            Assert.IsType<ProfilesViewModel>(viewModel.CurrentPage);

            Click(window, FindNav(window, "Settings"));
            Assert.IsType<SettingsViewModel>(viewModel.CurrentPage);
            Assert.True(viewModel.IsSettingsActive);

            Click(window, FindNav(window, "Stats"));
            Assert.IsType<StatsViewModel>(viewModel.CurrentPage);

            Click(window, FindNav(window, "Profiles"));
            Assert.IsType<ProfilesViewModel>(viewModel.CurrentPage);
        });
    }

    /// <summary>
    /// Each page is a different template over a different view model; switching between them is
    /// where a binding that only works on one of them shows up.
    /// </summary>
    [Fact]
    public async Task EveryPageRendersWhenNavigatedTo()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            MainAppViewModel viewModel = scenario.MainApp();
            Window window = ShowMainWindow(viewModel);

            foreach (string page in new[] { "Settings", "Stats", "Profiles", "Settings" })
            {
                Click(window, FindNav(window, page));
                Settle(window);
            }
        });
    }

    private static Window ShowMainWindow(MainAppViewModel viewModel)
    {
        MainAppWindow window = new() { DataContext = viewModel };
        window.Show();
        Settle(window);
        return window;
    }

    /// <summary>The rail's buttons hold an icon and a label, so they are found by their label.</summary>
    private static Button FindNav(Window window, string label) =>
        FindControl<Button>(window, b => b.Classes.Contains("navItem")
            && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == label));
}
