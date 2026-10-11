namespace ContextSwitcher.Core.Messages;

/// <summary>Why a Focus Shortcut couldn't be offered. Mirrored in Strings.resx as <c>Msg_&lt;Key&gt;</c>.</summary>
public static class FocusMessages
{
    public static readonly MessageTemplate CustomFocus = new("Focus_CustomFocus", "“{0}” is a Focus you made yourself, so it has to be picked in the Shortcut by hand.");

    public static readonly MessageTemplate CouldNotPrepare = new("Focus_CouldNotPrepare", "Couldn't prepare the Shortcut.");

    public static readonly MessageTemplate CouldNotOpenShortcuts = new("Focus_CouldNotOpenShortcuts", "Couldn't open the Shortcuts app.");

    /// <summary>{0} is macOS's own reason.</summary>
    public static readonly MessageTemplate CouldNotPrepareBecause = new("Focus_CouldNotPrepareBecause", "Couldn't prepare the Shortcut: {0}");
}
