using ContextSwitcher.Core.Abstractions;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>The menu bar icon is a template image, which macOS colours for a light or a dark menu bar itself.</summary>
public sealed class MacTrayIconLook : ITrayIconLook
{
    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public TrayIconLook Current => TrayIconLook.Template;
}
