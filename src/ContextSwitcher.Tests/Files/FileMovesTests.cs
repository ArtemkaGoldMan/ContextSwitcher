using System.Diagnostics;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.Windows;

namespace ContextSwitcher.Tests.Files;

/// <summary>
/// Saving settings, time and the log ends by moving a new file over the old one. Windows refuses that
/// while another process has the file open - the app and the command line at once, an antivirus, the
/// search indexer - so there a refused move is tried again for a moment before it fails.
/// </summary>
public sealed class FileMovesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
    private readonly string source;
    private readonly string destination;

    public FileMovesTests()
    {
        Directory.CreateDirectory(this.directory);
        this.source = Path.Combine(this.directory, "new.json");
        this.destination = Path.Combine(this.directory, "settings.json");
        File.WriteAllText(this.source, "new");
        File.WriteAllText(this.destination, "old");
    }

    public void Dispose() => Directory.Delete(this.directory, recursive: true);

    [Fact]
    public async Task TheNewFileTakesTheOldOnesPlace()
    {
        await FileMoves.ReplaceAsync(this.source, this.destination);

        Assert.Equal("new", await File.ReadAllTextAsync(this.destination));
        Assert.False(File.Exists(this.source));
    }

    /// <summary>A file that isn't there doesn't come back by waiting, so it fails at once.</summary>
    [Fact]
    public async Task AMissingFileIsNotWaitedFor()
    {
        Stopwatch watch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<FileNotFoundException>(() => FileMoves.ReplaceAsync(
            Path.Combine(this.directory, "missing.json"), this.destination, [TimeSpan.FromSeconds(5)], CancellationToken.None));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"It waited {watch.Elapsed} for a file that isn't there.");
    }

    /// <summary>Held open for a moment, as an antivirus does: the move waits, and is made once it lets go.</summary>
    [WindowsFact]
    public async Task OnWindowsAFileHeldOpenForAMomentIsReplacedOnceLetGo()
    {
        FileStream holder = new(this.destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        Task letGo = Task.Delay(TimeSpan.FromMilliseconds(300)).ContinueWith(_ => holder.Dispose(), TaskScheduler.Default);

        await FileMoves.ReplaceAsync(this.source, this.destination, Enumerable.Repeat(TimeSpan.FromMilliseconds(100), 20).ToList(), CancellationToken.None);
        await letGo;

        Assert.Equal("new", await File.ReadAllTextAsync(this.destination));
    }

    /// <summary>Held open past the last try, the move fails as it always did - and nothing is lost.</summary>
    [WindowsFact]
    public async Task OnWindowsAFileHeldOpenTooLongStillFails()
    {
        using (new FileStream(this.destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? failure = await Record.ExceptionAsync(() => FileMoves.ReplaceAsync(
                this.source, this.destination, [TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)], CancellationToken.None));

            Assert.True(failure is IOException or UnauthorizedAccessException, $"It failed with {failure?.GetType().Name ?? "nothing"}.");
        }

        Assert.Equal("old", await File.ReadAllTextAsync(this.destination));
        Assert.Equal("new", await File.ReadAllTextAsync(this.source));
    }
}
