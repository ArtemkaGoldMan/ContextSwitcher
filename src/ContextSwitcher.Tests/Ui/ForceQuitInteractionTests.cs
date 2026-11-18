using Avalonia.Controls;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// Force quit on an app's row in Profile Setup, driven as a person uses it: it is there where apps
/// stay in the tray and not where they don't, ticking it unticks Close, and saving writes it.
/// </summary>
public sealed class ForceQuitInteractionTests : UiTest
{
    private static readonly PlatformCapabilities InTheTray = MacPlatform.Capabilities with { AppsLiveInTray = true };

    private static readonly ContextDefinition Work = new()
    {
        Id = "work",
        DisplayName = "Work",
        AccentColor = "#2F6FED",
        LaunchApps = ["Slack"],
        CloseApps = ["Slack"]
    };

    [Fact]
    public async Task ForceQuitIsOnEachRowWhereAppsStayInTheTrayAndSaysWhy()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.With(new AppConfiguration { ActiveContextId = "work", Contexts = [Work] });
            ProfileSetupViewModel viewModel = scenario.ProfileSetupOn(InTheTray, Work);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 1600);
            ExpandSections(window);

            CheckBox forceQuit = FindControl<CheckBox>(window, c => c.Content as string == "Force quit");
            Assert.True(IsClickable(forceQuit), "Force quit is not on the row");
            TextBlock hint = FindControl<TextBlock>(window, t => t.Text?.Contains("go to the tray and keep running", StringComparison.Ordinal) == true);
            Assert.True(IsClickable(hint), "nothing says why to choose it");
        });
    }

    [Fact]
    public async Task ForceQuitIsNotOfferedOnAMac()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.With(new AppConfiguration { ActiveContextId = "work", Contexts = [Work] });
            ProfileSetupViewModel viewModel = scenario.ProfileSetupOn(MacPlatform.Capabilities, Work);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 1600);
            ExpandSections(window);

            Assert.DoesNotContain(FindAll<CheckBox>(window), c => c.Content as string == "Force quit" && IsClickable(c));
            Assert.DoesNotContain(FindAll<TextBlock>(window), t => t.Text?.Contains("keep running", StringComparison.Ordinal) == true && IsClickable(t));
        });
    }

    [Fact]
    public async Task TickingForceQuitUnticksCloseAndSavingWritesIt()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.With(new AppConfiguration { ActiveContextId = "work", Contexts = [Work] });
            ProfileSetupViewModel viewModel = scenario.ProfileSetupOn(InTheTray, Work);
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel }, height: 1600);
            ExpandSections(window);

            CheckBox forceQuit = FindControl<CheckBox>(window, c => c.Content as string == "Force quit");
            Click(window, forceQuit);

            CheckBox close = FindControl<CheckBox>(window, c => c.Content as string == "Close" && IsClickable(c));
            Assert.True(forceQuit.IsChecked);
            Assert.False(close.IsChecked);

            SaveAndWait(window, viewModel);
            ContextDefinition saved = scenario.AppState.Configuration.Contexts[0];
            Assert.Equal(["Slack"], saved.ForceQuitApps);
            Assert.Empty(saved.CloseApps);
            Assert.Equal(["Slack"], saved.LaunchApps);
        });
    }

    private static void SaveAndWait(Window window, ProfileSetupViewModel viewModel)
    {
        TaskCompletionSource saved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Saved += (_, _) => saved.TrySetResult();

        Click(window, FindVisibleButton(window, "Save"));
        PumpUntil(saved.Task);
        Assert.False(viewModel.HasErrorMessage, viewModel.ErrorMessage ?? string.Empty);
        Settle(window);
    }
}
