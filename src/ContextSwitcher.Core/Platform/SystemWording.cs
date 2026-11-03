namespace ContextSwitcher.Core.Platform;

/// <summary>Whose words the interface uses for the computer it runs on, where the two differ.</summary>
public enum SystemWording
{
    /// <summary>The Mac: the menu bar, Finder, System Settings, ⌘.</summary>
    Mac,

    /// <summary>Windows: the tray, File Explorer, Settings, Ctrl.</summary>
    Windows
}
