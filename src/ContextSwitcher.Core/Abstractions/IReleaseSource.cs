using ContextSwitcher.Core.Updates;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>Where new versions of the app are published - GitHub Releases.</summary>
public interface IReleaseSource
{
    /// <summary>
    /// The newest published release, or null when nothing has been published yet. Throws
    /// <see cref="UpdateException"/> when the answer could not be had, such as when offline.
    /// </summary>
    Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken);
}
