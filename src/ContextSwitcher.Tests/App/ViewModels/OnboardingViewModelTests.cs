using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

[Collection(AppHostTestCollection.Name)]
public sealed class OnboardingViewModelTests
{
    [Fact]
    public void SuggestionsOnlySeedAppsThatAreActuallyInstalled()
    {
        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Slack", null));      // a Work suggestion
        installed.Apps.Add(new InstalledApp("Spotify", null));    // a Personal suggestion
        installed.Apps.Add(new InstalledApp("SomeOtherApp", null));

        OnboardingViewModel viewModel = Create(installed);

        Assert.Contains(viewModel.Work.Apps, a => a.Name == "Slack");
        Assert.Contains(viewModel.Personal.Apps, a => a.Name == "Spotify");

        // Never invents an app that isn't on the machine.
        Assert.DoesNotContain(viewModel.Work.Apps, a => a.Name == "Microsoft Teams");
        Assert.DoesNotContain(viewModel.Work.Apps, a => a.Name == "SomeOtherApp");
    }

    [Fact]
    public void FinishWritesBothProfilesAndMarksOnboardingComplete()
    {
        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Slack", null));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore store = new(jsonStore, configPaths, new ConfigurationValidator());
        StubContextSwitchService switchService = new();

        AppHost.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "default",
                OnboardingCompleted = false,
                Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }]
            },
            new ConfigurationValidationResult([]));

        OnboardingViewModel viewModel = new(store, installed, switchService);
        viewModel.FinishCommand.Execute(null);

        AppConfiguration? saved = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.NotNull(saved);
        Assert.True(saved.OnboardingCompleted);
        Assert.Equal(["work", "personal"], saved.Contexts.Select(c => c.Id));
        Assert.Equal("work", saved.ActiveContextId);

        // The placeholder "default" profile is replaced, not left behind alongside the new ones.
        Assert.DoesNotContain(saved.Contexts, c => c.Id == "default");

        // Finishing switches into Work so the first thing the user sees is the app working.
        Assert.Equal("work", switchService.LastRequest?.TargetContextId);
    }

    [Fact]
    public void SkipMarksOnboardingCompleteWithoutReplacingProfiles()
    {
        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore store = new(jsonStore, configPaths, new ConfigurationValidator());

        AppHost.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "default",
                OnboardingCompleted = false,
                Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }]
            },
            new ConfigurationValidationResult([]));

        OnboardingViewModel viewModel = new(store, new FakeInstalledAppsService(), new StubContextSwitchService());
        viewModel.SkipCommand.Execute(null);

        AppConfiguration? saved = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.NotNull(saved);
        Assert.True(saved.OnboardingCompleted);
        Assert.Equal(["default"], saved.Contexts.Select(c => c.Id));
    }

    [Fact]
    public void StepNavigationStaysWithinBounds()
    {
        OnboardingViewModel viewModel = Create(new FakeInstalledAppsService());

        Assert.True(viewModel.IsWelcomeStep);
        Assert.False(viewModel.CanGoBack);

        viewModel.NextCommand.Execute(null);
        Assert.True(viewModel.IsWorkStep);
        Assert.True(viewModel.CanGoBack);

        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);
        Assert.True(viewModel.IsDoneStep);
        Assert.False(viewModel.CanGoNext);

        // Back remains available on the final step so a profile can still be corrected.
        viewModel.BackCommand.Execute(null);
        Assert.True(viewModel.IsPersonalStep);
    }

    private static OnboardingViewModel Create(FakeInstalledAppsService installed)
    {
        AppHost.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "default",
                OnboardingCompleted = false,
                Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }]
            },
            new ConfigurationValidationResult([]));

        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator());
        return new OnboardingViewModel(store, installed, new StubContextSwitchService());
    }

    /// <summary>
    /// The wizard seeds suggestions with close-on-leave already on, and until this was surfaced in
    /// the UI the only way to keep an app running was to remove it from the profile entirely.
    /// Unticking must now be enough.
    /// </summary>
    [Fact]
    public void ClearingQuitOnLeaveKeepsTheAppLaunchingButNotClosing()
    {
        OnboardingProfileViewModel profile = new(
            "work", "Work", "WORK", "#2F6FED",
            new AppPickerViewModel(new FakeInstalledAppsService(), () => [], _ => { }));

        profile.AddApp("Visual Studio Code");
        profile.AddApp("Calculator");

        AppRowViewModel editor = profile.Apps.Single(row => row.Name == "Visual Studio Code");
        Assert.True(editor.CloseOnLeave);
        editor.CloseOnLeave = false;

        ContextDefinition context = profile.ToContextDefinition();

        Assert.Contains("Visual Studio Code", context.LaunchApps);
        Assert.DoesNotContain("Visual Studio Code", context.CloseApps);
        Assert.Contains("Calculator", context.CloseApps);
    }

}
