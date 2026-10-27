using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Hides apps by minimising their main windows to the taskbar, so they keep their windows and come back
/// at once. Windows has no Hide for an app; <see cref="WindowsAppLauncher"/> restores the windows when the
/// profile is entered again.
/// </summary>
public sealed class WindowsApplicationHider : IApplicationHider
{
    private readonly AppWindows appWindows;

    public WindowsApplicationHider(AppWindows appWindows)
    {
        ArgumentNullException.ThrowIfNull(appWindows);

        this.appWindows = appWindows;
    }

    /// <inheritdoc />
    public int Hide(IReadOnlyList<string> apps)
    {
        ArgumentNullException.ThrowIfNull(apps);

        return apps.Count(app => !string.IsNullOrWhiteSpace(app) && this.appWindows.Minimize(app));
    }
}
