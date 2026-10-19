namespace ContextSwitcher.Core.Automation;

/// <summary>The Docker containers a step starts or stops.</summary>
public sealed record ContainersArguments(IReadOnlyList<string> Containers) : IStepArguments
{
    private const string ContainersKey = "containers";

    public static ContainersArguments From(IReadOnlyDictionary<string, string> arguments) =>
        new(StepArgumentText.List<string>(arguments, ContainersKey));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToDictionary() =>
        new Dictionary<string, string> { [ContainersKey] = StepArgumentText.List(this.Containers) };
}
