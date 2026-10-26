using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// Windows has no Dock: the app's icon is in the tray, and its open windows have taskbar buttons of
/// their own. Settings offers no Dock setting there.
/// </summary>
public sealed class NoDockIcon : IDockIcon
{
    /// <inheritdoc />
    public bool? IsShown() => null;

    /// <inheritdoc />
    public void Show(bool show)
    {
    }
}
