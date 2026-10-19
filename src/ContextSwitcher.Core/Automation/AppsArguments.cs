namespace ContextSwitcher.Core.Automation;

/// <summary>The apps a step closes, opens or hides.</summary>
public sealed record AppsArguments(IReadOnlyList<string> Apps) : IStepArguments
{
    private const string AppsKey = "apps";

    public static AppsArguments From(IReadOnlyDictionary<string, string> arguments) =>
        new(StepArgumentText.List<string>(arguments, AppsKey));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToDictionary() =>
        new Dictionary<string, string> { [AppsKey] = StepArgumentText.List(this.Apps) };
}
