using Avalonia.Controls;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.App.Views.Pages;
using Avalonia.VisualTree;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class DashboardAndStatsInteractionTests : UiTest
{
    /// <summary>
    /// The popover is the app's main surface - it is what the menu bar icon opens - and every
    /// profile in it is a button that starts a real switch.
    /// </summary>
    [Fact]
    public async Task ClickingAProfileInThePopoverSwitchesToIt()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            DashboardViewModel viewModel = scenario.Dashboard();
            Window window = ShowDashboard(viewModel);

            Button personal = FindControl<Button>(window, b => b.Classes.Contains("switchContext")
                && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Personal")
                && IsClickable(b));

            Click(window, personal);
            PumpUntil(WaitUntil(() => scenario.SwitchService.LastRequest is not null));

            Assert.NotNull(scenario.SwitchService.LastRequest);
            Assert.Equal("personal", scenario.SwitchService.LastRequest!.TargetContextId);
        });
    }

    [Fact]
    public async Task ThePopoverRendersWithEveryProfileListed()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            DashboardViewModel viewModel = scenario.Dashboard();
            Window window = ShowDashboard(viewModel);

            Assert.Equal(2, viewModel.SwitchButtons.Count);
            Settle(window);
        });
    }

    [Fact]
    public async Task SwitchingTheStatsRangeRedrawsThePage()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            StatsViewModel viewModel = scenario.Stats();
            Window window = ShowWindow(new StatsPage { DataContext = viewModel }, height: 800);

            Assert.True(viewModel.IsWeekSelected);

            Click(window, FindVisibleButton(window, "Month"));
            Settle(window);
            Assert.True(viewModel.IsMonthSelected);

            Click(window, FindVisibleButton(window, "Week"));
            Settle(window);
            Assert.True(viewModel.IsWeekSelected);
        });
    }

    private static Window ShowDashboard(DashboardViewModel viewModel)
    {
        DashboardWindow window = new() { DataContext = viewModel };
        window.Show();
        Settle(window);
        return window;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }
}
