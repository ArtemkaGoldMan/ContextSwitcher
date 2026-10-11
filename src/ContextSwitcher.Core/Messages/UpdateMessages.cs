namespace ContextSwitcher.Core.Messages;

/// <summary>Why an update couldn't be checked for or installed. Mirrored in Strings.resx as <c>Msg_&lt;Key&gt;</c>.</summary>
public static class UpdateMessages
{
    public static readonly MessageTemplate GitHubError = new("Update_GitHubError", "GitHub answered with an error ({0}). Try again later.");

    public static readonly MessageTemplate GitHubUnreachable = new("Update_GitHubUnreachable", "Couldn't reach GitHub. Check the internet connection.");

    public static readonly MessageTemplate GitHubTimedOut = new("Update_GitHubTimedOut", "GitHub didn't answer in time. Try again later.");

    public static readonly MessageTemplate GitHubUnreadable = new("Update_GitHubUnreadable", "GitHub's answer couldn't be read.");

    public static readonly MessageTemplate TagNotAVersion = new("Update_TagNotAVersion", "The latest release is tagged \"{0}\", which isn't a version number.");

    /// <summary>{0} is the version, {1} the archive's file name.</summary>
    public static readonly MessageTemplate NoArchive = new("Update_NoArchive", "Version {0} has no {1} to install from. Download it from its release page instead.");

    public static readonly MessageTemplate CouldNotUnpack = new("Update_CouldNotUnpack", "The download couldn't be unpacked into the app. Try again, or download it from the release page.");

    public static readonly MessageTemplate WrongVersion = new("Update_WrongVersion", "The download is version {0}, not {1}, so it wasn't installed.");

    public static readonly MessageTemplate NotSigned = new("Update_NotSigned", "The download isn't signed with Context Switcher's certificate, so it wasn't installed.");

    public static readonly MessageTemplate NotInstalledApp = new("Update_NotInstalledApp", "This copy isn't an installed app, so there is nothing to replace.");

    public static readonly MessageTemplate CouldNotStart = new("Update_CouldNotStart", "The update couldn't be started.");

    public static readonly MessageTemplate DownloadError = new("Update_DownloadError", "The download failed ({0}). Try again later.");

    public static readonly MessageTemplate DownloadTooLarge = new("Update_DownloadTooLarge", "The download is far larger than a release should be, so it wasn't installed.");

    public static readonly MessageTemplate DownloadFailed = new("Update_DownloadFailed", "The download failed. Check the internet connection and try again.");

    public static readonly MessageTemplate NoVersion = new("Update_NoVersion", "The download has no version number, so it wasn't installed.");

    public static readonly MessageTemplate RunsFromSource = new("Update_RunsFromSource", "This copy runs from source rather than as an installed app, so it can't update itself.");

    public static readonly MessageTemplate CannotReplaceIn = new("Update_CannotReplaceIn", "Context Switcher isn't allowed to replace itself in {0}. Download the new version from its release page.");

    public static readonly MessageTemplate NotReleaseSigned = new(
        "Update_NotReleaseSigned",
        "This copy isn't signed with the release certificate, so it can't check that an update is genuine. Download the new version from its release page.");
}
