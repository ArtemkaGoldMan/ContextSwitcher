using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Applications;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class OnboardingViewModelTests
{
    private readonly AppState appState = new();

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
        ConfigurationStore store = new(jsonStore, configPaths, new ConfigurationValidator(), this.appState, new TestLogger());
        StubContextSwitchService switchService = new();

        this.appState.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "default",
                OnboardingCompleted = false,
                Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }]
            },
            new ConfigurationValidationResult([]));

        OnboardingViewModel viewModel = new(this.appState, store, installed, switchService, FakeLoginItem.Unavailable());
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
        ConfigurationStore store = new(jsonStore, configPaths, new ConfigurationValidator(), this.appState, new TestLogger());

        this.appState.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "default",
                OnboardingCompleted = false,
                Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }]
            },
            new ConfigurationValidationResult([]));

        OnboardingViewModel viewModel = new(this.appState, store, new FakeInstalledAppsService(), new StubContextSwitchService(), FakeLoginItem.Unavailable());
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

    private OnboardingViewModel Create(FakeInstalledAppsService installed)
    {
        this.appState.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "default",
                OnboardingCompleted = false,
                Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }]
            },
            new ConfigurationValidationResult([]));

        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator(), this.appState, new TestLogger());
        return new OnboardingViewModel(this.appState, store, installed, new StubContextSwitchService(), FakeLoginItem.Unavailable());
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
            "work", "Work", "WORK", "#2F6FED", "briefcase",
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


    /// <summary>
    /// The last step offers opening at login, ticked; finishing with it ticked adds the login item,
    /// and unticking it leaves macOS alone.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FinishingOpensAtLoginOnlyIfLeftTicked(bool ticked)
    {
        FakeLoginItem loginItem = new();
        OnboardingViewModel viewModel = WithLoginItem(loginItem);
        Assert.True(viewModel.CanOpenAtLogin);
        Assert.True(viewModel.OpenAtLogin);

        viewModel.OpenAtLogin = ticked;
        viewModel.FinishCommand.Execute(null);

        Assert.Equal(ticked ? [true] : [], loginItem.Changes);
    }

    /// <summary>Skipping the wizard changes nothing in macOS, and from source the box isn't offered at all.</summary>
    [Fact]
    public void SkippingOrRunningFromSourceAddsNoLoginItem()
    {
        FakeLoginItem loginItem = new();
        WithLoginItem(loginItem).SkipCommand.Execute(null);
        Assert.Empty(loginItem.Changes);

        OnboardingViewModel fromSource = WithLoginItem(new FakeLoginItem { Status = ContextSwitcher.Core.Abstractions.LoginItemStatus.Unavailable });
        Assert.False(fromSource.CanOpenAtLogin);
        Assert.False(fromSource.OpenAtLogin);
    }

    private OnboardingViewModel WithLoginItem(FakeLoginItem loginItem)
    {
        this.appState.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "default", OnboardingCompleted = false, Contexts = [new ContextDefinition { Id = "default", DisplayName = "Default" }] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator(), this.appState, new TestLogger());
        return new OnboardingViewModel(this.appState, store, new FakeInstalledAppsService(), new StubContextSwitchService(), loginItem);
    }
}
