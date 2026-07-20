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
    /// timestamped name. No-ops if the source file does not exist yet.
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

        string timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
        string backupPath = Path.Combine(backupDirectory, $"{Path.GetFileNameWithoutExtension(path)}.{timestamp}{Path.GetExtension(path)}");
        File.Copy(path, backupPath, overwrite: true);

        return Task.CompletedTask;
    }

    private static void QuarantineCorruptFile(string path)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        string corruptPath = $"{path}.corrupt.{timestamp}";
        File.Move(path, corruptPath, overwrite: false);
    }
}
