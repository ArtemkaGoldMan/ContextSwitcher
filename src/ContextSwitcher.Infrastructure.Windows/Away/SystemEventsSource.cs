using Microsoft.Win32;

namespace ContextSwitcher.Infrastructure.Windows.Away;

/// <summary>
/// <see cref="SystemEvents"/>: .NET's hidden window that Windows' broadcasts reach, on a thread of its
/// own, which needs no window of the app's. Subscribing starts it, and the last one to unsubscribe lets
/// it go.
/// </summary>
public sealed class SystemEventsSource : ISystemEvents
{
    /// <inheritdoc />
    public event PowerModeChangedEventHandler? PowerModeChanged
    {
        add => SystemEvents.PowerModeChanged += value;
        remove => SystemEvents.PowerModeChanged -= value;
    }

    /// <inheritdoc />
    public event SessionSwitchEventHandler? SessionSwitch
    {
        add => SystemEvents.SessionSwitch += value;
        remove => SystemEvents.SessionSwitch -= value;
    }
}
