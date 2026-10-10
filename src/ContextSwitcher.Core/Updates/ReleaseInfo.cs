namespace ContextSwitcher.Core.Updates;

/// <summary>The newest published release, as far as the update check is concerned.</summary>
/// <param name="Version">Its version, from the release's tag.</param>
/// <param name="PageUrl">The release's page, where its notes and downloads are.</param>
/// <param name="ArchiveUrl">
/// The zipped app the updater installs from, or null when the release has none - one published by
/// hand with only a disk image, say.
/// </param>
public sealed record ReleaseInfo(Version Version, string PageUrl, string? ArchiveUrl);
