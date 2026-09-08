using System.Text;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Core.Serialization;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Infrastructure.Logging;

/// <summary>
/// Appends structured log entries to the local <c>app.log.jsonl</c> file, one compact JSON object per line.
/// </summary>
public sealed class LocalJsonLogger : ILogger
{
    private const int LockAttempts = 500;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(2);

    private readonly string path;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalJsonLogger"/> class.
    /// </summary>
    /// <param name="configPaths">Resolves the local application log path.</param>
    public LocalJsonLogger(ConfigPaths configPaths)
    {
        ArgumentNullException.ThrowIfNull(configPaths);
        this.path = configPaths.AppLogPath;
    }

    /// <inheritdoc />
    public async Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string line = JsonSerializer.Serialize(entry, ContextSwitcherJson.CompactOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);

        await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(this.path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // The semaphore above only guards this process. Section 10 has macOS Shortcuts, Siri and
            // the CLI each triggering a switch in its own process, and two of those appending at the
            // same moment landed their writes at the same offset - one silently overwrote the other,
            // leaving no exception and no malformed line to show for it. The lock file makes "one
            // writer at a time" true across processes; the OS releases it if a holder dies.
            FileStream? processLock = await this.TryAcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);
            try
            {
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
                // The lock file cannot be created at all - a read-only config directory, say.
                return null;
            }
        }

        return null;
    }
}
