using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.Services;

/// <summary>The <c>apps</c> map of the settings as they are now, which a save or an edit of the file changes under it.</summary>
public sealed class ConfigurationAppsMap(IAppState appState) : IAppsMap
{
    /// <inheritdoc />
    public AppIdentity? Of(string appName) => AppsMap.Of(appState.Configuration, appName);
}
