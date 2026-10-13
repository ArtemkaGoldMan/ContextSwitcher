namespace ContextSwitcher.Infrastructure.Files;

/// <summary>
/// Whether this is a development build - the Debug build run from the code - or a release, built by
/// GitHub and installed. The two keep separate folders (<see cref="ConfigPaths"/>), so working on the
/// app never touches the profiles and the time of the copy in use, and both can run at once; a
/// development build says so in its name, so their two menu bar icons can be told apart.
/// </summary>
public static class AppBuild
{
    /// <summary>Whether this is a development (Debug) build.</summary>
    public static bool IsDevelopment { get; } =
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>The app's name as shown: "Context Switcher", or "Context Switcher DEV" for a development build.</summary>
    public static string DisplayName => IsDevelopment ? "Context Switcher DEV" : "Context Switcher";
}
