namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Identifies what triggered a context switch request - written to the log with every switch, so
/// each place that switches has its own.
/// </summary>
public enum ContextSwitchSource
{
    /// <summary>The menu of the app's icon: in the menu bar on macOS, in the tray on Windows.</summary>
    TrayMenu,

    /// <summary>The popover's buttons.</summary>
    Dashboard,

    /// <summary>A profile clicked on the Profiles page.</summary>
    ProfilesPage,

    /// <summary>The setup wizard, switching into the first profile it made.</summary>
    SetupWizard,

    Cli,
    Shortcut,
    Test
}
