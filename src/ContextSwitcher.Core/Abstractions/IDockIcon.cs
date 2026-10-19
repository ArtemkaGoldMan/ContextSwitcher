namespace ContextSwitcher.Core.Abstractions;

/// <summary>Shows or hides the app's icon among the running apps - the Dock, on a Mac - while it runs.</summary>
public interface IDockIcon
{
    /// <summary>Whether the icon shows now, or null where that can't be asked.</summary>
    bool? IsShown();

    /// <summary>Shows the icon, or hides it.</summary>
    void Show(bool show);
}
