namespace ContextSwitcher.App.Startup;

/// <summary>
/// Something the app keeps doing while it is up - a timer, a watcher. Each is registered as one, and
/// <see cref="AppHost.StartRuntimeServices"/> starts them all, once Avalonia is set up: they need its
/// dispatcher, and merely making a DispatcherTimer before then claims the UI dispatcher for the wrong
/// thread. The command line starts none of them.
/// </summary>
public interface IHousekeeping
{
    /// <summary>Starts it. Called once.</summary>
    void Start();
}
