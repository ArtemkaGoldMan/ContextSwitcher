using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Automation;

/// <summary>The pages, or the browser profiles, a step opens for the profile being entered.</summary>
/// <param name="ContextId">The profile being entered, under which the tabs it opens are remembered.</param>
/// <param name="RememberUrls">
/// The pages that close when the profile is left: the tabs opened for them are remembered, in a
/// browser that can say which tab is which, so they close wherever they have gone.
/// </param>
public sealed record BrowserContextArguments(
    string? ContextId,
    BrowserManagementMode Mode,
    BrowserKind Browser,
    IReadOnlyList<string> Urls,
    IReadOnlyList<string> RememberUrls,
    bool AvoidDuplicateTabs,
    IReadOnlyList<BrowserProfileConfig> Profiles) : IStepArguments
{
    private const string ContextIdKey = "contextId";
    private const string ModeKey = "mode";
    private const string BrowserKey = "browser";
    private const string UrlsKey = "urls";
    private const string RememberUrlsKey = "rememberUrlsJson";
    private const string AvoidDuplicateTabsKey = "avoidDuplicateTabs";
    private const string ProfilesKey = "profilesJson";

    public static BrowserContextArguments From(IReadOnlyDictionary<string, string> arguments) => new(
        arguments.GetValueOrDefault(ContextIdKey),
        StepArgumentText.Choice(arguments, ModeKey, BrowserManagementMode.None),
        StepArgumentText.Choice(arguments, BrowserKey, BrowserKind.Default),
        StepArgumentText.List<string>(arguments, UrlsKey),
        StepArgumentText.List<string>(arguments, RememberUrlsKey),
        StepArgumentText.Flag(arguments, AvoidDuplicateTabsKey),
        StepArgumentText.List<BrowserProfileConfig>(arguments, ProfilesKey));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        Dictionary<string, string> arguments = new()
        {
            [ModeKey] = this.Mode.ToString(),
            [BrowserKey] = this.Browser.ToString(),
            [UrlsKey] = StepArgumentText.List(this.Urls),
            [RememberUrlsKey] = StepArgumentText.List(this.RememberUrls),
            [AvoidDuplicateTabsKey] = StepArgumentText.Flag(this.AvoidDuplicateTabs),
            [ProfilesKey] = StepArgumentText.List(this.Profiles)
        };
        if (this.ContextId is { } contextId)
        {
            arguments[ContextIdKey] = contextId;
        }

        return arguments;
    }
}
