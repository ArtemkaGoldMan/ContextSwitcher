namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// What tells an app's processes from the others on Windows: its package, for a Store app, or its
/// executable, for any other. An app the Start menu doesn't say either of has no identity, and so
/// nothing about it can be told or done.
/// </summary>
/// <param name="ExecutablePath">The full path of the app's executable, or its file name alone when that is all the settings say; null for a Store app.</param>
/// <param name="PackageFamilyName">The family of the app's package, such as <c>MSTeams_8wekyb3d8bbwe</c>; null for any other.</param>
public sealed record AppIdentity(string? ExecutablePath, string? PackageFamilyName)
{
    /// <summary>
    /// Whether <paramref name="process"/> is this app's. A Store app's processes carry its package. For
    /// the others the executable is the same, or has the same file name below the folder of the
    /// executable the Start menu names: apps that update themselves keep each version in a folder of its
    /// own (<c>Discord\app-1.0.9\Discord.exe</c> under <c>Discord\Discord.exe</c>). The file name must be
    /// the same, so that an app installed beside others - in a folder every program shares - doesn't
    /// take their processes for its own.
    /// </summary>
    public bool Matches(ProcessInfo process)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (this.PackageFamilyName is { } family)
        {
            return string.Equals(process.PackageFamilyName, family, StringComparison.OrdinalIgnoreCase);
        }

        if (this.ExecutablePath is not { } executable || process.ImagePath is not { } image)
        {
            return false;
        }

        if (string.Equals(image, executable, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A file name alone - what the settings may say, as "slack.exe" - is any program of that name.
        if (!Path.IsPathRooted(executable))
        {
            return string.Equals(Path.GetFileName(image), executable, StringComparison.OrdinalIgnoreCase);
        }

        string? folder = Path.GetDirectoryName(executable);
        return folder is not null
            && string.Equals(Path.GetFileName(image), Path.GetFileName(executable), StringComparison.OrdinalIgnoreCase)
            && image.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
