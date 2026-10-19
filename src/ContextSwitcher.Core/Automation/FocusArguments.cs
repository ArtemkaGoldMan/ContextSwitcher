namespace ContextSwitcher.Core.Automation;

/// <summary>The Focus mode a step turns on, or the one it turns off.</summary>
/// <param name="Enabled">Whether the profile being entered turns a Focus on.</param>
/// <param name="ModeName">The mode it turns on.</param>
/// <param name="PreviousModeName">
/// What to turn off when the profile being entered wants no Focus: the mode the profile being left
/// turned on, each having its own Off Shortcut. Empty when it turned none on.
/// </param>
public sealed record FocusArguments(bool Enabled, string ModeName, string PreviousModeName) : IStepArguments
{
    private const string EnabledKey = "enabled";
    private const string ModeNameKey = "modeName";
    private const string PreviousModeNameKey = "previousModeName";

    public static FocusArguments From(IReadOnlyDictionary<string, string> arguments) => new(
        StepArgumentText.Flag(arguments, EnabledKey),
        StepArgumentText.Text(arguments, ModeNameKey),
        StepArgumentText.Text(arguments, PreviousModeNameKey));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>
    {
        [EnabledKey] = StepArgumentText.Flag(this.Enabled),
        [ModeNameKey] = this.ModeName,
        [PreviousModeNameKey] = this.PreviousModeName
    };
}
