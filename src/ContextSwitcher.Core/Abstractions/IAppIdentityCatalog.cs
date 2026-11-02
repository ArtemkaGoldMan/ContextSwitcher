using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// What this system knows of an app a profile names, to be written into the <c>apps</c> map of the
/// settings: on Windows, the app's AppUserModelID and executable. A system that goes by the name alone
/// - the Mac - knows nothing to write, and the map is left as it is.
/// </summary>
public interface IAppIdentityCatalog
{
    /// <summary>
    /// How <paramref name="appName"/> is found on this system; null when there is nothing to record,
    /// the app isn't installed, or this system doesn't use the map. May list the installed apps first.
    /// </summary>
    Task<AppIdentity?> IdentifyAsync(string appName, CancellationToken cancellationToken);
}
