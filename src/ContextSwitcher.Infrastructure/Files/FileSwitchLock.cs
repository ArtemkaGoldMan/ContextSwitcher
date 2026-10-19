using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// Holds the switch lock as an exclusive hold on a file beside <c>state.json</c>. The system lets go
/// of it if the holder dies - on Unix <see cref="FileShare.None"/> is an advisory lock the kernel
/// drops when the process exits - so a crash cannot wedge it.
/// </summary>
public sealed class FileSwitchLock : ISwitchLock
{
    private readonly string lockPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSwitchLock"/> class.
    /// </summary>
    /// <param name="lockPath">The file held while a switch runs: <see cref="ConfigPaths.SwitchLockPath"/>.</param>
    public FileSwitchLock(string lockPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockPath);
        this.lockPath = lockPath;
    }

    /// <inheritdoc />
    public IDisposable? TryAcquire()
    {
        try
        {
            // A bare filename has an empty directory part, not a null one - Directory.CreateDirectory
            // throws on empty, so fall back to the working directory explicitly.
            string directory = Path.GetDirectoryName(this.lockPath) is { Length: > 0 } parent
                ? parent
                : Directory.GetCurrentDirectory();

            Directory.CreateDirectory(directory);

            return new FileStream(
                Path.Combine(directory, Path.GetFileName(this.lockPath)),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException)
        {
            // Not logged: held by another switch, which the switch that finds it says.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Can't create the lock file at all - a read-only config directory, say. Serialising
            // across processes is a safeguard, not a precondition, so let the switch proceed
            // rather than making the app unusable.
            return NothingHeld.Instance;
        }
    }

    /// <summary>What a switch holds when the lock file can't be made: nothing to give back.</summary>
    private sealed class NothingHeld : IDisposable
    {
        public static readonly NothingHeld Instance = new();

        public void Dispose()
        {
        }
    }
}
