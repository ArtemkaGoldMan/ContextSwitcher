namespace ContextSwitcher.Core.Security;

/// <summary>
/// The fixed set of executables ContextSwitcher is allowed to run. <see cref="Abstractions.IProcessRunner"/>
/// implementations must reject anything outside this list; a request to run something else indicates a bug,
/// not an expected automation failure.
/// </summary>
public static class CommandAllowlist
{
    private static readonly HashSet<string> AllowedExecutables = new(StringComparer.Ordinal)
    {
        "osascript",
        "open",
        "docker",
        "shortcuts"
    };

    /// <summary>
    /// Returns whether the given executable name is allowed to run.
    /// </summary>
    public static bool IsAllowed(string fileName) => AllowedExecutables.Contains(fileName);
}
