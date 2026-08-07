using ContextSwitcher.Core.Applications;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Discovers the applications installed on this machine so Profile Setup can offer a picker
/// instead of a free-text field. Typing an app name by hand is the single most error-prone step
/// in configuring a profile - a wrong or misremembered name fails silently at switch time.
/// </summary>
public interface IInstalledAppsService
{
    /// <summary>
    /// Returns installed applications sorted by name. Implementations must not throw for an
    /// unreadable directory or a malformed bundle - skip it and return everything else.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the scan.</param>
    Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken);
}
