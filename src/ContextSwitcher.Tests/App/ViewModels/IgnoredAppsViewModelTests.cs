using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

/// <summary>Settings' apps never touched: adding one takes it out of every profile and says which; removing gives it back to none.</summary>
[Collection(AppHostTestCollection.Name)]
public sealed class IgnoredAppsViewModelTests
{
    [Fact]
    public async Task AddingAnAppTakesItOutOfEveryProfileAndSaysWhich()
    {
        (IgnoredAppsViewModel viewModel, InMemoryJsonStore jsonStore, ConfigPaths paths) = Create();

        await viewModel.AddAsync("Telegram");

        AppConfiguration saved = jsonStore.Get<AppConfiguration>(paths.SettingsPath)!;
        Assert.Equal(["Telegram"], saved.IgnoredApps);
        Assert.Empty(saved.Contexts[0].LaunchApps);
        Assert.Equal("Telegram is never touched now, and was taken out of Work.", viewModel.Message);
        Assert.Equal(["Telegram"], viewModel.Apps.Select(row => row.Name));

        viewModel.Apps[0].RemoveCommand.Execute(null);
        await Task.Delay(50);

        Assert.Empty(jsonStore.Get<AppConfiguration>(paths.SettingsPath)!.IgnoredApps);
        Assert.Empty(viewModel.Apps);
        Assert.Empty(AppHost.Configuration.Contexts[0].LaunchApps);
        viewModel.Dispose();
    }

    /// <summary>
    /// Settings is opened with the main window, so it reads nothing it doesn't need: the installed apps
    /// for the picker only when the picker is opened - and, with nothing on the list, no icons at all.
    /// </summary>
    [Fact]
    public async Task ThePickerReadsTheInstalledAppsOnlyWhenOpened()
    {
        (IgnoredAppsViewModel _, InMemoryJsonStore jsonStore, ConfigPaths paths) = Create();
        FakeInstalledAppsService installed = new();
        installed.Apps.Add(new Core.Applications.InstalledApp("Telegram", null));
        IgnoredAppsViewModel viewModel = new(new ConfigurationStore(jsonStore, paths, new ConfigurationValidator(), AppHost.AppState, new TestLogger()), installed);

        Assert.Equal(0, installed.Reads);

        viewModel.OpenPickerCommand.Execute(null);
        viewModel.OpenPickerCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(1, installed.Reads);
        viewModel.Dispose();
    }

    [Fact]
    public async Task SettingsSavesWhetherToAsk()
    {
        (IgnoredAppsViewModel ignored, InMemoryJsonStore jsonStore, ConfigPaths paths) = Create();
        ignored.Dispose();
        ConfigurationStore store = new(jsonStore, paths, new ConfigurationValidator(), AppHost.AppState, new TestLogger());
        SettingsViewModel settings = new(store, new FakePermissionsChecker(), new FakeProcessRunner(), SettingsViewModelTests.Updates());

        Assert.False(settings.AskOnSwitch);
        settings.AskOnSwitch = true;
        await Task.Delay(100);

        Assert.True(jsonStore.Get<AppConfiguration>(paths.SettingsPath)!.AskOnSwitch);

        // "Don't ask again" in the window turns it off from outside Settings, which follows.
        AppConfiguration off = AppHost.Configuration with { AskOnSwitch = false };
        AppHost.UpdateConfiguration(off, new ConfigurationValidator().Validate(off));
        Assert.False(settings.AskOnSwitch);
    }

    private static (IgnoredAppsViewModel ViewModel, InMemoryJsonStore JsonStore, ConfigPaths Paths) Create()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED", LaunchApps = ["Telegram"] },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
            ]
        };
        ConfigurationValidator validator = new();
        AppHost.UpdateConfiguration(configuration, validator.Validate(configuration));
        InMemoryJsonStore jsonStore = new();
        ConfigPaths paths = new("/tmp/context-switcher-ignored-tests");
        return (new IgnoredAppsViewModel(new ConfigurationStore(jsonStore, paths, validator, AppHost.AppState, new TestLogger()), installedApps: null), jsonStore, paths);
    }
}
