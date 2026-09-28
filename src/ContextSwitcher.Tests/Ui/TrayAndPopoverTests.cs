using Avalonia.Controls;
using ContextSwitcher.App;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views;
using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// Not covered here: the popover hiding when it loses focus. Headless windows never lose activation -
/// activating a second window leaves the first IsActive and never raises its Deactivated - so that
/// behaviour can only be checked against the real app.
/// </summary>
[Collection(AppHostTestCollection.Name)]
public sealed class TrayAndPopoverTests : UiTest
{
    private static readonly IReadOnlyList<ContextDefinition> TwoProfiles =
    [
        new ContextDefinition { Id = "work", DisplayName = "Work" },
        new ContextDefinition { Id = "personal", DisplayName = "Personal" }
    ];

    /// <summary>The menu bar menu lists every profile, ticks the active one, then Open Dashboard and Quit.</summary>
    [Fact]
    public async Task TheTrayMenuListsEveryProfileWithTheActiveOneTicked()
    {
        await OnUiThreadAsync(() =>
        {
            TrayMenu tray = new(_ => { }, () => { }, () => { });
            tray.Update(TwoProfiles, "personal");

            Assert.Equal(["Work", "Personal", "---", TrayMenu.OpenDashboardLabel, "---", TrayMenu.QuitLabel], Shape(tray.Menu));
            NativeMenuItem[] profiles = tray.Menu.Items.OfType<NativeMenuItem>().Take(2).ToArray();
            Assert.All(profiles, item => Assert.Equal(MenuItemToggleType.Radio, item.ToggleType));
            Assert.False(profiles[0].IsChecked);
            Assert.True(profiles[1].IsChecked);
        });
    }

    [Fact]
    public async Task EachMenuItemRunsItsOwnAction()
    {
        await OnUiThreadAsync(() =>
        {
            List<string> switched = [];
            int dashboards = 0, quits = 0;
            TrayMenu tray = new(switched.Add, () => dashboards++, () => quits++);
            tray.Update(TwoProfiles, "work");
            NativeMenuItem Item(string header) => tray.Menu.Items.OfType<NativeMenuItem>().Single(i => i.Header == header);

            Item("Personal").Command!.Execute(null);
            Item("Work").Command!.Execute(null);
            Item(TrayMenu.OpenDashboardLabel).Command!.Execute(null);
            Item(TrayMenu.QuitLabel).Command!.Execute(null);

            Assert.Equal(["personal", "work"], switched);
            Assert.Equal(1, dashboards);
            Assert.Equal(1, quits);
        });
    }

    /// <summary>
    /// On macOS the tray icon's native menu stays bound to the NativeMenu it first exported, so giving
    /// it a replacement crashed the app on the second switch ("The menu being updated does not
    /// match"). A switch must move the tick within the same menu and the same entries.
    /// </summary>
    [Fact]
    public async Task ASwitchMovesTheTickInsideTheSameMenuAndEntries()
    {
        await OnUiThreadAsync(() =>
        {
            TrayMenu tray = new(_ => { }, () => { }, () => { });
            tray.Update(TwoProfiles, "work");
            NativeMenu menu = tray.Menu;
            NativeMenuItem[] before = tray.Menu.Items.OfType<NativeMenuItem>().Take(2).ToArray();

            tray.Update(TwoProfiles, "personal");
            tray.Update(TwoProfiles, "work");
            tray.Update(TwoProfiles, "personal");

            Assert.Same(menu, tray.Menu);
            NativeMenuItem[] after = tray.Menu.Items.OfType<NativeMenuItem>().Take(2).ToArray();
            Assert.Same(before[0], after[0]);
            Assert.Same(before[1], after[1]);
            Assert.True(after[1].IsChecked);
            Assert.False(after[0].IsChecked);
        });
    }

    /// <summary>Adding, renaming or removing profiles rewrites the entries - still in the same menu.</summary>
    [Fact]
    public async Task ChangingTheProfilesRewritesTheEntriesInTheSameMenu()
    {
        await OnUiThreadAsync(() =>
        {
            TrayMenu tray = new(_ => { }, () => { }, () => { });
            NativeMenu menu = tray.Menu;
            tray.Update(TwoProfiles, "work");

            tray.Update([.. TwoProfiles, new ContextDefinition { Id = "study", DisplayName = "Study" }], "study");
            Assert.Equal(["Work", "Personal", "Study [x]", "---", TrayMenu.OpenDashboardLabel, "---", TrayMenu.QuitLabel], Shape(menu, ticks: true));

            tray.Update([new ContextDefinition { Id = "work", DisplayName = "Office" }], "work");
            Assert.Equal(["Office [x]", "---", TrayMenu.OpenDashboardLabel, "---", TrayMenu.QuitLabel], Shape(menu, ticks: true));

            tray.Update([], null);
            Assert.Equal([TrayMenu.OpenDashboardLabel, "---", TrayMenu.QuitLabel], Shape(menu));
            Assert.Same(menu, tray.Menu);
        });
    }

    private static string[] Shape(NativeMenu menu, bool ticks = false) =>
        menu.Items
            // A separator is itself a NativeMenuItem (with header "-"), so it has to be checked first.
            .Select(item => item is NativeMenuItemSeparator ? "---"
                : item is NativeMenuItem m ? m.Header + (ticks && m.IsChecked ? " [x]" : string.Empty)
                : "?")
            .ToArray();

    /// <summary>A clean switch from the popover closes it, the way picking a menu item closes a menu.</summary>
    [Fact]
    public async Task ACleanSwitchFromThePopoverDismissesIt()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            DashboardWindow popover = new(scenario.Dashboard());
            popover.Show();
            Settle(popover);

            Click(popover, FindSwitchButton(popover, "Personal"));
            PumpUntil(WaitUntil(() => !popover.IsVisible));

            Assert.False(popover.IsVisible);
        });
    }

    /// <summary>A switch that left something to read keeps the popover open so it can be read.</summary>
    [Fact]
    public async Task ASwitchWithWarningsKeepsThePopoverOpen()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.SwitchService.Result = scenario.SwitchService.Result with { Status = ContextSwitchStatus.SucceededWithWarnings };
            DashboardWindow popover = new(scenario.Dashboard());
            popover.Show();
            Settle(popover);

            Click(popover, FindSwitchButton(popover, "Personal"));
            PumpUntil(WaitUntil(() => scenario.SwitchService.LastRequest is not null));
            PumpUntil(Task.Delay(300));

            Assert.True(popover.IsVisible);
        });
    }

    /// <summary>
    /// Warnings come from state.json, which records every switch - so one made from the menu bar, the
    /// CLI or a Shortcut shows up in the popover too, not only one made from the popover itself.
    /// </summary>
    [Fact]
    public async Task ThePopoverShowsTheLastSwitchsWarningsWhoeverMadeIt()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            DashboardViewModel viewModel = scenario.Dashboard();

            AppHost.UpdateState(new CurrentContextState
            {
                CurrentContextId = "personal",
                LastSwitchStatus = nameof(ContextSwitchStatus.SucceededWithWarnings),
                LastErrors = [new StateError { StepId = "CloseApplications.work", Message = "Could not close: Slack." }]
            });

            Assert.Equal(["Could not close: Slack."], viewModel.LastSwitchWarnings);
            Assert.True(viewModel.HasLastSwitchWarnings);
        });
    }

    private static Button FindSwitchButton(Window window, string profile) =>
        FindControl<Button>(window, b => b.Classes.Contains("switchContext")
            && b.DataContext is SwitchButtonViewModel vm && vm.DisplayName == profile && IsClickable(b));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }
}
