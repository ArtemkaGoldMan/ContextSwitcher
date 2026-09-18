using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using System.Collections.Concurrent;
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

    /// <summary>
    /// UpdateConfiguration raises ConfigurationChanged inline, and its subscribers rebuild view
    /// models that construct Avalonia objects - which belong to the thread that made them. Awaiting
    /// the writes with ConfigureAwait(false) published from a thread-pool thread instead of the
    /// caller's, so brushes built there crashed the next render with "the calling thread cannot
    /// access this object because a different thread owns it".
    /// </summary>
    [Fact]
    public async Task SaveAsyncPublishesOnTheCallingContextRatherThanAThreadPoolThread()
    {
        using SingleThreadContext context = new();
        InMemoryJsonStore inner = new();
        ConfigPaths configPaths = new("/tmp/context-switcher-tests");
        inner.Seed(configPaths.SettingsPath, new AppConfiguration());

        // The in-memory store returns completed tasks, so an await on it never yields and the
        // continuation stays on the calling thread whatever ConfigureAwait says. Real file IO does
        // yield, so make this one yield too or the test cannot see the bug at all.
        YieldingJsonStore jsonStore = new(inner);
        ConfigurationStore store = new(jsonStore, configPaths, new ConfigurationValidator());
        AppHost.UpdateConfiguration(new AppConfiguration(), new ConfigurationValidationResult([]));

        int publishedOn = 0;
        void Handler(object? sender, EventArgs e) => publishedOn = Environment.CurrentManagedThreadId;
        AppHost.ConfigurationChanged += Handler;

        try
        {
            await context.RunAsync(() => store.SaveAsync(ValidConfiguration, CancellationToken.None));
        }
        finally
        {
            AppHost.ConfigurationChanged -= Handler;
        }

        Assert.Equal(context.ThreadId, publishedOn);
    }

    /// <summary>
    /// A single-threaded synchronization context standing in for the UI thread: work posted to it
    /// runs on its one thread, so a continuation that captured it resumes there.
    /// </summary>
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();
        private readonly Thread thread;

        public SingleThreadContext()
        {
            this.thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach ((SendOrPostCallback callback, object? state) in this.queue.GetConsumingEnumerable())
                {
                    callback(state);
                }
            })
            { IsBackground = true };

            this.thread.Start();
            this.ThreadId = this.thread.ManagedThreadId;
        }

        public int ThreadId { get; }

        public override void Post(SendOrPostCallback d, object? state) => this.queue.Add((d, state));

        public async Task RunAsync(Func<Task> work)
        {
            TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.Post(
                async _ =>
                {
                    try
                    {
                        await work();
                        done.SetResult();
                    }
                    catch (Exception ex)
                    {
                        done.SetException(ex);
                    }
                },
                null);

            await done.Task;
        }

        public void Dispose() => this.queue.CompleteAdding();
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
