using System.Text;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Serialization;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Infrastructure.Logging;

/// <summary>
/// Appends structured log entries to the local <c>app.log.jsonl</c> file, one compact JSON object per
/// line. This is the file itself; what is worth writing to it is decided by <see cref="TrailLogger"/>.
/// </summary>
public sealed class LocalJsonLogger : ILogger
{
    /// <summary>
    /// Roll the log at this size, whatever its age. With only problems written to it a log stays a few
    /// kilobytes; this is for the day something repeats without end.
    /// </summary>
    public const long MaxLogBytes = 1024 * 1024;

    /// <summary>
    /// A log starts again from nothing after this long, and the one it replaces is kept for as long
    /// again: a problem noticed on Monday that happened on Sunday is still in it.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    private const int LockAttempts = 500;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(2);

    private readonly string path;
    private readonly IFileReplacer fileReplacer;
    private readonly IClock clock;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalJsonLogger"/> class.
    /// </summary>
    /// <param name="configPaths">Resolves the local application log path.</param>
    /// <param name="fileReplacer">Moves a full or week-old log aside, over the previous one.</param>
    /// <param name="clock">The clock a log's age is read against.</param>
    public LocalJsonLogger(ConfigPaths configPaths, IFileReplacer fileReplacer, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(configPaths);
        ArgumentNullException.ThrowIfNull(fileReplacer);
        ArgumentNullException.ThrowIfNull(clock);
        this.path = configPaths.AppLogPath;
        this.fileReplacer = fileReplacer;
        this.clock = clock;
    }

    /// <inheritdoc />
    public Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return this.AppendAsync([entry], cancellationToken);
    }

    /// <summary>Appends <paramref name="entries"/> in order, under one hold of the file.</summary>
    public async Task AppendAsync(IReadOnlyList<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return;
        }

        StringBuilder text = new();
        foreach (LogEntry entry in entries)
        {
            text.Append(JsonSerializer.Serialize(entry, ContextSwitcherJson.CompactOptions)).Append(Environment.NewLine);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());

        await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(this.path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // The semaphore above only guards this process. macOS Shortcuts, Siri and
            // the CLI each trigger a switch in its own process, and two of those appending at the
            // same moment landed their writes at the same offset - one silently overwrote the other,
            // leaving no exception and no malformed line to show for it. The lock file makes "one
            // writer at a time" true across processes; the OS releases it if a holder dies.
            FileStream? processLock = await this.TryAcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await this.RollIfDueAsync(cancellationToken).ConfigureAwait(false);

                FileStream stream = new(
                    this.path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    useAsync: true);

                await using (stream.ConfigureAwait(false))
                {
                    await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                processLock?.Dispose();
            }
        }
        finally
        {
            this.writeLock.Release();
        }
    }

    /// <summary>
    /// Starts the log over if its week is up, and lets go of the previous one once it is a week old
    /// too. Done when the app starts, so a log never holds more than the last week or two even when
    /// nothing has been written to it for a month.
    /// </summary>
    public async Task TidyAsync(CancellationToken cancellationToken = default)
    {
        await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            FileStream? processLock = await this.TryAcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await this.RollIfDueAsync(cancellationToken).ConfigureAwait(false);

                string previous = this.path + ".1";
                if (File.Exists(previous) && this.clock.UtcNow - File.GetLastWriteTimeUtc(previous) >= MaxAge)
                {
                    File.Delete(previous);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not logged: a log that cannot be tidied is still a log.
            }
            finally
            {
                processLock?.Dispose();
            }
        }
        finally
        {
            this.writeLock.Release();
        }
    }

    /// <summary>
    /// Moves the log aside once it is a week old or passes <see cref="MaxLogBytes"/>, keeping a single
    /// previous generation. Runs under the cross-process lock the caller already holds, so two
    /// processes cannot roll it at the same time. Failures are swallowed deliberately: losing the roll
    /// is a far better outcome than a switch that fails because its log could not be tidied.
    /// </summary>
    private async Task RollIfDueAsync(CancellationToken cancellationToken)
    {
        try
        {
            FileInfo current = new(this.path);
            if (!current.Exists)
            {
                return;
            }

            bool tooBig = current.Length >= MaxLogBytes;
            bool tooOld = this.clock.UtcNow - File.GetCreationTimeUtc(this.path) >= MaxAge;
            if (!tooBig && !tooOld)
            {
                return;
            }

            string previous = this.path + ".1";
            await this.fileReplacer.ReplaceAsync(this.path, previous, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not logged: keep appending to the oversized file rather than dropping the entry.
        }
    }

    /// <summary>
    /// Takes an exclusive lock on a file beside the log, retrying while another process holds it -
    /// a writer only holds it for the length of one append, so contention clears quickly. Returns
    /// <see langword="null"/> when the lock cannot be taken at all, in which case the caller appends
    /// unguarded: serialising is a safeguard, and a dropped diagnostic line is a better outcome than
    /// a switch that fails because its log file was busy.
    /// </summary>
    private async Task<FileStream?> TryAcquireCrossProcessLockAsync(CancellationToken cancellationToken)
    {
        string lockPath = this.path + ".lock";

        for (int attempt = 0; attempt < LockAttempts; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                // Held by another writer - wait for it to finish its append and try again.
                await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                // Not logged: the lock file cannot be created at all - a read-only config directory, say.
                return null;
            }
        }

        return null;
    }
}
