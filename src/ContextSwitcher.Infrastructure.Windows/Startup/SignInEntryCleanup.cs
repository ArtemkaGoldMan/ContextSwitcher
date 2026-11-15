using System.Security;

namespace ContextSwitcher.Infrastructure.Windows.Startup;

/// <summary>What the app takes with it when it is uninstalled.</summary>
public static class SignInEntryCleanup
{
    /// <summary>
    /// Takes the app out of what opens at sign-in: the entry names a path that is about to go, and Windows
    /// would go on trying it. What the person set up - profiles, settings, time - is in the settings
    /// folder, which an uninstall leaves.
    /// </summary>
    public static void Remove(IStartupEntries entries, LoginItemOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            entries.RemoveCommand(options.ValueName);
            entries.RemoveApproval(options.ValueName);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Not logged: this runs as the uninstaller starts, before the log is there to write to, and an
            // entry left behind only fails to open something that is no longer installed.
        }
    }
}