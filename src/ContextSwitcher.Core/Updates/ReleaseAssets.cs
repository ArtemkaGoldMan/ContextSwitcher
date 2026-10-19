namespace ContextSwitcher.Core.Updates;

/// <summary>
/// The files that make a release one for this platform: the archive its updater installs from, and
/// the installer people download by hand. A release with neither - one carrying only another
/// platform's files - is not this platform's, however new it is. Each platform's updater says which
/// are its own; the composition root hands that to the release source.
/// </summary>
/// <param name="Archive">The file the updater installs from, by its exact name: <c>ContextSwitcher.zip</c> on macOS.</param>
/// <param name="DownloadExtension">How the file people download by hand ends: <c>.dmg</c> on macOS.</param>
public sealed record ReleaseAssets(string Archive, string DownloadExtension)
{
    /// <summary>Whether a release file of this name is one of this platform's.</summary>
    public bool Recognises(string assetName) =>
        string.Equals(assetName, this.Archive, StringComparison.Ordinal)
        || assetName.EndsWith(this.DownloadExtension, StringComparison.OrdinalIgnoreCase);
}
