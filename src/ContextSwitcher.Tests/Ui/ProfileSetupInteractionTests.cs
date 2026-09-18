using Avalonia.Controls;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.App.Views.Pages;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.App;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class ProfileSetupInteractionTests : UiTest
{
    /// <summary>
    /// "+ Add app" opens the installed-apps picker rather than adding anything itself - the row
    /// comes from the picker or from "Add manually…" inside that flyout. A test that clicks the
    /// outer button and expects a row is asserting the wrong thing about this UI, which is what the
    /// first version of this test did.
    /// </summary>
    [Fact]
    public async Task AddAppOpensThePickerRatherThanAddingARowDirectly()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            Button addApp = FindButton(window, "+ Add app");
            Assert.NotNull(addApp.Flyout);

            Click(window, addApp);

            Assert.True(addApp.Flyout!.IsOpen, "the app picker did not open");
            Assert.Empty(viewModel.Apps);
        });
    }

    [Fact]
    public async Task ClickingARowsRemoveButtonTakesThatAppAway()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            viewModel.Apps.Add(new AppRowViewModel("Slack", true, true, row => viewModel.Apps.Remove(row)));
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            Button remove = FindControl<Button>(window, b => b.Classes.Contains("rowAction"));
            Click(window, remove);

            Assert.Empty(viewModel.Apps);
        });
    }

    [Fact]
    public async Task TypingIntoDisplayNameReachesTheViewModel()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, _) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            TextBox displayName = FindAll<TextBox>(window)[1];
            displayName.Text = string.Empty;
            Type(window, displayName, "Deep Work");

            Assert.Equal("Deep Work", viewModel.DisplayName);
        });
    }

    /// <summary>
    /// The crash this suite exists for: adding apps and saving published the new configuration from
    /// a thread-pool thread, so the view models rebuilt in response built their brushes there, and
    /// the next frame drawn threw "the calling thread cannot access this object because a different
    /// thread owns it" from inside Border.Render. Settle() renders, so a regression fails here.
    /// </summary>
    [Fact]
    public async Task AddingAppsAndSavingLeavesTheUiRenderable()
    {
        await OnUiThreadAsync(() =>
        {
            (ProfileSetupViewModel viewModel, ConfigurationStore store) = CreateViewModel();
            Window window = ShowWindow(new ProfileSetupPage { DataContext = viewModel });

            // A subscriber that rebuilds view models on ConfigurationChanged, the way the real
            // Profiles and Dashboard pages do - that rebuild is what produced the stray brushes.
            List<ProfileRowViewModel> rebuilt = [];
            void Rebuild(object? sender, EventArgs e) =>
                rebuilt = AppHost.Configuration.Contexts.Select(c => new ProfileRowViewModel(c, false, _ => Task.CompletedTask, _ => { }, _ => Task.CompletedTask, _ => Task.CompletedTask)).ToList();
            AppHost.ConfigurationChanged += Rebuild;

            try
            {
                viewModel.AddAppCommand.Execute(null);
                viewModel.Apps[0].Name = "Calculator";
                Settle(window);

                TaskCompletionSource saved = new(TaskCreationOptions.RunContinuationsAsynchronously);
                viewModel.Saved += (_, _) => saved.TrySetResult();

                Click(window, FindButton(window, "Save"));

                PumpUntil(saved.Task);
                Assert.False(viewModel.HasErrorMessage, viewModel.ErrorMessage ?? string.Empty);

                // The frame that used to throw.
                Settle(window);
                Assert.NotEmpty(rebuilt);
            }
            finally
            {
                AppHost.ConfigurationChanged -= Rebuild;
            }
        });
    }

    private static (ProfileSetupViewModel ViewModel, ConfigurationStore Store) CreateViewModel()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", AccentColor = "#2F6FED" }]
        };
        ConfigurationValidator validator = new();
        AppHost.UpdateConfiguration(configuration, validator.Validate(configuration));

        InMemoryJsonStore jsonStore = new();
        ConfigPaths paths = new("/tmp/context-switcher-ui-tests");
        jsonStore.Seed(paths.SettingsPath, configuration);
        // Deliberately the yielding wrapper: the plain in-memory store never suspends, so a save
        // that resumes on the wrong thread would look identical to one that does not.
        ConfigurationStore store = new(new YieldingJsonStore(jsonStore), paths, validator);

        return (new ProfileSetupViewModel(store, new FakeInstalledAppsService(), configuration.Contexts[0]), store);
    }
}
