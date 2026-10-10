namespace ContextSwitcher.Core.Contexts;

/// <summary>
/// Identifies what triggered a context switch request.
/// </summary>
public enum ContextSwitchSource
{
    MenuBar,
    Dashboard,
    Cli,
    Shortcut,
    StartupRecovery,
    Test
}
