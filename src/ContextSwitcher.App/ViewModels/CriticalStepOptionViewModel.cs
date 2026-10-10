using ContextSwitcher.Core.Automation;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One checkbox in a Profile Setup's switch-policy section. <see cref="StepType"/>'s
/// <see cref="Enum.ToString()"/> is written verbatim into <c>switchPolicy.criticalSteps[]</c>,
/// which must match an <see cref="AutomationStepType"/> member name exactly (agent.md section
/// 6.1) - offering a fixed checkbox list instead of free text makes a typo impossible.
/// </summary>
public sealed class CriticalStepOptionViewModel(AutomationStepType stepType, string displayName, bool isSelected) : ViewModelBase
{
    private bool isSelected = isSelected;

    /// <summary>
    /// The steps a switch actually runs, in the order it runs them, each named for what it does.
    /// <c>WriteState</c> and <c>AnalyticsBoundary</c> are internal bookkeeping, and <c>OpenUrls</c>
    /// never runs - opening pages is part of <c>ManageBrowserContext</c> - so marking it did nothing.
    /// </summary>
    public static IReadOnlyList<(AutomationStepType Type, string Label)> SelectableSteps { get; } =
    [
        (AutomationStepType.StopDockerResources, "Stopping containers"),
        (AutomationStepType.CloseApplications, "Quitting apps"),
        (AutomationStepType.SetFocusMode, "Changing Focus"),
        (AutomationStepType.LaunchApplications, "Opening apps"),
        (AutomationStepType.ManageBrowserContext, "Opening the browser"),
        (AutomationStepType.StartDockerResources, "Starting containers"),
        (AutomationStepType.ControlMedia, "Starting music")
    ];

    public AutomationStepType StepType { get; } = stepType;

    public string DisplayName { get; } = displayName;

    public bool IsSelected
    {
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected, value);
    }
}
