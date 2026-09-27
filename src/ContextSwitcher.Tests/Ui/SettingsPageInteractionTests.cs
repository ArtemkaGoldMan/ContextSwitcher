using Avalonia.Controls;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class SettingsPageInteractionTests : UiTest
{
    [Fact]
    public async Task TogglingAnalyticsAndSavingPersistsIt()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1000);

            bool before = viewModel.AnalyticsEnabled;
            ToggleSwitch analytics = FindAll<ToggleSwitch>(window).First(t => t.IsChecked == before && IsClickable(t));

            Click(window, analytics);
            Assert.NotEqual(before, viewModel.AnalyticsEnabled);

            Click(window, FindVisibleButton(window, "Save"));
            PumpUntil(WaitUntil(() => AppHost.Configuration.Analytics.Enabled == viewModel.AnalyticsEnabled));

            Assert.Equal(viewModel.AnalyticsEnabled, AppHost.Configuration.Analytics.Enabled);
            Settle(window);
        });
    }

    /// <summary>
    /// A non-numeric timeout has to be refused with a message rather than saved or swallowed.
    /// </summary>
    [Fact]
    public async Task SavingAnInvalidTimeoutShowsAnErrorAndKeepsTheOldValue()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1000);

            int original = AppHost.Configuration.DefaultSwitchTimeoutSeconds;
            viewModel.DefaultSwitchTimeoutSecondsText = "not-a-number";
            Settle(window);

            Click(window, FindVisibleButton(window, "Save"));
            PumpUntil(WaitUntil(() => viewModel.HasErrorMessage));
            Settle(window);

            Assert.True(viewModel.HasErrorMessage);
            Assert.Equal(original, AppHost.Configuration.DefaultSwitchTimeoutSeconds);
        });
    }

    /// <summary>
    /// Automation is the only permission left now that global hotkeys - the one thing that needed
    /// Accessibility - are gone, so Refresh has to redraw its pill after it is granted.
    /// </summary>
    [Fact]
    public async Task RefreshingPermissionsRedrawsTheStatusPill()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Permissions.AutomationGranted = false;
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1000);
            PumpUntil(WaitUntil(() => viewModel.AutomationGranted == false));

            scenario.Permissions.AutomationGranted = true;
            Click(window, FindVisibleButton(window, "Refresh"));
            PumpUntil(WaitUntil(() => viewModel.AutomationGranted == true));
            Settle(window);

            Assert.True(viewModel.AutomationGranted);
        });
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }
}
