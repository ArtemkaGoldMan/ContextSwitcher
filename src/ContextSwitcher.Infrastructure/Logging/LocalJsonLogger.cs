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

            FileStream stream = new(
                this.path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
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
            this.writeLock.Release();
        }
    }
}
