using Avalonia.Threading;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.App.Startup;

/// <summary>
/// Reloads <c>state.json</c> when another process rewrites it, so the running app notices switches
/// it did not perform itself.
///
/// Section 10 has Shortcuts, Siri and the CLI each switching in their own process, and
/// <see cref="AppHost.State"/> was only ever updated in-process. A switch from any of those left the
/// menu bar popover showing the previous profile and a stale elapsed timer until the app was
/// restarted - the switch itself worked, the UI just never heard about it. Global hotkeys were
/// unaffected, since those fire inside this process.
/// </summary>
public sealed class StateFileWatcher : IDisposable
{
    private readonly IJsonStore jsonStore;
    private readonly ConfigPaths configPaths;
    private readonly FileSystemWatcher? watcher;
    private readonly DispatcherTimer debounce;

    public StateFileWatcher(IJsonStore jsonStore, ConfigPaths configPaths)
    {
        ArgumentNullException.ThrowIfNull(jsonStore);
        ArgumentNullException.ThrowIfNull(configPaths);

        this.jsonStore = jsonStore;
        this.configPaths = configPaths;

        // A write arrives as several events, and the file is briefly absent while it is replaced
        // atomically, so settle before reading rather than racing the writer.
        this.debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        this.debounce.Tick += this.OnDebounceElapsed;

        try
        {
            this.watcher = new FileSystemWatcher(configPaths.BaseDirectory, Path.GetFileName(configPaths.StatePath))
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

    /// <summary>Raised after <see cref="AppHost.State"/> has been refreshed from disk.</summary>
    public event EventHandler? StateReloaded;

    public void Dispose()
    {
        this.debounce.Stop();
        this.debounce.Tick -= this.OnDebounceElapsed;
        this.watcher?.Dispose();
    }

    // FileSystemWatcher raises on a background thread; everything downstream of UpdateState rebuilds
    // view models and the Avalonia objects they hold, which belong to the thread that built them.
    private void OnFileChanged(object sender, FileSystemEventArgs e) =>
        Dispatcher.UIThread.Post(this.RestartDebounce);

    private void RestartDebounce()
    {
        this.debounce.Stop();
        this.debounce.Start();
    }

    private void OnDebounceElapsed(object? sender, EventArgs e)
    {
        this.debounce.Stop();
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
            return;
        }

        if (reloaded is null || !HasChanged(AppHost.State, reloaded))
        {
            // Our own writes come back through the watcher too; ignoring identical state keeps this
            // from looping on them.
            return;
        }

        AppHost.UpdateState(reloaded);
        this.StateReloaded?.Invoke(this, EventArgs.Empty);
    }

    private static bool HasChanged(CurrentContextState current, CurrentContextState reloaded) =>
        current.CurrentContextId != reloaded.CurrentContextId
        || current.LastSwitchCompletedAt != reloaded.LastSwitchCompletedAt;
}
