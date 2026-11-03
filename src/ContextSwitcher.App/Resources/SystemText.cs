using ContextSwitcher.Core.Platform;

namespace ContextSwitcher.App.Resources;

/// <summary>
/// The words for the computer and its parts, where the Mac's and Windows' differ: "your Mac" or "your
/// PC", the menu bar or the tray, Finder or File Explorer, ⌘V or Ctrl+V. Each is a pair of
/// <see cref="Strings"/> - the Mac's, and the same key with <c>_Windows</c> - chosen by what the platform
/// says its <see cref="PlatformCapabilities.Wording"/> is, so a view binds to one name and each system
/// reads its own. A string that is the same on both has one key and is not here.
/// </summary>
public sealed class SystemText(PlatformCapabilities capabilities)
{
    private bool Windows => capabilities.Wording == SystemWording.Windows;

    /// <summary>The welcome's line: what one click does to the computer.</summary>
    public string OneClickFlips => this.Windows ? Strings.Onboarding_OneClickFlipsYourMac_Windows : Strings.Onboarding_OneClickFlipsYourMac;

    /// <summary>The welcome's list: where to switch from.</summary>
    public string SwitchAnytime => this.Windows ? Strings.Onboarding_SwitchAnytimeFromTheMenu_Windows : Strings.Onboarding_SwitchAnytimeFromTheMenu;

    /// <summary>The welcome's list: what Profile Setup adds.</summary>
    public string AddInProfileSetup => this.Windows ? Strings.Onboarding_AddBrowserTabsFocusAnd_Windows : Strings.Onboarding_AddBrowserTabsFocusAnd;

    /// <summary>Whether the welcome says how to keep the icon in sight: on Windows, which hides a new icon behind an arrow.</summary>
    public bool HasPinTheIcon => this.Windows;

    /// <summary>The welcome's list: how to keep the icon in sight; empty where it needs no keeping.</summary>
    public string PinTheIcon => this.Windows ? Strings.Onboarding_PinTheIcon_Windows : string.Empty;

    /// <summary>The theme and language choice that follows the system.</summary>
    public string MatchSystem => this.Windows ? Strings.Settings_Theme_MatchMacOS_Windows : Strings.Settings_Theme_MatchMacOS;

    /// <summary>What following the system's theme means.</summary>
    public string MatchSystemTheme => this.Windows ? Strings.Settings_MatchMacOSFollowsTheSystems_Windows : Strings.Settings_MatchMacOSFollowsTheSystems;

    /// <summary>What opening at login does.</summary>
    public string OpenAtLoginDescription => this.Windows ? Strings.Settings_OpenAtLogin_Description_Windows : Strings.Settings_OpenAtLogin_Description;

    /// <summary>Opening at login is turned on but the system has it off.</summary>
    public string OpenAtLoginNeedsApproval => this.Windows ? Strings.Settings_OpenAtLogin_NeedsApproval_Windows : Strings.Settings_OpenAtLogin_NeedsApproval;

    /// <summary>The system didn't allow opening at login; <c>{0}</c> is why.</summary>
    public string OpenAtLoginFailed => this.Windows ? Strings.Settings_OpenAtLogin_Failed_Windows : Strings.Settings_OpenAtLogin_Failed;

    /// <summary>The link to the system page where an app is allowed to open at login.</summary>
    public string OpenLoginItems => this.Windows ? Strings.Settings_OpenAtLogin_OpenLoginItems_Windows : Strings.Settings_OpenAtLogin_OpenLoginItems;

    /// <summary>What exporting settings is for.</summary>
    public string TransferDescription => this.Windows ? Strings.Settings_Transfer_Description_Windows : Strings.Settings_Transfer_Description;

    /// <summary>How to see the app in a language just chosen; <c>{0}</c> is the language.</summary>
    public string LanguageRestartHint => this.Windows ? Strings.Settings_Language_RestartHint_Windows : Strings.Settings_Language_RestartHint;

    /// <summary>What the diagnostics hold.</summary>
    public string DiagnosticsDescription => this.Windows ? Strings.Settings_Diagnostics_Description_Windows : Strings.Settings_Diagnostics_Description;

    /// <summary>The button that shows the log in the system's file manager.</summary>
    public string ShowLog => this.Windows ? Strings.Settings_ShowLog_Windows : Strings.Settings_ShowLog;

    /// <summary>What is said once the mail app is open and the diagnostics are copied.</summary>
    public string EmailOpened => this.Windows ? Strings.Contact_EmailOpened_Windows : Strings.Contact_EmailOpened;

    /// <summary>What recording time is for, and that it stays on the computer.</summary>
    public string RecordsHowLongYouSpend => this.Windows ? Strings.Settings_RecordsHowLongYouSpend_Windows : Strings.Settings_RecordsHowLongYouSpend;

    /// <summary>What a profile's quick links are, in the dashboard.</summary>
    public string QuickLinksShown => this.Windows ? Strings.ProfileSetup_ShownInTheMenuBar_Windows : Strings.ProfileSetup_ShownInTheMenuBar;

    /// <summary>What a profile's reminders are, in the dashboard.</summary>
    public string RemindersShown => this.Windows ? Strings.ProfileSetup_RemindersShownInTheMenu_Windows : Strings.ProfileSetup_RemindersShownInTheMenu;
}
