namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// What of the Start menu is not an app a profile could open and close: its uninstallers, its help
/// files and web links, its management consoles. Windows lists all of them next to the apps.
/// </summary>
/// <param name="LaunchableExtensions">The only kinds of file whose path starts an app, with the dot.</param>
/// <param name="UninstallerPrefixes">A file name starting with one of these is an uninstaller.</param>
/// <param name="ExcludedSchemes">Links opening these are a web page, not an app.</param>
public sealed record StartAppsFilter(
    IReadOnlyList<string> LaunchableExtensions,
    IReadOnlyList<string> UninstallerPrefixes,
    IReadOnlyList<string> ExcludedSchemes)
{
    private const string SchemeSeparator = "://";

    /// <summary>
    /// An executable is an app; a shortcut to a document or a <c>.msc</c> console is not. Other links
    /// (<c>steam://</c>, which starts a game) stay: they open an app.
    /// </summary>
    public static StartAppsFilter Default { get; } = new([".exe"], ["unins", "uninstall"], ["http", "https"]);

    /// <summary>Whether <paramref name="appId"/> is a path to a file, not an AppUserModelID or a link.</summary>
    public static bool IsPath(string appId)
    {
        ArgumentNullException.ThrowIfNull(appId);

        return !appId.Contains(SchemeSeparator, StringComparison.Ordinal) && appId.Contains('\\', StringComparison.Ordinal);
    }

    /// <summary>Whether <paramref name="app"/> is an app to offer.</summary>
    public bool Keeps(StartApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        int separator = app.AppId.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (separator > 0)
        {
            return !this.ExcludedSchemes.Contains(app.AppId[..separator], StringComparer.OrdinalIgnoreCase);
        }

        if (!IsPath(app.AppId))
        {
            return true;
        }

        string fileName = Path.GetFileName(app.AppId);
        return this.LaunchableExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase)
            && !this.UninstallerPrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
