using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Logging;

/// <summary>
/// Which copy of the app, on which version of the system, wrote a log: the first thing anyone reading
/// it needs, and the only thing a run that went well never has to say.
/// </summary>
/// <param name="SystemName">The system's name: "macOS".</param>
/// <param name="SystemVersion">The system's version, as its settings show it: "26.6" on a Mac.</param>
public sealed record RunInfo(string AppName, string Version, string SystemName, string SystemVersion, string Architecture, string Build, int ProcessId)
{
    /// <summary>This run, for the app at <paramref name="version"/> on <paramref name="system"/>.</summary>
    public static RunInfo Current(string version, bool development, ISystemVersion system) => new(
        "ContextSwitcher",
        version,
        system.Name,
        system.Read(),
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        development ? "development" : "release",
        Environment.ProcessId);
}
