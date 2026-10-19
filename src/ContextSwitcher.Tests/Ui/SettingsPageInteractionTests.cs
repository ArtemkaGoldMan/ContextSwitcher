using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Styling;
using ContextSwitcher.App;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.Ui;

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

            bool before = scenario.AppState.Configuration.Analytics.Enabled;
            Click(window, FindControl<ToggleSwitch>(window, t => AutomationProperties.GetName(t) == "Record time per profile"));
            PumpUntil(WaitUntil(() => scenario.AppState.Configuration.Analytics.Enabled != before));

            Assert.Equal(!before, scenario.AppState.Configuration.Analytics.Enabled);
            Assert.False(viewModel.HasErrorMessage, viewModel.ErrorMessage ?? string.Empty);
        });
    }

    /// <summary>The detailed log is a switch like the others: flipping it is the change, and it says until when.</summary>
    [Fact]
    public async Task TheDetailedLogSwitchTurnsItOnForAWeekAndSaysUntilWhen()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 2400);

            Assert.Null(scenario.AppState.Configuration.DetailedLogUntil);
            Click(window, FindControl<ToggleSwitch>(window, t => AutomationProperties.GetName(t) == "Detailed log"));
            PumpUntil(WaitUntil(() => scenario.AppState.Configuration.DetailedLogUntil is not null));

            Assert.True(scenario.AppState.Configuration.DetailedLogUntil > scenario.Clock.UtcNow.AddDays(6));
            Settle(window);
            Assert.StartsWith("On until", FindControl<TextBlock>(window, t => t.Text?.StartsWith("On until", StringComparison.Ordinal) == true && IsClickable(t)).Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CopyDiagnosticsIsOnThePageAndPutsTheReportOnTheClipboard()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            string? copied = null;
            viewModel.Diagnostics.ClipboardWriter = text =>
            {
                copied = text;
                return Task.FromResult(true);
            };
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 2400);

            Click(window, FindControl<Button>(window, b => b.Content as string == "Copy diagnostics" && IsClickable(b)));
            PumpUntil(WaitUntil(() => copied is not null));

            Assert.StartsWith("Context Switcher diagnostics", copied, StringComparison.Ordinal);
            Settle(window);
            Assert.True(IsClickable(FindControl<TextBlock>(window, t => t.Text == "Copied. Paste it into your message.")));
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
            PumpUntil(WaitUntil(() => scenario.AppState.Configuration.Analytics.RetentionDays == 90));

            Assert.Equal(90, scenario.AppState.Configuration.Analytics.RetentionDays);
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
                ["Language", "Theme", "Keep history for"],
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
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 2400);
            Application app = Application.Current!;
            using ThemeFollower theme = new(scenario.AppState, app);

            Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);

            ChooseFromDropdown(window, Dropdown(window, "Theme"), "Dark");
            PumpUntil(WaitUntil(() => app.RequestedThemeVariant == ThemeVariant.Dark));
            Settle(window);

            Assert.Equal(AppearanceMode.Dark, scenario.AppState.Configuration.Appearance);
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);

            ChooseFromDropdown(window, Dropdown(window, "Theme"), "Match macOS");
            PumpUntil(WaitUntil(() => app.RequestedThemeVariant == ThemeVariant.Default));

            Assert.Equal(AppearanceMode.System, scenario.AppState.Configuration.Appearance);
        });
    }

    /// <summary>
    /// Choosing a language saves it, and since it applies at the next start, the page says so - rather
    /// than the choice looking ignored. Going back to the language showing takes the note away.
    /// </summary>
    [Fact]
    public async Task ChoosingALanguageSavesItAndSaysWhenItApplies()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.WithTwoProfiles();
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 2400);

            Assert.Equal(["Match macOS", "English", "Українська"], viewModel.LanguageChoices.Select(c => c.Label));

            ChooseFromDropdown(window, Dropdown(window, "Language"), "Українська");
            PumpUntil(WaitUntil(() => scenario.AppState.Configuration.Language == "uk"));
            Settle(window);

            Assert.True(IsClickable(FindControl<TextBlock>(window, t => t.Text == "Quit Context Switcher from its menu bar icon and open it again to see it in Українська.")));

            ChooseFromDropdown(window, Dropdown(window, "Language"), "English");
            PumpUntil(WaitUntil(() => scenario.AppState.Configuration.Language == "en"));
            Settle(window);

            Assert.False(viewModel.IsLanguagePending);
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
                "https://github.com/ArtemkaGoldMan/ContextSwitcher/releases/download/v0.2.0/ContextSwitcher.zip")
            {
                Notes = ["Update notifications.", "Turn a profile off."]
            };
            SettingsViewModel viewModel = scenario.Settings();
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 2400);

            Assert.True(IsClickable(FindVisibleButton(window, "Check now")));
            Assert.DoesNotContain(FindAll<Button>(window), b => b.Content as string == "Install and restart" && IsClickable(b));

            Click(window, FindVisibleButton(window, "Check now"));
            PumpUntil(WaitUntil(() => viewModel.Updates.IsUpdateAvailable));
            Settle(window);

            Assert.True(IsClickable(FindControl<TextBlock>(window, t => t.Text == "Version 0.2.0 is available.")));
            // What it brings, before it's installed - and the page for the rest.
            Assert.True(IsClickable(FindControl<TextBlock>(window, t => t.Text == "What's new in 0.2.0")));
            Assert.True(IsClickable(FindControl<TextBlock>(window, t => t.Text == "Turn a profile off.")));
            Assert.True(IsClickable(FindVisibleButton(window, "Release notes on GitHub ↗")));

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
            Window window = ShowWindow(new SettingsPage { DataContext = viewModel }, height: 2400);

            Assert.True(scenario.AppState.Configuration.CheckForUpdates);
            Click(window, FindControl<ToggleSwitch>(window, t => AutomationProperties.GetName(t) == "Check for updates automatically"));
            PumpUntil(WaitUntil(() => !scenario.AppState.Configuration.CheckForUpdates));

            Assert.False(scenario.AppState.Configuration.CheckForUpdates);
        });
    }
}
