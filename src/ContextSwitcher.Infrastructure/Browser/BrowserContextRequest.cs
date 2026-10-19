using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Infrastructure.Browser;

/// <summary>
/// Everything <see cref="IBrowserLauncher"/> needs to manage a context's browser state for one
/// switch, decoded from a <c>ManageBrowserContext</c> automation step's arguments.
/// </summary>
/// <param name="ContextId">The profile being entered, under which the tabs it opens are remembered.</param>
/// <param name="RememberUrls">
/// The pages that close when the profile is left: the tabs opened for them are remembered, so they
/// close even after moving to another address.
/// </param>
public sealed record BrowserContextRequest(
    BrowserManagementMode Mode,
    BrowserKind Browser,
    IReadOnlyList<string> Urls,
    bool AvoidDuplicateTabs,
    IReadOnlyList<BrowserProfileConfig> Profiles,
    TimeSpan Timeout,
    string? ContextId = null,
    IReadOnlyList<string>? RememberUrls = null);
