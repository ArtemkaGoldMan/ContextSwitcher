namespace ContextSwitcher.Core.Security;

/// <summary>
/// The executables ContextSwitcher may run, which differ by platform: <see cref="MacOS"/> on a Mac,
/// chosen with the platform's services. <see cref="Abstractions.IProcessRunner"/> implementations
/// must reject anything outside it; a request to run something else indicates a bug, not an expected
/// automation failure.
/// </summary>
public sealed class CommandAllowlist
{
    private readonly HashSet<string> allowed;

    /// <summary>An allowlist of exactly <paramref name="executables"/>, matched by exact name.</summary>
    public CommandAllowlist(IEnumerable<string> executables)
    {
        ArgumentNullException.ThrowIfNull(executables);

        this.allowed = new HashSet<string>(executables, StringComparer.Ordinal);
    }

    /// <summary>What the app runs on a Mac.</summary>
    public static CommandAllowlist MacOS { get; } = new(
    [
        "osascript",
        "open",
        "docker",
        "shortcuts",

        // Built-in macOS image converter, used only to transcode an installed app's .icns bundle
        // icon into a cached PNG for the Profile Setup app picker. Read-only with respect to the
        // app bundle: it writes solely into ConfigPaths.IconCacheDirectory.
        "sips",

        // The updater (AppBundleUpdater): ditto unpacks a downloaded release, plutil reads its
        // version, and codesign checks it was signed with the same certificate as this copy.
        "ditto",
        "plutil",
        "codesign"
    ]);

    /// <summary>Whether <paramref name="fileName"/> may run.</summary>
    public bool IsAllowed(string fileName) => this.allowed.Contains(fileName);
}
