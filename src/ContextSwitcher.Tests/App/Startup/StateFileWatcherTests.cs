using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;
using ContextSwitcher.Tests.Ui;

namespace ContextSwitcher.Tests.App.Startup;

/// <summary>
/// Needs a dispatcher, because the watcher debounces on one and marshals to it before touching
/// anything the UI owns.
/// </summary>
public sealed class StateFileWatcherTests : UiTest
{
    /// <summary>
    /// A switch made by Shortcuts, Siri or the CLI happens in another process, so it cannot raise
    /// StateChanged here. Before this the running app kept showing the previous profile until it was
    /// restarted.
    /// </summary>
    [Fact]
    public async Task StateWrittenByAnotherProcessIsPickedUp()
    {
        await OnUiThreadAsync(() =>
        {
            string directory = CreateTempDirectory();
            try
            {
                ConfigPaths paths = new(directory);
                JsonFileStore store = new(FileReplacer.Once, new TestLogger());
                AppState appState = Seeded();

                store.WriteAsync(paths.StatePath, new CurrentContextState { CurrentContextId = "work" })
                    .GetAwaiter().GetResult();

                using StateFileWatcher watcher = new(appState, store, paths);
                watcher.Start();
                bool reloaded = false;
                watcher.StateReloaded += (_, _) => reloaded = true;

                // Stand in for the other process.
                store.WriteAsync(paths.StatePath, new CurrentContextState
                {
                    CurrentContextId = "personal",
                    PreviousContextId = "work",
                    LastSwitchCompletedAt = DateTimeOffset.UnixEpoch.AddMinutes(1)
                }).GetAwaiter().GetResult();

                PumpUntil(WaitUntil(() => reloaded), TimeSpan.FromSeconds(15));

                Assert.True(reloaded);
                Assert.Equal("personal", appState.State.CurrentContextId);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    /// <summary>
    /// Our own writes come back through the watcher as well, so identical state must not be
    /// republished - that would rebuild every view model on a loop.
    /// </summary>
    [Fact]
    public async Task RewritingIdenticalStateDoesNotRepublishIt()
    {
        await OnUiThreadAsync(() =>
        {
            string directory = CreateTempDirectory();
            try
            {
                ConfigPaths paths = new(directory);
                JsonFileStore store = new(FileReplacer.Once, new TestLogger());
                AppState appState = Seeded();

                CurrentContextState state = new() { CurrentContextId = "work", LastSwitchCompletedAt = DateTimeOffset.UnixEpoch };
                store.WriteAsync(paths.StatePath, state).GetAwaiter().GetResult();
                appState.UpdateState(state);

                using StateFileWatcher watcher = new(appState, store, paths);
                watcher.Start();
                int reloads = 0;
                watcher.StateReloaded += (_, _) => reloads++;

                store.WriteAsync(paths.StatePath, state).GetAwaiter().GetResult();
                PumpUntil(Task.Delay(TimeSpan.FromSeconds(1)), TimeSpan.FromSeconds(5));

                Assert.Equal(0, reloads);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    private static AppState Seeded()
    {
        AppConfiguration configuration = new()
        {
            ActiveContextId = "work",
            Contexts =
            [
                new ContextDefinition { Id = "work", DisplayName = "Work" },
                new ContextDefinition { Id = "personal", DisplayName = "Personal" }
            ]
        };
        AppState appState = new();
        appState.UpdateConfiguration(configuration, new ConfigurationValidator().Validate(configuration));
        appState.UpdateState(new CurrentContextState { CurrentContextId = "work" });
        return appState;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 600 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cs-watch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
