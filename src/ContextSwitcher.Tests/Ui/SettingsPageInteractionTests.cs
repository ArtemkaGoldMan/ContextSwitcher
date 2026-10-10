using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Styling;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class SettingsPageInteractionTests : UiTest
{
    /// <summary>
    /// There is no Save button: flipping a switch is the change, the way macOS's own settings work.
    /// A Save button at the top of a page of toggles left it unclear whether anything had applied.
    /// </summary>
    [Fact]
    public async Task FlippingASwitchSavesItStraightAway()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1000);

            Assert.DoesNotContain(FindAll<Button>(window), b => b.Content as string == "Save");

            bool before = AppHost.Configuration.Analytics.Enabled;
            Click(window, FindControl<ToggleSwitch>(window, t => AutomationProperties.GetName(t) == "Record time per profile"));
            PumpUntil(WaitUntil(() => AppHost.Configuration.Analytics.Enabled != before));

            Assert.Equal(!before, AppHost.Configuration.Analytics.Enabled);
            Assert.False(viewModel.HasErrorMessage, viewModel.ErrorMessage ?? string.Empty);
        });
    }

    [Fact]
    public async Task ChoosingHowLongToKeepHistorySavesIt()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1000);

            // Visible ones only: every ComboBox carries a hidden text box for its editable mode.
            Assert.DoesNotContain(FindAll<TextBox>(window), IsClickable);

            ChooseFromDropdown(window, Dropdown(window, "Keep history for"), "90 days");
            PumpUntil(WaitUntil(() => AppHost.Configuration.Analytics.RetentionDays == 90));

            Assert.Equal(90, AppHost.Configuration.Analytics.RetentionDays);
        });
    }

    /// <summary>
    /// "Check again" redraws the permission's pill and the sentence explaining it once access is
    /// granted in System Settings.
    /// </summary>
    [Fact]
    public async Task CheckingAgainRedrawsThePermissionsState()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Permissions.AutomationGranted = false;
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1000);
            PumpUntil(WaitUntil(() => viewModel.AutomationGranted == false));
            Settle(window);
            Assert.Contains(FindAll<TextBlock>(window), t => t.Text == "Not allowed" && IsClickable(t));

            scenario.Permissions.AutomationGranted = true;
            Click(window, FindVisibleButton(window, "Check again"));
            PumpUntil(WaitUntil(() => viewModel.AutomationGranted == true));
            Settle(window);

            Assert.Contains(FindAll<TextBlock>(window), t => t.Text == "Allowed" && IsClickable(t));
            Assert.StartsWith("Allowed.", viewModel.AutomationStatusText, StringComparison.Ordinal);
        });
    }

    /// <summary>The switch timeout did nothing - nothing read it - so the page no longer offers it.</summary>
    [Fact]
    public async Task OnlySettingsThatDoSomethingAreOffered()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            Window window = ShowWindow(new SettingsPage { DataContext = scenario.Settings() }, height: 1000);

            Assert.DoesNotContain(FindAll<TextBlock>(window), t => t.Text?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true);
            Assert.Equal(
                ["Theme", "Keep history for"],
                FindAll<ComboBox>(window).Where(IsClickable).Select(AutomationProperties.GetName));
        });
    }

    /// <summary>
    /// Choosing a theme recolors the app at once - no restart - and "Match macOS" hands it back to
    /// the system's appearance.
    /// </summary>
    [Fact]
    public async Task ChoosingAThemeAppliesItAtOnce()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1400);
            Application app = Application.Current!;

            Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);

            ChooseFromDropdown(window, Dropdown(window, "Theme"), "Dark");
            PumpUntil(WaitUntil(() => app.RequestedThemeVariant == ThemeVariant.Dark));
            Settle(window);

            Assert.Equal(AppearanceMode.Dark, AppHost.Configuration.Appearance);
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);

            ChooseFromDropdown(window, Dropdown(window, "Theme"), "Match macOS");
            PumpUntil(WaitUntil(() => app.RequestedThemeVariant == ThemeVariant.Default));

            Assert.Equal(AppearanceMode.System, AppHost.Configuration.Appearance);
        });
    }

    private static ComboBox Dropdown(Window window, string name) =>
        FindControl<ComboBox>(window, c => AutomationProperties.GetName(c) == name);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }

    /// <summary>
    /// "Check now" finds the new version, the button turns into "Install and restart" with a link to
    /// what's new, and installing hands the download over for when the app quits.
    /// </summary>
    [Fact]
    public async Task CheckingFindsAnUpdateAndInstallingHandsItOver()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            scenario.Releases.Latest = new ContextSwitcher.Core.Updates.ReleaseInfo(
                new Version(0, 2, 0),
                "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/tag/v0.2.0",
                "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip");
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1400);

            Assert.True(IsClickable(FindVisibleButton(window, "Check now")));
            Assert.DoesNotContain(FindAll<Button>(window), b => b.Content as string == "Install and restart" && IsClickable(b));

            Click(window, FindVisibleButton(window, "Check now"));
            PumpUntil(WaitUntil(() => viewModel.Updates.IsUpdateAvailable));
            Settle(window);

            Assert.True(IsClickable(FindControl<TextBlock>(window, t => t.Text == "Version 0.2.0 is available.")));
            Assert.True(IsClickable(FindVisibleButton(window, "What's new in 0.2.0 ↗")));

            Click(window, FindVisibleButton(window, "Install and restart"));
            PumpUntil(WaitUntil(() => scenario.Updater.Installed is not null));

            Assert.Equal(new Version(0, 2, 0), scenario.Updater.Installed!.Version);
        });
    }

    [Fact]
    public async Task TurningOffAutomaticChecksSavesIt()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 1400);

            Assert.True(AppHost.Configuration.CheckForUpdates);
            Click(window, FindControl<ToggleSwitch>(window, t => AutomationProperties.GetName(t) == "Check for updates automatically"));
            PumpUntil(WaitUntil(() => !AppHost.Configuration.CheckForUpdates));

            Assert.False(AppHost.Configuration.CheckForUpdates);
        });
    }
}
