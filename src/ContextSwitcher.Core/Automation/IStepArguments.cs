namespace ContextSwitcher.Core.Automation;

/// <summary>
/// What one type of step needs to run, as the plan builder writes it and the step's handler reads it.
/// Each record keeps its own keys: a step carries its arguments as <see cref="AutomationStep.Arguments"/>,
/// text under names, and only the record writes and reads them.
/// </summary>
public interface IStepArguments
{
    /// <summary>The arguments as a step carries them.</summary>
    IReadOnlyDictionary<string, string> ToDictionary();
}
