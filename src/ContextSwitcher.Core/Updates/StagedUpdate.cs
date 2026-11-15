namespace ContextSwitcher.Core.Updates;

/// <summary>A downloaded, unpacked and verified copy of the app, ready to take the running one's place.</summary>
/// <param name="Version">The version it is.</param>
/// <param name="AppPath">What was downloaded: the unpacked <c>ContextSwitcher.app</c> on macOS, the package's file name on Windows.</param>
/// <param name="WorkDirectory">The temporary folder holding it, removed once it is installed; empty when the installer keeps it.</param>
public sealed record StagedUpdate(Version Version, string AppPath, string WorkDirectory)
{
    /// <summary>What the platform's updater needs again to install it; opaque to everything else.</summary>
    public object? Handle { get; init; }
}
