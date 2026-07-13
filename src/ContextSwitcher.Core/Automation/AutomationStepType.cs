namespace ContextSwitcher.Core.Automation;

/// <summary>
/// The kind of automation action an <see cref="AutomationStep"/> performs.
/// </summary>
public enum AutomationStepType
{
    CloseApplications,
    LaunchApplications,
    ManageBrowserContext,
    SetTheme,
    SetWallpaper,
    SetFocusMode,
    ControlMedia,
    StartDockerResources,
    StopDockerResources,
    OpenUrls,
    WriteState,
    AnalyticsBoundary
}
