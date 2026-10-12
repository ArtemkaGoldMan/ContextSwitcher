namespace ContextSwitcher.Core.Messages;

/// <summary>What the configuration check says about a setting that isn't valid. Mirrored in Strings.resx as <c>Msg_&lt;Key&gt;</c>.</summary>
public static class ConfigurationMessages
{
    /// <summary>{0} is the file's schema version, {1} the newest this build reads.</summary>
    public static readonly MessageTemplate SchemaTooNew = new(
        "Config_SchemaTooNew",
        "This configuration is version {0}, written by a newer version of ContextSwitcher; this build understands up to {1}. Update the app.");

    public static readonly MessageTemplate SchemaUnsupported = new("Config_SchemaUnsupported", "Unsupported configuration schema version.");

    public static readonly MessageTemplate NoContexts = new("Config_NoContexts", "At least one context is required.");

    public static readonly MessageTemplate IdNotUrlSafe = new("Config_IdNotUrlSafe", "Context id must be lowercase and URL-safe.");

    public static readonly MessageTemplate IdNotUnique = new("Config_IdNotUnique", "Context id must be unique.");

    public static readonly MessageTemplate DisplayNameRequired = new("Config_DisplayNameRequired", "Context display name is required.");

    public static readonly MessageTemplate AccentColorInvalid = new("Config_AccentColorInvalid", "Accent color must be a 6-digit hex color.");

    public static readonly MessageTemplate ActiveContextMissing = new("Config_ActiveContextMissing", "Active context must reference an existing context.");

    public static readonly MessageTemplate UrlModeNeedsUrl = new("Config_UrlModeNeedsUrl", "URL mode requires at least one URL.");

    public static readonly MessageTemplate BrowserModeInvalid = new("Config_BrowserModeInvalid", "Browser management mode is invalid.");

    public static readonly MessageTemplate BrowserInvalid = new("Config_BrowserInvalid", "Browser must be Default, Chrome, Brave, or Safari.");

    public static readonly MessageTemplate CloseTabsNeedsBrowser = new(
        "Config_CloseTabsNeedsBrowser",
        "Closing tabs needs Chrome, Brave, or Safari: the default browser can't be asked which tabs are open.");

    public static readonly MessageTemplate TabGroupsNeedName = new("Config_TabGroupsNeedName", "Tab groups mode requires at least one tab group name.");

    public static readonly MessageTemplate TabGroupNameEmpty = new("Config_TabGroupNameEmpty", "Tab group name cannot be empty.");

    public static readonly MessageTemplate ProfilesNeedProfile = new("Config_ProfilesNeedProfile", "Profiles mode requires at least one browser profile.");

    public static readonly MessageTemplate ProfileBrowserInvalid = new("Config_ProfileBrowserInvalid", "Profile browser must be Chrome or Brave.");

    public static readonly MessageTemplate ProfileDirectoryRequired = new("Config_ProfileDirectoryRequired", "Profile directory is required.");

    public static readonly MessageTemplate QuickLinkUrlInvalid = new("Config_QuickLinkUrlInvalid", "Quick link URL must use http or https.");

    public static readonly MessageTemplate UrlInvalid = new("Config_UrlInvalid", "URL must use http or https.");
}
