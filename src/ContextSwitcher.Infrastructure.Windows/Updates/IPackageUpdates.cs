using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Infrastructure.Windows.Updates;

/// <summary>
/// What the installer's own library does for an update: finds the release's package, downloads it, and
/// has it put in place once the app quits. Behind an interface so what <see cref="WindowsAppUpdater"/>
/// decides can be tested without an installed app or a download.
/// </summary>
public interface IPackageUpdates
{
    /// <summary>Whether this copy was put where it is by the installer, which is what an update replaces.</summary>
    bool IsInstalled { get; }

    /// <summary>
    /// The package the release at <paramref name="releaseFolderUrl"/> offers to this copy, or null when
    /// it offers nothing newer than the running version.
    /// </summary>
    Task<PackageUpdate?> FindAsync(string releaseFolderUrl, CancellationToken cancellationToken);

    /// <summary>Downloads the package <paramref name="update"/> names and checks it against its checksum.</summary>
    Task DownloadAsync(PackageUpdate update, CancellationToken cancellationToken);

    /// <summary>Has the downloaded <paramref name="update"/> replace this copy as soon as this process exits, and open it.</summary>
    void InstallOnExit(PackageUpdate update);
}

/// <summary>A package found in a release.</summary>
/// <param name="Version">Its version.</param>
/// <param name="FileName">The package's file, as the release lists it.</param>
/// <param name="Handle">What the library needs to download and install it; opaque to everything else.</param>
public sealed record PackageUpdate(Version Version, string FileName, object Handle);
