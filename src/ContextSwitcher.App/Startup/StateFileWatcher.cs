using ContextSwitcher.Core.Logging;
using Avalonia.Threading;
using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Reloads <c>state.json</c> when another process rewrites it, so the running app notices switches
/// it did not perform itself.
///
/// Shortcuts, Siri and the command line each switch in a process of their own, and
/// the app's state was only ever updated in-process. A switch from any of those left the
/// menu bar popover showing the previous profile and a stale elapsed timer until the app was
/// restarted - the switch itself worked, the UI just never heard about it.
/// </summary>
public sealed class StateFileWatcher : IHousekeeping, IDisposable
{
    private readonly IAppState appState;
    private readonly IJsonStore jsonStore;
    private readonly ConfigPaths configPaths;
    private FileSystemWatcher? watcher;
    private DispatcherTimer? debounce;

    public StateFileWatcher(IAppState appState, IJsonStore jsonStore, ConfigPaths configPaths)
    {
        ArgumentNullException.ThrowIfNull(appState);
        ArgumentNullException.ThrowIfNull(jsonStore);
        ArgumentNullException.ThrowIfNull(configPaths);

        this.appState = appState;
        this.jsonStore = jsonStore;
        this.configPaths = configPaths;
    }

    /// <summary>Raised after <see cref="IAppState.State"/> has been refreshed from disk.</summary>
    public event EventHandler? StateReloaded;

    /// <summary>Starts watching <c>state.json</c>.</summary>
    public void Start()
    {
        // A write arrives as several events, and the file is briefly absent while it is replaced
        // atomically, so settle before reading rather than racing the writer.
        this.debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        this.debounce.Tick += this.OnDebounceElapsed;

        try
        {
            this.watcher = new FileSystemWatcher(this.configPaths.BaseDirectory, Path.GetFileName(this.configPaths.StatePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            this.watcher.Changed += this.OnFileChanged;
            this.watcher.Created += this.OnFileChanged;
            this.watcher.Renamed += this.OnFileChanged;
            this.watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Watching is an improvement, not a precondition - without it the app behaves exactly
            // as it did before, which is to say it misses out-of-process switches.
            this.watcher = null;
        }
    }

    /// <summary>Stops watching; nothing to do when it never started.</summary>
    public void Dispose()
    {
        if (this.debounce is { } debounce)
        {
            debounce.Stop();
            debounce.Tick -= this.OnDebounceElapsed;
        }

        this.watcher?.Dispose();
    }

    // FileSystemWatcher raises on a background thread; everything downstream of UpdateState rebuilds
    // view models and the Avalonia objects they hold, which belong to the thread that built them.
    private void OnFileChanged(object sender, FileSystemEventArgs e) =>
        Dispatcher.UIThread.Post(this.RestartDebounce);

    private void RestartDebounce()
    {
        this.debounce?.Stop();
        this.debounce?.Start();
    }

    private void OnDebounceElapsed(object? sender, EventArgs e)
    {
        this.debounce?.Stop();
        _ = this.ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        CurrentContextState? reloaded;
        try
        {
            reloaded = await this.jsonStore.ReadAsync<CurrentContextState>(this.configPaths.StatePath)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("State", "StateNotReloaded", "state.json could not be read after another process changed it, so the app may show an old state.", ex);
            return;
        }

        if (reloaded is null || !HasChanged(this.appState.State, reloaded))
        {
            // Our own writes come back through the watcher too; ignoring identical state keeps this
            // from looping on them.
            return;
        }

        this.appState.UpdateState(reloaded);
        this.StateReloaded?.Invoke(this, EventArgs.Empty);
    }

    private static bool HasChanged(CurrentContextState current, CurrentContextState reloaded) =>
        current.CurrentContextId != reloaded.CurrentContextId
        || current.LastSwitchCompletedAt != reloaded.LastSwitchCompletedAt;
}
