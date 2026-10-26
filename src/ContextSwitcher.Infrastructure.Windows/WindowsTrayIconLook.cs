using ContextSwitcher.Core.Abstractions;
using Microsoft.Win32;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// Windows doesn't colour tray icons for the taskbar the way macOS colours a template image, so the
/// icon comes in a dark and a light drawing, and this says which suits the taskbar now. The taskbar
/// follows Windows' mode, not the apps' - the two can differ - and a change to it is heard as it
/// happens.
/// </summary>
public sealed class WindowsTrayIconLook : ITrayIconLook, IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private TrayIconLook current;

    public WindowsTrayIconLook()
    {
        this.current = Read();
        SystemEvents.UserPreferenceChanged += this.OnUserPreferenceChanged;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public TrayIconLook Current => this.current;

    /// <summary>
    /// The drawing for the taskbar's mode as the registry holds it: 1 for light. Without the value,
    /// the taskbar is dark - as it always was before Windows 10 had a light one.
    /// </summary>
    public static TrayIconLook For(int? systemUsesLightTheme) => systemUsesLightTheme == 1 ? TrayIconLook.Dark : TrayIconLook.Light;

    /// <inheritdoc />
    public void Dispose() => SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;

    private static TrayIconLook Read()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return For(key?.GetValue("SystemUsesLightTheme") as int?);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        TrayIconLook now = Read();
        if (now == this.current)
        {
            return;
        }

        this.current = now;
        this.Changed?.Invoke(this, EventArgs.Empty);
    }
}
