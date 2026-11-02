using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// What the settings say of how an app a profile names is found on this system - the <c>apps</c> map of
/// <c>settings.json</c>. Read where the platform looks an app up, so that this code needs no more of
/// the settings than that.
/// </summary>
public interface IAppsMap
{
    /// <summary>The entry for <paramref name="appName"/>, by the name a profile lists it by; null when there is none.</summary>
    AppIdentity? Of(string appName);
}
