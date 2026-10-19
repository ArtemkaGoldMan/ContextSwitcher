using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Messages;
using ContextSwitcher.App.Views;
using Avalonia.VisualTree;

namespace ContextSwitcher.Tests.Ui;

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

            scenario.AppState.UpdateState(new CurrentContextState
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

    /// <summary>
    /// An app still running in the background has "Force quit" beside its line, which asks for it to
    /// be ended; a line about an app that may be asking to save has none.
    /// </summary>
    [Fact]
    public async Task AnAppLeftInTheBackgroundCanBeForceQuitFromTheCard()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            Window window = ShowMainWindow(scenario.MainApp());
            scenario.AppState.UpdateState(new CurrentContextState
            {
                CurrentContextId = "personal",
                LastSwitchCompletedAt = DateTimeOffset.UtcNow,
                LastSwitchStatus = nameof(ContextSwitchStatus.SucceededWithWarnings),
                LastErrors = [Keyed(SwitchMessages.StayedInBackground.With("Steam")), Keyed(SwitchMessages.CouldNotClose.With("TextEdit"))]
            });
            Settle(window);
            Border notice = FindControl<Border>(window, b => AutomationProperties.GetName(b) == "Switch problems");

            Assert.True(IsClickable(FindControl<TextBlock>(notice, t => t.Text == "Steam still hasn't quit: it is running in the background, or its window is on another desktop.")));
            Assert.Single(FindAll<Button>(notice), b => b.IsEffectivelyVisible && Equals(b.Content, "Force quit"));
            Button forceQuit = FindControl<Button>(notice, b => AutomationProperties.GetName(b) == "Force quit Steam");
            Assert.True(IsClickable(forceQuit));

            Click(window, forceQuit);
            Settle(window);

            Assert.Equal(["Steam"], scenario.ForceQuit);
        });
    }

    private static StateError Keyed(UserMessage message) =>
        new() { StepId = "CloseApplications.work", Message = message.English, MessageKey = message.Key, MessageArguments = message.Arguments };

    /// <summary>The card belongs to the Profiles page; editing a profile or reading stats is not the moment.</summary>
    [Fact]
    public async Task TheCardIsOnlyOnTheProfilesPage()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            MainAppViewModel viewModel = scenario.MainApp();
            Window window = ShowMainWindow(viewModel);
            scenario.AppState.UpdateState(new CurrentContextState
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
