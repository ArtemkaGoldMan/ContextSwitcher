namespace ContextSwitcher.Core.Automation;

/// <summary>
/// The kind of automation action an <see cref="AutomationStep"/> performs.
/// </summary>
public enum AutomationStepType
{
    CloseApplications,
    LaunchApplications,
    ManageBrowserContext,
    SetFocusMode,
    ControlMedia,
    StartDockerResources,
    StopDockerResources,
    OpenUrls,
    WriteState,
    AnalyticsBoundary,

    /// <summary>Closes the tabs of the pages the context being left lists to close.</summary>
    CloseBrowserTabs,

    /// <summary>Hides the apps the context being left lists to hide.</summary>
    HideApplications
}
