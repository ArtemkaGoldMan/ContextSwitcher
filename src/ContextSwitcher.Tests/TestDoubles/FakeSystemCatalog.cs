using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Catalog;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Tests.TestDoubles;

/// <summary>
/// Everything empty and Docker unavailable unless a test says otherwise - the state of a Mac with
/// nothing set up, which is the one every picker has to degrade gracefully on.
/// </summary>
public sealed class FakeSystemCatalog : ISystemCatalog
{
    public List<string> ShortcutNames { get; } = [];

    /// <summary>Null means Docker could not be reached.</summary>
    public List<string>? DockerContainers { get; set; }

    public Dictionary<BrowserKind, List<BrowserProfile>> BrowserProfiles { get; } = [];

    /// <summary>The playlists Music has; null makes it answer as though it could not be read.</summary>
    public List<string>? MusicPlaylists { get; set; } = [];

    /// <summary>When false, reading playlists without starting Music returns null, as it does when Music is closed.</summary>
    public bool IsMusicRunning { get; set; }

    public List<OpenTab> OpenTabs { get; } = [];

    public List<BrowserKind> OpenTabRequests { get; } = [];

    public List<bool> MusicRequests { get; } = [];

    /// <summary>
    /// Answers from a thread-pool thread, the way the real catalog does after running a process or
    /// a script - so anything that resumes off the UI thread shows up, as it would in the app.
    /// </summary>
    public bool CompletesOffThread { get; set; }

    public int ShortcutReads { get; private set; }

    public int DockerReads { get; private set; }

    public async Task<IReadOnlyList<string>> GetShortcutNamesAsync(CancellationToken cancellationToken)
    {
        this.ShortcutReads++;
        await this.MaybeLeaveTheCallersThread().ConfigureAwait(false);
        return this.ShortcutNames.ToList();
    }

    public async Task<IReadOnlyList<string>?> GetDockerContainersAsync(CancellationToken cancellationToken)
    {
        this.DockerReads++;
        await this.MaybeLeaveTheCallersThread().ConfigureAwait(false);
        return this.DockerContainers?.ToList();
    }

    public IReadOnlyList<BrowserProfile> GetBrowserProfiles(BrowserKind browser) =>
        this.BrowserProfiles.TryGetValue(browser, out List<BrowserProfile>? profiles) ? profiles.ToList() : [];

    public async Task<IReadOnlyList<string>?> GetMusicPlaylistsAsync(bool startMusicIfNeeded, CancellationToken cancellationToken)
    {
        this.MusicRequests.Add(startMusicIfNeeded);
        if (startMusicIfNeeded)
        {
            this.IsMusicRunning = true;
        }

        await this.MaybeLeaveTheCallersThread().ConfigureAwait(false);
        return this.IsMusicRunning ? this.MusicPlaylists?.ToList() : null;
    }

    public async Task<IReadOnlyList<OpenTab>> GetOpenTabsAsync(BrowserKind browser, CancellationToken cancellationToken)
    {
        this.OpenTabRequests.Add(browser);
        await this.MaybeLeaveTheCallersThread().ConfigureAwait(false);
        return this.OpenTabs.ToList();
    }

    // Task.Yield is not enough: it resumes on the caller's context. A real delay completes on the
    // thread pool, like a process exiting.
    private Task MaybeLeaveTheCallersThread() => this.CompletesOffThread ? Task.Delay(1) : Task.CompletedTask;
}
