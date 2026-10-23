using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.NotYet;

/// <summary>
/// Finding an app's processes and windows on Windows comes with the apps part of stage 1; until then
/// nothing can be told, and nothing is ended.
/// </summary>
public sealed class NotYetAppProcesses : IAppProcesses
{
    /// <inheritdoc />
    public bool? IsRunning(string app) => null;

    /// <inheritdoc />
    public bool? HasWindowOnScreen(string app) => null;

    /// <inheritdoc />
    public bool ForceQuit(string app) => false;
}
