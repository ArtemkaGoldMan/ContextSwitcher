using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Finds the things on this Mac a profile can refer to besides apps - Shortcuts, Docker containers,
/// browser profiles, Music playlists, open tabs - so Profile Setup can offer them as choices. A
/// typed name that is slightly wrong fails only at switch time, far from where it was typed; a
/// chosen one cannot be wrong.
///
/// Every member degrades instead of throwing: the caller falls back to letting the user type.
/// </summary>
public interface ISystemCatalog
{
    /// <summary>The names of the user's Shortcuts, or an empty list when they cannot be read.</summary>
    Task<IReadOnlyList<string>> GetShortcutNamesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every Docker container, running or not, or <see langword="null"/> when Docker cannot be
    /// reached - not installed, or the daemon is not running.
    /// </summary>
    Task<IReadOnlyList<string>?> GetDockerContainersAsync(CancellationToken cancellationToken);

    /// <summary>The profiles of a Chromium browser, or an empty list when it has none on this Mac.</summary>
    IReadOnlyList<BrowserProfile> GetBrowserProfiles(BrowserKind browser);

    /// <summary>
    /// Apple Music's playlists, or <see langword="null"/> when they could not be read. Reading them
    /// means scripting Music, which starts it if it is not running, so that only happens when
    /// <paramref name="startMusicIfNeeded"/> says the user asked for it.
    /// </summary>
    Task<IReadOnlyList<string>?> GetMusicPlaylistsAsync(bool startMusicIfNeeded, CancellationToken cancellationToken);

    /// <summary>
    /// The http(s) tabs open in <paramref name="browser"/>, or in every supported browser that is
    /// running for <see cref="BrowserKind.Default"/>. A browser that is not running is not started.
    /// </summary>
    Task<IReadOnlyList<OpenTab>> GetOpenTabsAsync(BrowserKind browser, CancellationToken cancellationToken);
}
