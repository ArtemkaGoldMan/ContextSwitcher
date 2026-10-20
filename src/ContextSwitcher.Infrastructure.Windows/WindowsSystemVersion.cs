using System.Globalization;
using ContextSwitcher.Core.Abstractions;
using Microsoft.Win32;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// The Windows version as Settings → System → About shows it - "11 25H2 (26200.6584)" - from the
/// registry, which is where About reads it too. Windows 11 still calls itself 10.0, so the build tells
/// them apart: 11 starts at build 22000.
/// </summary>
public sealed class WindowsSystemVersion : ISystemVersion
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    /// <inheritdoc />
    public string Name => "Windows";

    /// <inheritdoc />
    public string Read()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
        return Describe(
            key?.GetValue("CurrentBuild") as string,
            key?.GetValue("UBR") as int?,
            key?.GetValue("DisplayVersion") as string)
            ?? Environment.OSVersion.Version.ToString();
    }

    /// <summary>The version from what the registry holds; null without a build number to go on.</summary>
    public static string? Describe(string? build, int? revision, string? displayVersion)
    {
        if (!int.TryParse(build, NumberStyles.None, CultureInfo.InvariantCulture, out int buildNumber))
        {
            return null;
        }

        string release = buildNumber >= 22000 ? "11" : "10";
        string named = string.IsNullOrWhiteSpace(displayVersion) ? release : $"{release} {displayVersion}";
        string full = revision is { } update ? $"{buildNumber}.{update}" : $"{buildNumber}";
        return $"{named} ({full})";
    }
}
