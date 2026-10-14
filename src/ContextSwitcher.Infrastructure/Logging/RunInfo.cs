using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ContextSwitcher.Infrastructure.Logging;

/// <summary>
/// Which copy of the app, on which Mac, wrote a log: the first thing anyone reading it needs, and the
/// only thing a run that went well never has to say.
/// </summary>
public sealed partial record RunInfo(string AppName, string Version, string MacOs, string Architecture, string Build, int ProcessId)
{
    /// <summary>This run, for the app at <paramref name="version"/>.</summary>
    public static RunInfo Current(string version, bool development) => new(
        "ContextSwitcher",
        version,
        ReadMacOsVersion(),
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        development ? "development" : "release",
        Environment.ProcessId);

    /// <summary>The macOS version ("26.6"), from the system's own file - no process to start for it.</summary>
    private static string ReadMacOsVersion()
    {
        try
        {
            string text = File.ReadAllText("/System/Library/CoreServices/SystemVersion.plist");
            if (VersionPattern().Match(text) is { Success: true } match)
            {
                return match.Groups[1].Value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not logged: not macOS, or not readable: fall through to what the runtime says.
        }

        return Environment.OSVersion.VersionString;
    }

    [GeneratedRegex(@"<key>ProductVersion</key>\s*<string>([^<]+)</string>")]
    private static partial Regex VersionPattern();
}
