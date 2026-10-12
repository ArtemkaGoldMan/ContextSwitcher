namespace ContextSwitcher.Core.Abstractions;

/// <summary>Notifications in macOS's Notification Center, and clicks on them.</summary>
public interface ISystemNotifications
{
    /// <summary>
    /// Raised with a notification's id when the person clicks it. It can come on any thread, and
    /// for a notification shown before the app was last restarted.
    /// </summary>
    event EventHandler<string>? Clicked;

    /// <summary>
    /// Shows a notification, replacing any earlier one with the same <paramref name="id"/>. The first
    /// one asks macOS for permission. Where that is refused, or there is no app bundle to notify as -
    /// running from source - nothing is shown.
    /// </summary>
    void Show(string id, string title, string body);

    /// <summary>Takes the notification with <paramref name="id"/> out of Notification Center, if it is there.</summary>
    void Remove(string id);
}
