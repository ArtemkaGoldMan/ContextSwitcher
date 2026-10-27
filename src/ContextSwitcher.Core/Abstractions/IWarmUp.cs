namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Something a platform reads once as the app starts, so that a question asked later - one that
/// can't wait for a slow answer, such as the apps open for a window that must not stall - already has it.
/// </summary>
public interface IWarmUp
{
    /// <summary>Reads it. Called once, in the background; what it finds is kept by whatever asked for it.</summary>
    Task WarmUpAsync(CancellationToken cancellationToken);
}
