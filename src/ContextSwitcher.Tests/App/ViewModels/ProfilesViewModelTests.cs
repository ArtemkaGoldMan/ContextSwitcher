using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

/// <summary>
/// <see cref="ProfilesViewModel"/>'s commands run through test doubles whose async members all
/// complete on already-finished <see cref="Task"/>s, and this process has no synchronization
/// context - so <c>ICommand.Execute</c> runs each command's async chain synchronously to
/// completion before returning, and these tests assert immediately without awaiting anything.
/// </summary>
[Collection(AppHostTestCollection.Name)]
public sealed class ProfilesViewModelTests
{
    [Fact]
    public void AddCommandRaisesEditRequestedWithNullContext()
    {
        (ProfilesViewModel viewModel, _, _) = CreateViewModel(TwoContexts());
        bool raised = false;
        ContextDefinition? requested = new ContextDefinition { Id = "sentinel" };
        viewModel.EditRequested += (_, context) =>
        {
            raised = true;
            requested = context;
        };

        viewModel.AddCommand.Execute(null);

        Assert.True(raised);
        Assert.Null(requested);
    }

    [Fact]
    public void ActivateCommandSwitchesAndPublishesNewState()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, StubContextSwitchService switchService, InMemoryJsonStore jsonStore) = CreateViewModel(configuration);
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        jsonStore.Seed(configPaths.StatePath, new CurrentContextState { CurrentContextId = "personal" });
        switchService.Result = new ContextSwitchResult("personal", "work", ContextSwitchStatus.Succeeded, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "corr");

        viewModel.Rows[1].ActivateCommand.Execute(null);

        Assert.Equal("personal", switchService.LastRequest?.TargetContextId);
        Assert.Equal(ContextSwitchSource.Dashboard, switchService.LastRequest?.Source);
        Assert.Equal("personal", AppHost.State.CurrentContextId);
    }

    [Fact]
    public void DuplicateCommandSavesCopyWithUniqueId()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration);
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");

        viewModel.Rows[0].DuplicateCommand.Execute(null);

        AppConfiguration? saved = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.NotNull(saved);
        Assert.Equal(3, saved.Contexts.Count);
        Assert.Contains(saved.Contexts, c => c.Id == "work-copy" && c.DisplayName == "Work Copy");
    }

    [Fact]
    public void DeleteCommandRefusesToDeleteTheActiveProfile()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration, activeContextId: "work");
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");

        ProfileRowViewModel workRow = viewModel.Rows[0];
        workRow.DeleteCommand.Execute(null);
        workRow.DeleteCommand.Execute(null);

        Assert.True(viewModel.HasErrorMessage);
        Assert.Null(jsonStore.Get<AppConfiguration>(configPaths.SettingsPath));
    }

    [Fact]
    public void DeleteCommandRemovesNonActiveProfile()
    {
        AppConfiguration configuration = TwoContexts();
        (ProfilesViewModel viewModel, _, InMemoryJsonStore jsonStore) = CreateViewModel(configuration, activeContextId: "work");
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");

        ProfileRowViewModel personalRow = viewModel.Rows[1];
        personalRow.DeleteCommand.Execute(null);
        personalRow.DeleteCommand.Execute(null);

        AppConfiguration? saved = jsonStore.Get<AppConfiguration>(configPaths.SettingsPath);
        Assert.NotNull(saved);
        Assert.Single(saved.Contexts);
        Assert.Equal("work", saved.Contexts[0].Id);
    }

    private static AppConfiguration TwoContexts()
    {
        return new AppConfiguration
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
            ]
        };
    }

    private static (ProfilesViewModel ViewModel, StubContextSwitchService SwitchService, InMemoryJsonStore JsonStore) CreateViewModel(
        AppConfiguration configuration, string activeContextId = "work")
    {
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidationResult([]));
        AppHost.UpdateState(new CurrentContextState { CurrentContextId = activeContextId });

        InMemoryJsonStore jsonStore = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        StubContextSwitchService switchService = new();
        ConfigurationStore configurationStore = new(jsonStore, configPaths, new ConfigurationValidator());

        ProfilesViewModel viewModel = new(switchService, configurationStore, jsonStore, configPaths);
        return (viewModel, switchService, jsonStore);
    }
}
