namespace ContextSwitcher.Core.Analytics;

/// <summary>
/// Why a <see cref="ContextSession"/> ended.
/// </summary>
public enum SessionEndReason
{
    Switch,
    AppShutdown,
    RecoveredAfterCrash,

    /// <summary>The Mac went to sleep; a new session starts when it wakes.</summary>
    Sleep,

    /// <summary>The screen was locked, or another user's session came to the front.</summary>
    Away
}
