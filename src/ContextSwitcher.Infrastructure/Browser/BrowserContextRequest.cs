using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Everything <see cref="BrowserLauncher"/> needs to manage a context's browser state for one
/// switch, decoded from a <c>ManageBrowserContext</c> automation step's arguments.
/// </summary>
public sealed record BrowserContextRequest(
    BrowserManagementMode Mode,
    BrowserKind Browser,
    IReadOnlyList<string> Urls,
    IReadOnlyList<string> TabGroups,
    bool AvoidDuplicateTabs,
    IReadOnlyList<BrowserProfileConfig> Profiles,
    TimeSpan Timeout);
