using System.Diagnostics;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Tests.Windows;

namespace ContextSwitcher.Tests.Files;

/// <summary>
/// Saving settings, time and the log ends by moving a new file over the old one. Windows refuses that
/// while another process has the file open - the app and the command line at once, an antivirus, the
/// search indexer - so the replacer the app gives Windows tries again for a moment before it fails.
/// </summary>
public sealed class FileReplacerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
    private readonly string source;
    private readonly string destination;

    public FileReplacerTests()
    {
        Directory.CreateDirectory(this.directory);
        this.source = Path.Combine(this.directory, "new.json");
        this.destination = Path.Combine(this.directory, "settings.json");
        File.WriteAllText(this.source, "new");
        File.WriteAllText(this.destination, "old");
    }

    public void Dispose() => Directory.Delete(this.directory, recursive: true);

    public static TheoryData<string> Policies => new() { nameof(FileReplacer.Once), nameof(FileReplacer.RetryingWhileHeldOpen) };

    [Theory]
    [MemberData(nameof(Policies))]
    public async Task TheNewFileTakesTheOldOnesPlace(string policy)
    {
        await Policy(policy).ReplaceAsync(this.source, this.destination);

        Assert.Equal("new", await File.ReadAllTextAsync(this.destination));
        Assert.False(File.Exists(this.source));
    }

    /// <summary>A file that isn't there doesn't come back by waiting, so it fails at once.</summary>
    [Fact]
    public async Task AMissingFileIsNotWaitedFor()
    {
        FileReplacer patient = new([TimeSpan.FromSeconds(5)]);
        Stopwatch watch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<FileNotFoundException>(() => patient.ReplaceAsync(Path.Combine(this.directory, "missing.json"), this.destination));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"It waited {watch.Elapsed} for a file that isn't there.");
    }

    /// <summary>Held open for a moment, as an antivirus does: the move waits, and is made once it lets go.</summary>
    [WindowsFact]
    public async Task OnWindowsAFileHeldOpenForAMomentIsReplacedOnceLetGo()
    {
        FileReplacer patient = new(Enumerable.Repeat(TimeSpan.FromMilliseconds(100), 20).ToList());
        FileStream holder = new(this.destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        Task letGo = Task.Delay(TimeSpan.FromMilliseconds(300)).ContinueWith(_ => holder.Dispose(), TaskScheduler.Default);

        await patient.ReplaceAsync(this.source, this.destination);
        await letGo;

        Assert.Equal("new", await File.ReadAllTextAsync(this.destination));
    }

    /// <summary>Held open past the last try, the move fails as it always did - and nothing is lost.</summary>
    [WindowsFact]
    public async Task OnWindowsAFileHeldOpenTooLongStillFails()
    {
        FileReplacer brief = new([TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)]);

        using (new FileStream(this.destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? failure = await Record.ExceptionAsync(() => brief.ReplaceAsync(this.source, this.destination));

            Assert.True(failure is IOException or UnauthorizedAccessException, $"It failed with {failure?.GetType().Name ?? "nothing"}.");
        }

        Assert.Equal("old", await File.ReadAllTextAsync(this.destination));
        Assert.Equal("new", await File.ReadAllTextAsync(this.source));
    }

    private static FileReplacer Policy(string name) =>
        name == nameof(FileReplacer.Once) ? FileReplacer.Once : FileReplacer.RetryingWhileHeldOpen;
}
