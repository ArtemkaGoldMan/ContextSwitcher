using System.Text.RegularExpressions;
using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>The macOS version ("26.6"), from the system's own file - no process to start for it.</summary>
public sealed partial class MacSystemVersion : ISystemVersion
{
    /// <inheritdoc />
    public string Read()
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
