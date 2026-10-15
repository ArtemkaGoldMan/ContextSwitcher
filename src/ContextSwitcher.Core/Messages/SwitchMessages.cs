using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.Core.Messages;

/// <summary>
/// What a switch reports about a step that didn't fully work. Each is also in the app's Strings.resx as
/// <c>Msg_&lt;Key&gt;</c>, with the same English - a test keeps the two in step.
/// </summary>
public static class SwitchMessages
{
    /// <summary>
    /// {0} is an app still running after being asked to quit, with a window open - or one whose
    /// windows could not be seen, so a question to save cannot be ruled out.
    /// </summary>
    public static readonly MessageTemplate CouldNotClose = new(
        "Switch_CouldNotClose",
        "Didn't quit: {0}. It may be asking whether to save changes.");

    /// <summary>
    /// {0} is an app still running after being asked to quit with no window left: some
    /// apps take half a minute to finish, and the switch does not wait for them. The line goes once
    /// it has quit (<see cref="Contexts.ClosingAppsFollowUp"/>).
    /// </summary>
    public static readonly MessageTemplate StillClosing = new(
        "Switch_StillClosing",
        "Still closing: {0} - its windows are closed. This line goes once it has quit.");

    /// <summary>{0} is an app that closed its windows when asked to quit, and minutes later still runs.</summary>
    public static readonly MessageTemplate StayedInBackground = new(
        "Switch_StayedInBackground",
        "{0} closed its windows but kept running in the background.");

    /// <summary>{0} is an app the person asked to force quit that was still running after.</summary>
    public static readonly MessageTemplate CouldNotForceQuit = new("Switch_CouldNotForceQuit", "Could not force {0} to quit.");

    /// <summary>{0} is the apps macOS found no app for.</summary>
    public static readonly MessageTemplate AppsNotInstalled = new("Switch_AppsNotInstalled", "Not installed, so not opened: {0}.");

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

    /// <summary>{0}, {1} and {2}: how many tabs were closed of how many, and in which browser; {3} the pages still open.</summary>
    public static readonly MessageTemplate SomeTabsStayedOpen = new(
        "Switch_SomeTabsStayedOpen",
        "Closed {0} of {1} tabs in {2}; still open: {3}.");

    /// <summary>The same when the pages still open could not be looked up.</summary>
    public static readonly MessageTemplate SomeTabsNotClosed = new("Switch_SomeTabsNotClosed", "Closed {0} of {1} tabs in {2}.");

    public static readonly MessageTemplate TimedOutClosingApps = new("Switch_TimedOutClosingApps", "Closing apps didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutOpeningApps = new("Switch_TimedOutOpeningApps", "Opening apps didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutHidingApps = new("Switch_TimedOutHidingApps", "Hiding apps didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutBrowserPages = new("Switch_TimedOutBrowserPages", "Opening the browser's pages didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutClosingTabs = new("Switch_TimedOutClosingTabs", "Closing the browser's tabs didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutFocus = new("Switch_TimedOutFocus", "Changing Focus didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutMedia = new("Switch_TimedOutMedia", "Starting the music didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutStartingDocker = new("Switch_TimedOutStartingDocker", "Starting Docker containers didn't finish in {0} seconds.");

    public static readonly MessageTemplate TimedOutStoppingDocker = new("Switch_TimedOutStoppingDocker", "Stopping Docker containers didn't finish in {0} seconds.");

    /// <summary>
    /// A step that ran out of time, saying which: "Step timed out" left the reader guessing which of
    /// them it was. A step with nothing more specific to say keeps the general message.
    /// </summary>
    public static UserMessage TimedOut(AutomationStepType step, string seconds) => step switch
    {
        AutomationStepType.CloseApplications => TimedOutClosingApps.With(seconds),
        AutomationStepType.LaunchApplications => TimedOutOpeningApps.With(seconds),
        AutomationStepType.HideApplications => TimedOutHidingApps.With(seconds),
        AutomationStepType.ManageBrowserContext => TimedOutBrowserPages.With(seconds),
        AutomationStepType.CloseBrowserTabs => TimedOutClosingTabs.With(seconds),
        AutomationStepType.SetFocusMode => TimedOutFocus.With(seconds),
        AutomationStepType.ControlMedia => TimedOutMedia.With(seconds),
        AutomationStepType.StartDockerResources => TimedOutStartingDocker.With(seconds),
        AutomationStepType.StopDockerResources => TimedOutStoppingDocker.With(seconds),
        _ => StepTimedOut.With(seconds)
    };

    /// <summary>macOS has not let Context Switcher send Apple events to the browser: Automation is off for it, or was refused.</summary>
    public static readonly MessageTemplate BrowserNotAllowed = new(
        "Switch_BrowserNotAllowed",
        "macOS isn't letting Context Switcher control {0}, so its tabs weren't touched. Turn it on under Context Switcher in System Settings → Privacy & Security → Automation.");

    /// <summary>The browser was asked and did not reply within the step's time.</summary>
    public static readonly MessageTemplate BrowserNotAnswering = new(
        "Switch_BrowserNotAnswering",
        "{0} didn't answer in time, so its tabs weren't touched.");

    /// <summary>{1} is the browser's or macOS's own reason.</summary>
    public static readonly MessageTemplate CouldNotControlBrowser = new(
        "Switch_CouldNotControlBrowser",
        "Could not control {0}: {1}");

    public static readonly MessageTemplate CloseTabsNeedsBrowser = new(
        "Switch_CloseTabsNeedsBrowser",
        "The profile's tabs weren't closed: that needs Chrome, Brave or Safari as the browser.");

    public static readonly MessageTemplate CouldNotCloseTabs = new(
        "Switch_CouldNotCloseTabs",
        "Could not close the profile's tabs in {0}. Check Automation permissions.");

    /// <summary>{1} is the browser's or macOS's own reason.</summary>
    public static readonly MessageTemplate CouldNotOpenUrl = new("Switch_CouldNotOpenUrl", "Could not open '{0}': {1}");

    /// <summary>{0} is the browser, {1} the profile folder, {2} the browser's own reason.</summary>
    public static readonly MessageTemplate CouldNotLaunchBrowserProfile = new("Switch_CouldNotLaunchBrowserProfile", "Could not launch {0} profile '{1}': {2}");
}
