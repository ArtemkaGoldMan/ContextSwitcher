using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Contexts;
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

    /// <summary>
    /// A switch with problems puts a card at the bottom of the window, under the profiles rather
    /// than over them, and its close button puts it away.
    /// </summary>
    [Fact]
    public async Task ASwitchWithProblemsShowsACardAtTheBottomThatCanBeClosed()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            MainAppViewModel viewModel = scenario.MainApp();
            Window window = ShowMainWindow(viewModel);
            Border notice = FindControl<Border>(window, b => AutomationProperties.GetName(b) == "Switch problems");

            Assert.False(notice.IsEffectivelyVisible, "the card showed with nothing to report");

            AppHost.UpdateState(new CurrentContextState
            {
                CurrentContextId = "personal",
                LastSwitchCompletedAt = DateTimeOffset.UtcNow,
                LastSwitchStatus = nameof(ContextSwitchStatus.SucceededWithWarnings),
                LastErrors = [new StateError { StepId = "close-app:Slack", Message = "Could not close Slack." }]
            });
            Settle(window);

            Assert.True(IsClickable(notice), "the card did not appear");
            Assert.True(IsClickable(FindControl<TextBlock>(notice, t => t.Text == "Could not close Slack.")));
            ScrollViewer page = FindControl<ScrollViewer>(window, s => s.Name == "ContentScroll");
            Assert.True(
                notice.TranslatePoint(new Point(0, 0), window)!.Value.Y >= page.TranslatePoint(new Point(0, page.Bounds.Height), window)!.Value.Y,
                "the card covers the page instead of sitting under it");

            Click(window, FindControl<Button>(notice, b => AutomationProperties.GetName(b) == "Close notice"));

            Assert.False(notice.IsEffectivelyVisible, "the close button left the card up");
        });
    }

    /// <summary>The card belongs to the Profiles page; editing a profile or reading stats is not the moment.</summary>
    [Fact]
    public async Task TheCardIsOnlyOnTheProfilesPage()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            MainAppViewModel viewModel = scenario.MainApp();
            Window window = ShowMainWindow(viewModel);
            AppHost.UpdateState(new CurrentContextState
            {
                CurrentContextId = "personal",
                LastSwitchCompletedAt = DateTimeOffset.UtcNow,
                LastSwitchStatus = nameof(ContextSwitchStatus.Failed),
                LastErrors = [new StateError { StepId = "docker", Message = "Docker isn't running." }]
            });
            Settle(window);
            Border notice = FindControl<Border>(window, b => AutomationProperties.GetName(b) == "Switch problems");

            Click(window, FindNav(window, "Settings"));
            Assert.False(notice.IsEffectivelyVisible);

            Click(window, FindNav(window, "Profiles"));
            Assert.True(IsClickable(notice));
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
