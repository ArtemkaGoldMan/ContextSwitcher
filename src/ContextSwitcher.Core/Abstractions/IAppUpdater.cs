using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Replaces the installed app with a newer release: downloads it, checks it was signed with the same
/// certificate as the copy that is running, and swaps it in once this copy has quit.
/// </summary>
public interface IAppUpdater
{
    /// <summary>
    /// Why this copy cannot replace itself - it runs from source, say, or was not signed with the
    /// release certificate - or null when it can.
    /// </summary>
    Task<string?> GetInstallBlockerAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads, unpacks and verifies <paramref name="release"/>. Throws <see cref="UpdateException"/>
    /// when any of that fails, leaving nothing behind.
    /// </summary>
    Task<StagedUpdate> StageAsync(ReleaseInfo release, CancellationToken cancellationToken);

    /// <summary>
    /// Arranges for <paramref name="update"/> to take this copy's place and be opened as soon as this
    /// process exits. The caller quits the app next.
    /// </summary>
    void InstallOnExit(StagedUpdate update);
}
