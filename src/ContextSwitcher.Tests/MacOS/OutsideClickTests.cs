using System.Runtime.InteropServices;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class OutsideClickTests
{
    private static readonly ScreenRect Card = new(1000, 600, 360, 400);

    /// <summary>The menu bar icon sits above the card - a press there is outside, and closes it.</summary>
    [Fact]
    public void TheMenuBarAboveTheCardIsOutsideIt()
    {
        Assert.False(Card.Contains(new ScreenPoint(1300, 1110)));
        Assert.False(Card.Contains(new ScreenPoint(200, 300)));
        Assert.True(Card.Contains(new ScreenPoint(1100, 800)));
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
    [MacOSFact]
    public void AppKitAnswersWithAPlausiblePointerAndButtonState()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
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
