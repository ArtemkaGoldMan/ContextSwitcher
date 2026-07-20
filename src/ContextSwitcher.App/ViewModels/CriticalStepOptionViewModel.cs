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
    /// The step types a user can meaningfully mark critical. <c>WriteState</c> and
    /// <c>AnalyticsBoundary</c> are internal pipeline bookkeeping steps, not user-facing
    /// automation, so they are omitted here.
    /// </summary>
    public static IReadOnlyList<AutomationStepType> SelectableStepTypes { get; } =
    [
        AutomationStepType.CloseApplications,
        AutomationStepType.LaunchApplications,
        AutomationStepType.ManageBrowserContext,
        AutomationStepType.SetTheme,
        AutomationStepType.SetWallpaper,
        AutomationStepType.SetFocusMode,
        AutomationStepType.ControlMedia,
        AutomationStepType.StartDockerResources,
        AutomationStepType.StopDockerResources,
        AutomationStepType.OpenUrls
    ];

    public AutomationStepType StepType { get; } = stepType;

    public string DisplayName { get; } = displayName;

    public bool IsSelected
    {
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected, value);
    }
}
