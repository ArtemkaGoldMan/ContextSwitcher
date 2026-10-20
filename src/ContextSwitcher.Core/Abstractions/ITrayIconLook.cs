namespace ContextSwitcher.Core.Abstractions;

/// <summary>Which drawing of the menu bar or tray icon suits what it sits on, and when that changes.</summary>
public interface ITrayIconLook
{
    /// <summary>The drawing to show now.</summary>
    TrayIconLook Current { get; }

    /// <summary>Raised when <see cref="Current"/> has changed, on a thread of its own.</summary>
    event EventHandler? Changed;
}

/// <summary>The drawings of the icon.</summary>
public enum TrayIconLook
{
    /// <summary>One drawing in black, which the system colours itself: macOS's template image.</summary>
    Template,

    /// <summary>A dark drawing, for a light taskbar.</summary>
    Dark,

    /// <summary>A light drawing, for a dark taskbar.</summary>
    Light
}
