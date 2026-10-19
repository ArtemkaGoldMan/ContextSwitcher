using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Browser;

/// <summary>
/// The tabs a profile opened outlive the run of the app that opened them - it is often left by a
/// later run, or by the CLI - so they go through the real file, one entry per profile.
/// </summary>
public sealed class OpenedTabsStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cs-opened-tabs-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EachProfilesTabsAreKeptUntilForgotten()
    {
        ConfigPaths paths = new(this.directory);
        Directory.CreateDirectory(this.directory);
        OpenedTabsStore store = new(new JsonFileStore(FileReplacer.Once, new TestLogger()), paths);

        await store.SetAsync("work", new OpenedTabs(BrowserKind.Chrome, "123@2026-10-13T20:00:00.0000000Z", [7, 8]), CancellationToken.None);
        await store.SetAsync("personal", new OpenedTabs(BrowserKind.Brave, "456@2026-10-13T20:00:00.0000000Z", [3]), CancellationToken.None);

        OpenedTabs? work = await new OpenedTabsStore(new JsonFileStore(FileReplacer.Once, new TestLogger()), paths).GetAsync("work", CancellationToken.None);
        Assert.NotNull(work);
        Assert.Equal((BrowserKind.Chrome, "123@2026-10-13T20:00:00.0000000Z"), (work.Browser, work.BrowserRun));
        Assert.Equal([7, 8], work.TabIds);

        await store.RemoveAsync("work", CancellationToken.None);

        Assert.Null(await store.GetAsync("work", CancellationToken.None));
        Assert.Equal([3], (await store.GetAsync("personal", CancellationToken.None))!.TabIds);
        Assert.True(File.Exists(paths.OpenedTabsPath));
    }

    [Fact]
    public async Task NothingRememberedYetIsNothing()
    {
        OpenedTabsStore store = new(new JsonFileStore(FileReplacer.Once, new TestLogger()), new ConfigPaths(this.directory));

        Assert.Null(await store.GetAsync("work", CancellationToken.None));
        await store.RemoveAsync("work", CancellationToken.None);
        Assert.False(File.Exists(new ConfigPaths(this.directory).OpenedTabsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }
    }
}
