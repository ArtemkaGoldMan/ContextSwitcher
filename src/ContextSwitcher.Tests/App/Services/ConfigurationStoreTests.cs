using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.Services;

[Collection(AppHostTestCollection.Name)]
public sealed class ConfigurationStoreTests
{
    private static AppConfiguration ValidConfiguration => new()
    {
        ActiveContextId = "work",
        Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
    };

    [Fact]
    public async Task SaveAsyncWritesBacksUpAndPublishesValidConfiguration()
    {
        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        jsonStore.Seed(configPaths.SettingsPath, new AppConfiguration());
        ConfigurationStore store = new(jsonStore, configPaths, new ConfigurationValidator());
        AppHost.UpdateConfiguration(new AppConfiguration(), new ConfigurationValidationResult([]));

        ConfigurationSaveResult result = await store.SaveAsync(ValidConfiguration, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.Equal("work", jsonStore.Get<AppConfiguration>(configPaths.SettingsPath)?.ActiveContextId);
        Assert.Contains(configPaths.SettingsPath, jsonStore.BackedUpPaths);
        Assert.Equal("work", AppHost.Configuration.ActiveContextId);
    }

    [Fact]
    public async Task SaveAsyncRejectsInvalidConfigurationWithoutWriting()
    {
        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        ConfigurationStore store = new(jsonStore, configPaths, new ConfigurationValidator());
        AppConfiguration original = new() { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }] };
        AppHost.UpdateConfiguration(original, new ConfigurationValidationResult([]));

        AppConfiguration invalid = new() { ActiveContextId = "missing", Contexts = [] };
        ConfigurationSaveResult result = await store.SaveAsync(invalid, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Null(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath));
        Assert.Same(original, AppHost.Configuration);
    }
}
