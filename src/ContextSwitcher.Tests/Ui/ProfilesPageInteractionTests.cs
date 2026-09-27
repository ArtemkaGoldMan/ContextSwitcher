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

    /// <summary>
    /// Delete is a two-step: the first click swaps the row's actions for a "Delete? Yes / No"
    /// confirmation, and only Yes removes the profile. Both halves live behind IsVisible swaps in
    /// the same grid cell, which is exactly the kind of thing that renders wrong without anyone
    /// noticing.
    /// </summary>
    [Fact]
    public async Task DeleteAsksForConfirmationAndNoBacksOut()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(new ProfilesPage { DataContext = viewModel }, height: 600);

            Click(window, FindVisibleButton(window, "Delete"));
            Settle(window);

            Assert.Contains(viewModel.Rows, r => r.IsConfirmingDelete);
            Button no = FindVisibleButton(window, "No");

            Click(window, no);

            Assert.DoesNotContain(viewModel.Rows, r => r.IsConfirmingDelete);
            Assert.Equal(2, viewModel.Rows.Count);
        });
    }

    [Fact]
    public async Task ConfirmingDeleteRemovesAnInactiveProfileAndThePageStillRenders()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(new ProfilesPage { DataContext = viewModel }, height: 600);

            // The second row is Personal; work is active and may not be deleted.
            Click(window, VisibleButtonsNamed(window, "Delete")[1]);
            Settle(window);

            Click(window, FindVisibleButton(window, "Yes"));
            PumpUntil(WaitForRowCount(viewModel, 1));
            Settle(window);

            Assert.Single(viewModel.Rows);
            Assert.Equal("work", viewModel.Rows[0].Context.Id);
            Assert.False(viewModel.HasErrorMessage);
        });
    }

    /// <summary>
    /// Deleting the profile you are currently in is refused, and the refusal has to reach the
    /// screen - the message is bound to a TextBlock whose IsVisible flips with it.
    /// </summary>
    [Fact]
    public async Task DeletingTheActiveProfileIsRefusedAndSaysWhyOnScreen()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(new ProfilesPage { DataContext = viewModel }, height: 600);

            Click(window, VisibleButtonsNamed(window, "Delete")[0]);
            Settle(window);
            Click(window, FindVisibleButton(window, "Yes"));
            PumpUntil(WaitForErrorMessage(viewModel));
            Settle(window);

            Assert.Equal(2, viewModel.Rows.Count);
            Assert.True(viewModel.HasErrorMessage);

            TextBlock shown = FindControl<TextBlock>(window, t => t.Text == viewModel.ErrorMessage && IsClickable(t));
            Assert.Contains("active profile", shown.Text!, StringComparison.Ordinal);
        });
    }

    private static async Task WaitForErrorMessage(ProfilesViewModel viewModel)
    {
        for (int attempt = 0; attempt < 200 && !viewModel.HasErrorMessage; attempt++)
        {
            await Task.Delay(5);
        }
    }

    private static IReadOnlyList<Button> VisibleButtonsNamed(Window window, string content) =>
        FindAll<Button>(window).Where(b => b.Content as string == content && IsClickable(b)).ToList();

    [Fact]
    public async Task DuplicateAddsACopyAndThePageStillRenders()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfilesViewModel viewModel, _, _) = Create();
            Window window = ShowWindow(new ProfilesPage { DataContext = viewModel }, height: 600);

            Click(window, FindVisibleButton(window, "Duplicate"));
            PumpUntil(WaitForRowCount(viewModel, 3));
            Settle(window);

            Assert.Equal(3, viewModel.Rows.Count);
        });
    }

    private static async Task WaitForRowCount(ProfilesViewModel viewModel, int expected)
    {
        for (int attempt = 0; attempt < 200 && viewModel.Rows.Count != expected; attempt++)
        {
            await Task.Delay(5);
        }
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
