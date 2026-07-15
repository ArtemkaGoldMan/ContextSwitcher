using System.Text;
using System.Text.Json;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Serialization;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Infrastructure.Analytics;

/// <summary>
/// Persists context sessions to <c>analytics.jsonl</c> (append-only, one compact JSON object per
/// line) and the <c>analytics.current.json</c> active-session marker used for crash recovery.
/// </summary>
public sealed class AnalyticsSessionStore : IAnalyticsSessionStore
{
    private readonly ConfigPaths configPaths;
    private readonly IJsonStore jsonStore;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsSessionStore"/> class.
    /// </summary>
    public AnalyticsSessionStore(ConfigPaths configPaths, IJsonStore jsonStore)
    {
        ArgumentNullException.ThrowIfNull(configPaths);
        ArgumentNullException.ThrowIfNull(jsonStore);

        this.configPaths = configPaths;
        this.jsonStore = jsonStore;
    }

    /// <inheritdoc />
    public async Task AppendCompletedSessionAsync(ContextSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        string line = JsonSerializer.Serialize(session, ContextSwitcherJson.CompactOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);

        await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string path = this.configPaths.AnalyticsLogPath;
            EnsureDirectoryExists(path);

            FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 4096, useAsync: true);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextSession>> ReadAllAsync(CancellationToken cancellationToken)
    {
        string path = this.configPaths.AnalyticsLogPath;
        if (!File.Exists(path))
        {
            return [];
        }

        string[] lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        List<ContextSession> sessions = [];

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                ContextSession? session = JsonSerializer.Deserialize<ContextSession>(line, ContextSwitcherJson.CompactOptions);
                if (session is not null)
                {
                    sessions.Add(session);
                }
            }
            catch (JsonException)
            {
                // Skip malformed lines rather than failing the whole read.
            }
        }

        return sessions;
    }

    /// <inheritdoc />
    public async Task ReplaceAllAsync(IReadOnlyList<ContextSession> sessions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        string path = this.configPaths.AnalyticsLogPath;
        EnsureDirectoryExists(path);

        string tempPath = Path.Combine(
            Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory(),
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                await using (stream.ConfigureAwait(false))
                {
                    foreach (ContextSession session in sessions)
                    {
                        string line = JsonSerializer.Serialize(session, ContextSwitcherJson.CompactOptions);
                        byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    }

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
        finally
        {
            this.writeLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<ContextSession?> ReadActiveMarkerAsync(CancellationToken cancellationToken)
    {
        return this.jsonStore.ReadAsync<ContextSession>(this.configPaths.AnalyticsMarkerPath, cancellationToken);
    }

    /// <inheritdoc />
    public Task WriteActiveMarkerAsync(ContextSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return this.jsonStore.WriteAsync(this.configPaths.AnalyticsMarkerPath, session, cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearActiveMarkerAsync(CancellationToken cancellationToken)
    {
        string path = this.configPaths.AnalyticsMarkerPath;
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static void EnsureDirectoryExists(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
