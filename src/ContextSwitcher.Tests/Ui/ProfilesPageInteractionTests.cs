using Avalonia.Controls;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.App;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class ProfilesPageInteractionTests : UiTest
{
    [Fact]
    public async Task ClickingActivateAsksTheSwitchServiceForThatContext()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, StubContextSwitchService switchService, _) = Create();
            Window window = ShowWindow(new ProfilesPage { DataContext = viewModel }, height: 600);

            // The first row is active, so its Activate is disabled; the second row's is not.
            Button activate = FindAll<Button>(window)
                .Where(b => b.Content as string == "Activate" && b.IsEffectivelyEnabled)
                .First();
            Click(window, activate);

            Assert.NotNull(switchService.LastRequest);
            Assert.Equal("personal", switchService.LastRequest!.TargetContextId);
        });
    }

    /// <summary>
    /// The crash reported from the running app: saving published the new configuration from a
    /// thread-pool thread, so this page rebuilt its rows there and the accent brushes it built
    /// belonged to that thread. Nothing went wrong until the next frame drew one, at which point
    /// Border.Render threw "the calling thread cannot access this object because a different thread
    /// owns it".
    ///
    /// The page has to be on screen and re-rendered after the save for this to bite - a rebuild
    /// whose brushes are never drawn cannot fail.
    /// </summary>
    [Fact]
    public async Task ThePageStillRendersAfterAConfigurationSaveRebuildsItsRows()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, ConfigurationStore store) = Create();
            Window window = ShowWindow(new ProfilesPage { DataContext = viewModel }, height: 600);

            Assert.Equal(2, viewModel.Rows.Count);

            AppConfiguration updated = new()
            {
                ActiveContextId = "work",
                Contexts =
                [
                    new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
                    new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" },
                    new ContextDefinition { Id = "focus", DisplayName = "Focus", AccentColor = "#B4530A" }
                ]
            };

            Task<ConfigurationSaveResult> save = store.SaveAsync(updated, CancellationToken.None);
            PumpUntil(save);
            Assert.True(save.Result.Succeeded);

            // Rows were rebuilt by the ConfigurationChanged handler; drawing them is what used to throw.
            Settle(window);

            Assert.Equal(3, viewModel.Rows.Count);
        });
    }

    private static (ProfilesViewModel ViewModel, StubContextSwitchService SwitchService, ConfigurationStore Store) Create()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" },
                new ContextDefinition { Id = "personal", DisplayName = "Personal", AccentColor = "#20A67A" }
            ]
        };
        ConfigurationValidator validator = new();
        AppHost.UpdateConfiguration(configuration, validator.Validate(configuration));
        AppHost.UpdateState(new CurrentContextState { CurrentContextId = "work" });

        InMemoryJsonStore inner = new();
        ConfigPaths paths = new("/tmp/context-switcher-ui-tests");
        inner.Seed(paths.SettingsPath, configuration);

        // Completes off-context like real file IO, so a save that resumes on the wrong thread is
        // actually observable here.
        YieldingJsonStore jsonStore = new(inner);
        ConfigurationStore store = new(jsonStore, paths, validator);
        StubContextSwitchService switchService = new();

        return (new ProfilesViewModel(switchService, store, jsonStore, paths), switchService, store);
    }
}
