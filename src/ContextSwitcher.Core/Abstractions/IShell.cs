namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// What the system opens for the app: a link, in the app the system has for it; a file, in the file
/// manager; and the system's own pages - a permission, Focus, Shortcuts.
/// </summary>
public interface IShell
{
    /// <summary>
    /// Opens a web page, or a <c>mailto:</c> link, in the app the system has for it. Says whether one
    /// took it: with no mail app, a mail link opens nothing.
    /// </summary>
    Task<bool> OpenLinkAsync(string url, CancellationToken cancellationToken);

    /// <summary>Shows a file selected in its folder - or, when it isn't there, the folder it would be in.</summary>
    void ShowFile(string path);

    /// <summary>Opens one of the system's own pages.</summary>
    void OpenSystemPage(SystemPage page);
}

/// <summary>The system's pages the app sends the person to.</summary>
public enum SystemPage
{
    /// <summary>Where the app is allowed to control the other apps, which switching needs.</summary>
    AutomationPermission,

    /// <summary>Focus's own settings, where a Focus filter is set.</summary>
    FocusSettings,

    /// <summary>The Shortcuts app, where the Focus shortcuts are made.</summary>
    Shortcuts
}
