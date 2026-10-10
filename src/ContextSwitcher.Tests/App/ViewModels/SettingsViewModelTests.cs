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
    /// <summary>
    /// Each change is written as it is made - no Save - and the switch timeout, which the page no
    /// longer offers because nothing reads it, is carried over untouched rather than reset.
    /// </summary>
    [Fact]
    public async Task ChangesAreSavedAsTheyAreMadeAndTheTimeoutIsKept()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }],
            DefaultSwitchTimeoutSeconds = 37
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore configurationStore = new(jsonStore, configPaths, new ConfigurationValidator());
        SettingsViewModel viewModel = new(configurationStore, new FakePermissionsChecker(), new FakeProcessRunner(), Updates());

        viewModel.ShowDockIcon = true;
        await viewModel.Saving;
        Assert.True(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!.ShowDockIcon);

        viewModel.AnalyticsEnabled = false;
        viewModel.CheckForUpdates = false;
        viewModel.SelectedRetention = viewModel.RetentionChoices.Single(choice => choice.Value == 30);
        await viewModel.Saving;

        AppConfiguration persisted = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)!;
        Assert.True(persisted.ShowDockIcon);
        Assert.False(persisted.Analytics.Enabled);
        Assert.False(persisted.CheckForUpdates);
        Assert.Equal(30, persisted.Analytics.RetentionDays);
        Assert.Equal(37, persisted.DefaultSwitchTimeoutSeconds);
        Assert.False(viewModel.HasErrorMessage);
    }

    /// <summary>
    /// A history length written by hand or by an older build shows as chosen, in order among the
    /// presets, rather than being swapped for the nearest one.
    /// </summary>
    [Fact]
    public void AHistoryLengthThatIsNoPresetIsOfferedAsChosen()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }],
            Analytics = new AnalyticsConfiguration { Enabled = true, RetentionDays = 1095 }
        };
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));

        ConfigurationStore configurationStore = new(new InMemoryJsonStore(), new ConfigPaths("/tmp/context-switcher-tests"), new ConfigurationValidator());
        SettingsViewModel viewModel = new(configurationStore, new FakePermissionsChecker(), new FakeProcessRunner(), Updates());

        Assert.Equal(
            ["1 week", "30 days", "90 days", "6 months", "1 year", "2 years", "3 years"],
            viewModel.RetentionChoices.Select(choice => choice.Label));
        Assert.Equal(1095, viewModel.SelectedRetention.Value);
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
        SettingsViewModel viewModel = new(store, new FakePermissionsChecker(), processRunner, Updates());

        viewModel.SupportDeveloperCommand.Execute(null);

        ProcessStartOptions call = Assert.Single(processRunner.Calls);
        Assert.Equal("open", call.FileName);
        Assert.Equal([AppLinks.Support], call.Arguments);
    }

    private static UpdatesViewModel Updates() =>
        new(new FakeReleaseSource(), new FakeAppUpdater(), new FakeProcessRunner(), new Version(0, 1, 0));
}
