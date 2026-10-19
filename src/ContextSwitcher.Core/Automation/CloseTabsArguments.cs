using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.Core.Automation;

/// <summary>The tabs a step closes.</summary>
/// <param name="Browser">The browser they are in.</param>
/// <param name="CloseUrls">The pages whose tabs close.</param>
/// <param name="KeepUrls">The pages the profile being entered opens, whose tabs stay.</param>
/// <param name="ContextId">
/// The profile being left, whose remembered tabs close too; null for the tabs no profile had.
/// </param>
public sealed record CloseTabsArguments(BrowserKind Browser, IReadOnlyList<string> CloseUrls, IReadOnlyList<string> KeepUrls, string? ContextId)
    : IStepArguments
{
    private const string BrowserKey = "browser";
    private const string CloseUrlsKey = "closeUrlsJson";
    private const string KeepUrlsKey = "keepUrlsJson";
    private const string ContextIdKey = "contextId";

    public static CloseTabsArguments From(IReadOnlyDictionary<string, string> arguments) => new(
        StepArgumentText.Choice(arguments, BrowserKey, BrowserKind.Default),
        StepArgumentText.List<string>(arguments, CloseUrlsKey),
        StepArgumentText.List<string>(arguments, KeepUrlsKey),
        arguments.GetValueOrDefault(ContextIdKey));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        Dictionary<string, string> arguments = new()
        {
            [BrowserKey] = this.Browser.ToString(),
            [CloseUrlsKey] = StepArgumentText.List(this.CloseUrls),
            [KeepUrlsKey] = StepArgumentText.List(this.KeepUrls)
        };
        if (this.ContextId is { } contextId)
        {
            arguments[ContextIdKey] = contextId;
        }

        return arguments;
    }
}
