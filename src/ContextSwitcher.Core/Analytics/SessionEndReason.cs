namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Why a <see cref="ContextSession"/> ended.
/// </summary>
public enum SessionEndReason
{
    Switch,
    AppShutdown,
    RecoveredAfterCrash
}
