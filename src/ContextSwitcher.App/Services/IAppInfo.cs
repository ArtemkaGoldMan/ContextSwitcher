namespace ContextSwitcher.App.Services;

/// <summary>
/// Which app this is: its version, whether it is a development build, and its name as shown. The log,
/// the diagnostics, the update check and the menu bar icon all say it the same way.
/// </summary>
public interface IAppInfo
{
    /// <summary>This build's version, from the assembly - the same number the bundle and the release carry.</summary>
    Version Version { get; }

    /// <summary>
    /// Whether this is a development build - the Debug build run from the code - rather than a release,
    /// built by GitHub and installed. The two keep separate folders, so working on the app never touches
    /// the profiles and the time of the copy in use, and both can run at once.
    /// </summary>
    bool IsDevelopment { get; }

    /// <summary>"Context Switcher", or "Context Switcher DEV" for a development build, so their two menu bar icons can be told apart.</summary>
    string DisplayName { get; }
}
