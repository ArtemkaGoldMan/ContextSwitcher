namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Identifies what triggered a context switch request.
/// </summary>
public enum ContextSwitchSource
{
    MenuBar,
    Dashboard,
    GlobalHotkey,
    Cli,
    Shortcut,
    StartupRecovery,
    Test
}
