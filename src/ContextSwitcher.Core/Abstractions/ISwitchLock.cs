namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Keeps two processes from switching at once - the app and the command line, or two command lines
/// started by Shortcuts or Siri - so their steps never run side by side and their state writes never
/// interleave. Within one process the switch service has a lock of its own; this is the one between
/// processes, and how it is held is the platform's business.
/// </summary>
public interface ISwitchLock
{
    /// <summary>
    /// Takes the lock if no other process holds it.
    /// </summary>
    /// <returns>What gives the lock back when disposed, or <see langword="null"/> when another process holds it.</returns>
    IDisposable? TryAcquire();
}
