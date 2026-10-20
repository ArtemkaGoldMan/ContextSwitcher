using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// What Profile Setup offers to pick from - browser profiles and tabs in stage 2, Docker's containers
/// in stage 3 - comes with each; until then there is nothing to pick. There are no Shortcuts or Music
/// playlists on Windows at all.
/// </summary>
public sealed class NotYetSystemCatalog : ISystemCatalog
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetShortcutNamesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetDockerContainersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>?>(null);

    /// <inheritdoc />
    public IReadOnlyList<BrowserProfile> GetBrowserProfiles(BrowserKind browser) => [];

    /// <inheritdoc />
    public bool IsBrowserInstalled(BrowserKind browser) => false;

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetMusicPlaylistsAsync(bool startMusicIfNeeded, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>?>(null);

    /// <inheritdoc />
    public Task<IReadOnlyList<OpenTab>> GetOpenTabsAsync(BrowserKind browser, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OpenTab>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<OpenBrowserTab>> GetBrowserTabsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OpenBrowserTab>>([]);
}
