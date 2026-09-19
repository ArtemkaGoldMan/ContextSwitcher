using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.App;
using ContextSwitcher.Tests.Ui;

namespace ContextSwitcher.Tests.App.Startup;

/// <summary>
/// Needs a dispatcher, because the watcher debounces on one and marshals to it before touching
/// anything the UI owns.
/// </summary>
[Collection(AppHostTestCollection.Name)]
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
                JsonFileStore store = new();
                SeedAppHost();

                store.WriteAsync(paths.StatePath, new CurrentContextState { CurrentContextId = "work" })
                    .GetAwaiter().GetResult();

                using StateFileWatcher watcher = new(store, paths);
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
                Assert.Equal("personal", AppHost.State.CurrentContextId);
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
                JsonFileStore store = new();
                SeedAppHost();

                CurrentContextState state = new() { CurrentContextId = "work", LastSwitchCompletedAt = DateTimeOffset.UnixEpoch };
                store.WriteAsync(paths.StatePath, state).GetAwaiter().GetResult();
                AppHost.UpdateState(state);

                using StateFileWatcher watcher = new(store, paths);
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

    private static void SeedAppHost()
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
        AppHost.UpdateConfiguration(configuration, new ConfigurationValidator().Validate(configuration));
        AppHost.UpdateState(new CurrentContextState { CurrentContextId = "work" });
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
