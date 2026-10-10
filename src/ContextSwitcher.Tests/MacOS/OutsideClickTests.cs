using System.Runtime.InteropServices;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class OutsideClickTests
{
    private static readonly ScreenRect Card = new(1000, 600, 360, 400);

    [Fact]
    public void AFreshPressOutsideTheCardCounts()
    {
        Assert.True(OutsideClick.IsPressOutside(wasDown: false, isDown: true, new ScreenPoint(200, 300), Card));
    }

    /// <summary>The menu bar icon sits above the card - a press there is outside, and closes it.</summary>
    [Fact]
    public void APressOnTheMenuBarAboveTheCardCounts()
    {
        Assert.True(OutsideClick.IsPressOutside(false, true, new ScreenPoint(1300, 1110), Card));
    }

    [Theory]
    [InlineData(false, true, 1100, 800)] // a press inside the card: a button being clicked
    [InlineData(true, true, 200, 300)]   // still held from before - a drag, or the click that opened it
    [InlineData(false, false, 200, 300)] // no button down at all
    [InlineData(true, false, 200, 300)]  // a release
    public void AnythingElseDoesNot(bool wasDown, bool isDown, double x, double y)
    {
        Assert.False(OutsideClick.IsPressOutside(wasDown, isDown, new ScreenPoint(x, y), Card));
    }

    /// <summary>
    /// The window is bigger than the card by the transparent room its shadow is drawn into. AppKit's
    /// rects grow upwards from the bottom-left, so the bottom margin is applied to the origin.
    /// </summary>
    [Fact]
    public void InsetShrinksTheWindowToTheCardInAppKitsCoordinates()
    {
        ScreenRect window = new(100, 200, 392, 447);

        ScreenRect card = window.Inset(left: 16, top: 10, right: 16, bottom: 22);

        Assert.Equal(new ScreenRect(116, 222, 360, 415), card);
        Assert.True(card.Contains(new ScreenPoint(116, 222)));
        Assert.False(card.Contains(new ScreenPoint(110, 300)));
    }

    /// <summary>
    /// The calls themselves, for real: AppKit's NSPoint comes back from objc_msgSend in registers, and
    /// a wrong struct declaration would read garbage rather than fail. Run with AppKit loaded, as the
    /// app has it; the pointer is somewhere on screen, and on a test run no button is held.
    /// </summary>
    [Fact]
    public void AppKitAnswersWithAPlausiblePointerAndButtonState()
    {
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            Assert.Null(OutsideClick.PointerLocation());
            return;
        }

        NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");

        ScreenPoint? pointer = OutsideClick.PointerLocation();
        Assert.NotNull(pointer);
        Assert.True(double.IsFinite(pointer.Value.X) && double.IsFinite(pointer.Value.Y));
        Assert.InRange(pointer.Value.X, -20000, 20000);
        Assert.InRange(pointer.Value.Y, -20000, 20000);
        Assert.NotNull(OutsideClick.IsMouseDown());
        Assert.Null(OutsideClick.WindowFrame(IntPtr.Zero));
    }
}
