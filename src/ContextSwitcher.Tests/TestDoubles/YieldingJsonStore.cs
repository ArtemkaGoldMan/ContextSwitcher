using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>
/// Wraps a store so every call completes on a thread-pool thread, the way real file IO does.
///
/// Two traps this exists to avoid, both of which produce a test that passes just as happily with a
/// thread-hopping bug present as without it:
///
/// - The plain in-memory double returns already-completed tasks, so awaiting it never suspends and
///   the continuation stays on the calling thread whatever ConfigureAwait says.
/// - <c>Task.Yield</c> is not enough either. ConfigureAwait(false) does not move work to the pool,
///   it only declines to marshal back; since Yield returns to the captured context, the continuation
///   still ends up on the caller's thread. Completing off-context is what makes the difference
///   observable.
/// </summary>
public sealed class YieldingJsonStore(IJsonStore inner) : IJsonStore
{
    public async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class
    {
        await OffContextAsync();
        return await inner.ReadAsync<T>(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken = default)
        where T : class
    {
        await OffContextAsync();
        await inner.WriteAsync(path, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task BackupAsync(string path, string backupDirectory, CancellationToken cancellationToken = default)
    {
        await OffContextAsync();
        await inner.BackupAsync(path, backupDirectory, cancellationToken).ConfigureAwait(false);
    }

    private static async Task OffContextAsync() => await Task.Delay(1).ConfigureAwait(false);
}
