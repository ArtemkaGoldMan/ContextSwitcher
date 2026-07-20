namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// A read-only row in the Settings page's cross-profile hotkey list (agent.md section 11.1.2).
/// Hotkeys are edited per-profile, from Profile Setup; this is an at-a-glance view of all of them.
/// </summary>
public sealed class HotkeyRowViewModel(string contextDisplayName, string accelerator, bool enabled)
{
    public string ContextDisplayName { get; } = contextDisplayName;

    public string Accelerator { get; } = accelerator;

    public bool Enabled { get; } = enabled;
}
