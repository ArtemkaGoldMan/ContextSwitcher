using ContextSwitcher.App;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

[Collection(AppHostTestCollection.Name)]
public sealed class SettingsViewModelTests
{
    [Fact]
    public void SaveCommandPersistsGeneralAndAnalyticsSettings()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore configurationStore = new(jsonStore, configPaths, new ConfigurationValidator());
        SettingsViewModel viewModel = new(configurationStore, new FakePermissionsChecker(), new FakeProcessRunner())
        {
            DefaultSwitchTimeoutSecondsText = "60",
            ShowDockIcon = true,
            AnalyticsEnabled = false,
            AnalyticsRetentionDaysText = "30"
        };

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasErrorMessage);
        AppConfiguration? persisted = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.NotNull(persisted);
        Assert.Equal(60, persisted.DefaultSwitchTimeoutSeconds);
        Assert.True(persisted.ShowDockIcon);
        Assert.False(persisted.Analytics.Enabled);
        Assert.Equal(30, persisted.Analytics.RetentionDays);
    }

    [Fact]
    public void SaveCommandRejectsNonNumericTimeoutWithoutWriting()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore configurationStore = new(jsonStore, configPaths, new ConfigurationValidator());
        SettingsViewModel viewModel = new(configurationStore, new FakePermissionsChecker(), new FakeProcessRunner())
        {
            DefaultSwitchTimeoutSecondsText = "not-a-number"
        };

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.HasErrorMessage);
        Assert.Null(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath));
    }

    [Fact]
    public void HotkeysListsEveryProfilesHotkeyWithDisplayName()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }],
            Hotkeys = [new HotkeyConfig { Id = "switch-work", ContextId = "work", Accelerator = "Cmd+Alt+Ctrl+W", Enabled = true }]
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore configurationStore = new(jsonStore, configPaths, new ConfigurationValidator());
        SettingsViewModel viewModel = new(configurationStore, new FakePermissionsChecker(), new FakeProcessRunner());

        HotkeyRowViewModel row = Assert.Single(viewModel.Hotkeys);
        Assert.Equal("Work", row.ContextDisplayName);
        Assert.Equal("Cmd+Alt+Ctrl+W", row.Accelerator);
    }

    /// <summary>
    /// Both "Support the developer" buttons were wired to an empty lambda - visible, clickable and
    /// doing nothing.
    /// </summary>
    [Fact]
    public void SupportDeveloperCommandOpensTheSupportLink()
    {
        AppHost.UpdateConfiguration(
            new AppConfiguration
            {
                ActiveContextId = "work",
                Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
            },
            new ConfigurationValidationResult([]));

        FakeProcessRunner processRunner = new();
        ConfigurationStore store = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator());
        SettingsViewModel viewModel = new(store, new FakePermissionsChecker(), processRunner);

        viewModel.SupportDeveloperCommand.Execute(null);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal("open", call.FileName);
        Assert.Equal([AppLinks.Support], call.Arguments);
    }

}
