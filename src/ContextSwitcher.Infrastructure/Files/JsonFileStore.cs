using System.Globalization;
using System.Text;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Serialization;

namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// Persists JSON data to disk with atomic writes and quarantines malformed files.
/// </summary>
public sealed class JsonFileStore : IJsonStore
{
    /// <summary>
    /// How many timestamped copies of any one file to keep. Enough to recover from a bad edit
    /// several saves ago, without the directory growing without limit.
    /// </summary>
    private const int MaxBackupsPerFile = 10;

    /// <summary>
    /// Reads a JSON file and returns the deserialized value.
    /// </summary>
    /// <typeparam name="T">The CLR type to deserialize into.</typeparam>
    /// <param name="path">The path to the JSON file.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The deserialized value, or <see langword="null"/> when the file does not exist.</returns>
    public async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<T>(
                    stream,
                    ContextSwitcherJson.Options,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            QuarantineCorruptFile(path);
            return null;
        }
    }

    /// <summary>
    /// Writes a JSON value to disk using a temporary file and rename operation.
    /// </summary>
    /// <typeparam name="T">The CLR type to serialize.</typeparam>
    /// <param name="path">The target file path.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    public async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = Path.Combine(
            directory ?? Directory.GetCurrentDirectory(),
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            FileStream stream = new(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    value,
                    ContextSwitcherJson.Options,
                    cancellationToken).ConfigureAwait(false);

                byte[] newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                await stream.WriteAsync(newline, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Copies the file at <paramref name="path"/> into <paramref name="backupDirectory"/> with a
    /// timestamped name, then prunes older copies of that same file down to
    /// <see cref="MaxBackupsPerFile"/>. No-ops if the source file does not exist yet.
    /// </summary>
    public Task BackupAsync(string path, string backupDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);

        if (!File.Exists(path))
        {
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(backupDirectory);

        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        // The name used second resolution, so two saves inside one second silently overwrote each
        // other. Milliseconds alone are not enough either - a tight loop still collides - so a short
        // random suffix makes the name unique outright. The timestamp stays first, which is what
        // keeps ordinal name order equal to age order for pruning.
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss-fffZ", CultureInfo.InvariantCulture);
        string unique = Guid.NewGuid().ToString("N")[..8];
        File.Copy(path, Path.Combine(backupDirectory, $"{stem}.{timestamp}.{unique}{extension}"), overwrite: false);

        PruneBackups(backupDirectory, stem, extension);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Keeps the newest <see cref="MaxBackupsPerFile"/> backups of one file and deletes the rest.
    /// Nothing pruned these before, so the directory grew by one file per save, forever.
    /// </summary>
    private static void PruneBackups(string backupDirectory, string stem, string extension)
    {
        try
        {
            // The timestamp format sorts chronologically as text, so ordinal name order is age order.
            List<string> backups = [.. Directory
                .EnumerateFiles(backupDirectory, $"{stem}.*{extension}")
                .OrderByDescending(file => file, StringComparer.Ordinal)];

            foreach (string stale in backups.Skip(MaxBackupsPerFile))
            {
                File.Delete(stale);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Housekeeping must never fail the save that triggered it. A backup that outlives its
            // welcome is harmless; losing the write is not.
        }
    }

    private static void QuarantineCorruptFile(string path)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        string corruptPath = $"{path}.corrupt.{timestamp}";
        File.Move(path, corruptPath, overwrite: false);
    }
}
