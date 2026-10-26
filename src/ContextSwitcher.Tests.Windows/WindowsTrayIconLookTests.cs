using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Infrastructure.Windows;

namespace ContextSwitcher.Tests.Windows;

/// <summary>The tray icon's drawing for the taskbar: dark on a light one, light on a dark one.</summary>
public sealed class WindowsTrayIconLookTests
{
    [Theory]
    [InlineData(1, TrayIconLook.Dark)]
    [InlineData(0, TrayIconLook.Light)]
    [InlineData(null, TrayIconLook.Light)]
    public void TheDrawingSuitsTheTaskbar(int? systemUsesLightTheme, TrayIconLook expected)
    {
        Assert.Equal(expected, WindowsTrayIconLook.For(systemUsesLightTheme));
    }

    /// <summary>Whatever this PC's taskbar is, its drawing is one of the two made for a taskbar.</summary>
    [Fact]
    public void ThisPcsTaskbarHasADrawingOfItsOwn()
    {
        using WindowsTrayIconLook look = new();

        Assert.NotEqual(TrayIconLook.Template, look.Current);
    }
}
