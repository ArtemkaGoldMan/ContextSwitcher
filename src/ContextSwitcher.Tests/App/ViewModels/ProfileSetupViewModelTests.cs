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
public sealed class ProfileSetupViewModelTests
{
    [Fact]
    public void NewProfileGetsAUniqueEditableId()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "profile",
            Contexts = [new ContextDefinition { Id = "profile", DisplayName = "Profile" }]
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), existing: null);

        Assert.True(viewModel.IsNew);
        Assert.True(viewModel.IsIdEditable);
        Assert.Equal("profile-2", viewModel.Id);
        Assert.Equal("New Profile", viewModel.HeaderText);
    }

    [Fact]
    public void ExistingProfileMergesLaunchAndCloseAppsIntoDualToggleRows()
    {
        ContextDefinition context = new()
        {
            Id = "work",
            DisplayName = "Work",
            LaunchApps = ["Slack", "Both App"],
            CloseApps = ["Spotify", "Both App"]
        };
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), context);

        Assert.False(viewModel.IsIdEditable);
        Assert.Equal(3, viewModel.Apps.Count);
        AppRowViewModel both = Assert.Single(viewModel.Apps, a => a.Name == "Both App");
        Assert.True(both.LaunchOnEnter);
        Assert.True(both.CloseOnLeave);
        AppRowViewModel slack = Assert.Single(viewModel.Apps, a => a.Name == "Slack");
        Assert.True(slack.LaunchOnEnter);
        Assert.False(slack.CloseOnLeave);
    }

    [Fact]
    public void SaveCommandRoundTripsIdentityAndAppsToConfiguration()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), context)
        {
            DisplayName = "Work Renamed"
        };
        viewModel.Apps.Add(new AppRowViewModel("Slack", launchOnEnter: true, closeOnLeave: false, _ => { }));

        bool saved = false;
        viewModel.Saved += (_, _) => saved = true;
        viewModel.SaveCommand.Execute(null);

        Assert.True(saved);
        Assert.False(viewModel.HasErrorMessage);
        AppConfiguration? persisted = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        ContextDefinition updated = Assert.Single(persisted!.Contexts);
        Assert.Equal("Work Renamed", updated.DisplayName);
        Assert.Equal(["Slack"], updated.LaunchApps);
        Assert.Empty(updated.CloseApps);
    }

    [Fact]
    public void SaveCommandWritesOnlyThisProfilesHotkeyPreservingOthers()
    {
        ContextDefinition work = new() { Id = "work", DisplayName = "Work" };
        ContextDefinition personal = new() { Id = "personal", DisplayName = "Personal" };
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [work, personal],
            Hotkeys =
            [
                new HotkeyConfig { Id = "switch-work", ContextId = "work", Accelerator = "Cmd+Alt+Ctrl+W", Enabled = true },
                new HotkeyConfig { Id = "switch-personal", ContextId = "personal", Accelerator = "Cmd+Alt+Ctrl+P", Enabled = true }
            ]
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), work);
        Assert.Equal("Cmd+Alt+Ctrl+W", viewModel.HotkeyAccelerator);

        viewModel.HotkeyAccelerator = "Cmd+Alt+Ctrl+Q";
        viewModel.SaveCommand.Execute(null);

        AppConfiguration? persisted = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.Equal(2, persisted!.Hotkeys.Count);
        Assert.Equal("Cmd+Alt+Ctrl+Q", persisted.Hotkeys.Single(h => h.ContextId == "work").Accelerator);
        Assert.Equal("Cmd+Alt+Ctrl+P", persisted.Hotkeys.Single(h => h.ContextId == "personal").Accelerator);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequestedWithoutSaving()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths);

        ProfileSetupViewModel viewModel = new(store, new FakeInstalledAppsService(), existing: null) { DisplayName = "Should not persist" };
        bool cancelled = false;
        viewModel.CancelRequested += (_, _) => cancelled = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(cancelled);
        Assert.Null(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath));
    }

    [Fact]
    public void AppPickerExcludesAppsAlreadyAddedAndFiltersBySearch()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work", LaunchApps = ["Slack"] };
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Slack", null));
        installed.Apps.Add(new InstalledApp("Discord", null));
        installed.Apps.Add(new InstalledApp("Docker", null));

        ProfileSetupViewModel viewModel = new(store, installed, context);

        // Slack is already on the profile, so the picker must not offer it again.
        Assert.DoesNotContain(viewModel.AppPicker.FilteredApps, app => app.Name == "Slack");
        Assert.Contains(viewModel.AppPicker.FilteredApps, app => app.Name == "Discord");

        viewModel.AppPicker.SearchText = "doc";
        InstalledAppViewModel match = Assert.Single(viewModel.AppPicker.FilteredApps);
        Assert.Equal("Docker", match.Name);
    }

    [Fact]
    public void PickingAnAppAddsItWithBothTogglesOnAndRemovesItFromThePicker()
    {
        ContextDefinition context = new() { Id = "work", DisplayName = "Work" };
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [context] },
            new ConfigurationValidationResult([]));
        ConfigurationStore store = CreateStore(out _, out _);

        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new InstalledApp("Discord", null));

        ProfileSetupViewModel viewModel = new(store, installed, context);
        InstalledAppViewModel discord = Assert.Single(viewModel.AppPicker.FilteredApps);

        discord.PickCommand.Execute(null);

        AppRowViewModel added = Assert.Single(viewModel.Apps);
        Assert.Equal("Discord", added.Name);
        Assert.True(added.LaunchOnEnter);
        Assert.True(added.CloseOnLeave);
        Assert.Empty(viewModel.AppPicker.FilteredApps);
    }

    private static ConfigurationStore CreateStore(out InMemoryJsonStore jsonStore, out ConfigPaths configPaths)
    {
        jsonStore = new InMemoryJsonStore();
        configPaths = new ConfigPaths("/tmp/context-switcher-tests");
        return new ConfigurationStore(jsonStore, configPaths, new ConfigurationValidator());
    }
}
