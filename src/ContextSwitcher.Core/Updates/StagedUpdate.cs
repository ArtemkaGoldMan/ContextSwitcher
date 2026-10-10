namespace ContextSwitcher.Core.Updates;

/// <summary>A downloaded, unpacked and verified copy of the app, ready to take the running one's place.</summary>
/// <param name="Version">The version it is.</param>
/// <param name="AppPath">The unpacked <c>ContextSwitcher.app</c>.</param>
/// <param name="WorkDirectory">The temporary folder holding it, removed once it is installed.</param>
public sealed record StagedUpdate(Version Version, string AppPath, string WorkDirectory);
