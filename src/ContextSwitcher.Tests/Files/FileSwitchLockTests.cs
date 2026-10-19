using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Tests.Files;

/// <summary>
/// The switch lock between processes, held as a file beside state.json. Shortcuts, Siri and the
/// command line each switch in a process of their own; while one holds the file, no other gets it.
/// </summary>
public sealed class FileSwitchLockTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cs-lock-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }
    }

    [Fact]
    public void WhileOneHoldsTheLockNoOtherGetsIt()
    {
        string lockPath = Path.Combine(this.directory, "state.json.lock");
        FileSwitchLock first = new(lockPath);
        FileSwitchLock second = new(lockPath);

        using (IDisposable? held = first.TryAcquire())
        {
            Assert.NotNull(held);
            Assert.Null(second.TryAcquire());
        }

        using IDisposable? next = second.TryAcquire();
        Assert.NotNull(next);
    }

    /// <summary>The folder is made the first time, as a fresh install has none.</summary>
    [Fact]
    public void TheLocksFolderIsMadeIfItIsMissing()
    {
        string lockPath = Path.Combine(this.directory, "nested", "state.json.lock");

        using IDisposable? held = new FileSwitchLock(lockPath).TryAcquire();

        Assert.NotNull(held);
        Assert.True(File.Exists(lockPath));
    }

    [Fact]
    public void TheLockIsBesideStateJsonUnderItsOldName()
    {
        ConfigPaths paths = new(this.directory);

        Assert.Equal(paths.StatePath + ".lock", paths.SwitchLockPath);
    }
}
