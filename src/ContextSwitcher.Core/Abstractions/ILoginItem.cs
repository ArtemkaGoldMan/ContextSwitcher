namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Whether the app opens when the person logs in to their Mac. macOS keeps the answer - it lists
/// the app under System Settings → General → Login Items, where it can be turned off as well - so
/// it is read from there each time rather than kept in the settings file.
/// </summary>
public interface ILoginItem
{
    LoginItemStatus Status { get; }

    /// <summary>Turns opening at login on or off. Returns null when done, or macOS's reason when not.</summary>
    string? SetEnabled(bool enabled);

    /// <summary>Opens System Settings at Login Items, where an app waiting for approval is allowed.</summary>
    void OpenSystemSettings();
}

/// <summary>Where opening at login stands.</summary>
public enum LoginItemStatus
{
    /// <summary>Not offered: the app isn't running as an installed app bundle, as when run from source.</summary>
    Unavailable,

    Off,

    On,

    /// <summary>Turned on, but macOS waits for it to be allowed in System Settings.</summary>
    NeedsApproval
}
