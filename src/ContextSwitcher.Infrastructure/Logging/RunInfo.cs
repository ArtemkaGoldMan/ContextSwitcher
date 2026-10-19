using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.Logging;

/// <summary>
/// Which copy of the app, on which version of the system, wrote a log: the first thing anyone reading
/// it needs, and the only thing a run that went well never has to say.
/// </summary>
/// <param name="SystemVersion">The system's version, as its settings show it: "26.6" on a Mac.</param>
public sealed record RunInfo(string AppName, string Version, string SystemVersion, string Architecture, string Build, int ProcessId)
{
    /// <summary>This run, for the app at <paramref name="version"/> on <paramref name="systemVersion"/>.</summary>
    public static RunInfo Current(string version, bool development, string systemVersion) => new(
        "ContextSwitcher",
        version,
        systemVersion,
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        development ? "development" : "release",
        Environment.ProcessId);
}
