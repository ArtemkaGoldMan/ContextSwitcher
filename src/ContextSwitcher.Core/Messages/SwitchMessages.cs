namespace ContextSwitcher.Core.Messages;

/// <summary>
/// What a switch reports about a step that didn't fully work. Each is also in the app's Strings.resx as
/// <c>Msg_&lt;Key&gt;</c>, with the same English - a test keeps the two in step.
/// </summary>
public static class SwitchMessages
{
    public static readonly MessageTemplate CouldNotClose = new("Switch_CouldNotClose", "Could not close: {0}.");

    public static readonly MessageTemplate CouldNotLaunch = new("Switch_CouldNotLaunch", "Could not launch: {0}.");

    public static readonly MessageTemplate CouldNotStartContainers = new("Switch_CouldNotStartContainers", "Could not start containers: {0}.");

    public static readonly MessageTemplate CouldNotStopContainers = new("Switch_CouldNotStopContainers", "Could not stop containers: {0}.");

    public static readonly MessageTemplate NoPlaylist = new("Switch_NoPlaylist", "No playlist configured.");

    public static readonly MessageTemplate CouldNotPlayAppleMusic = new("Switch_CouldNotPlayAppleMusic", "Could not play Apple Music playlist '{0}'.");

    public static readonly MessageTemplate CouldNotPlaySpotify = new("Switch_CouldNotPlaySpotify", "Could not start Spotify playback for '{0}'.");

    public static readonly MessageTemplate ShortcutMissing = new(
        "Switch_ShortcutMissing",
        "Shortcut '{0}' doesn't exist yet. In Context Switcher, edit the profile and click \"Create it\" under Focus, or see docs/shortcuts-integration.md.");

    /// <summary>{1} is macOS's own reason, in the Mac's language.</summary>
    public static readonly MessageTemplate ShortcutFailed = new("Switch_ShortcutFailed", "Shortcut '{0}' ran but failed: {1}");

    public static readonly MessageTemplate ShortcutFailedWithExitCode = new("Switch_ShortcutFailedWithExitCode", "Shortcut '{0}' ran but failed: exit code {1}");

    public static readonly MessageTemplate StepTimedOut = new("Switch_StepTimedOut", "Step timed out after {0} seconds.");

    public static readonly MessageTemplate CouldNotCheckTabs = new(
        "Switch_CouldNotCheckTabs",
        "Could not check {0} for existing tabs, so URLs were opened without duplicate checking. Check Automation permissions.");

    public static readonly MessageTemplate CouldNotCloseTabs = new(
        "Switch_CouldNotCloseTabs",
        "Could not close the profile's tabs in {0}. Check Automation permissions.");

    /// <summary>{1} is the browser's or macOS's own reason.</summary>
    public static readonly MessageTemplate CouldNotOpenUrl = new("Switch_CouldNotOpenUrl", "Could not open '{0}': {1}");

    public static readonly MessageTemplate CouldNotActivateTabGroup = new(
        "Switch_CouldNotActivateTabGroup",
        "Could not activate tab group '{0}'. Browser tab group scripting may not be supported on this macOS version.");

    /// <summary>{0} is the browser, {1} the profile folder, {2} the browser's own reason.</summary>
    public static readonly MessageTemplate CouldNotLaunchBrowserProfile = new("Switch_CouldNotLaunchBrowserProfile", "Could not launch {0} profile '{1}': {2}");
}
