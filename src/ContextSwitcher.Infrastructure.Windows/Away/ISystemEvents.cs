using Microsoft.Win32;

namespace ContextSwitcher.Infrastructure.Windows.Away;

/// <summary>Windows' notices of sleep and of the session changing. Behind an interface so that what is made of them is tested without sleeping or locking anything.</summary>
public interface ISystemEvents
{
    /// <summary>The PC suspends or resumes.</summary>
    event PowerModeChangedEventHandler? PowerModeChanged;

    /// <summary>This session's screen locks or unlocks, or the session is connected or disconnected.</summary>
    event SessionSwitchEventHandler? SessionSwitch;
}
