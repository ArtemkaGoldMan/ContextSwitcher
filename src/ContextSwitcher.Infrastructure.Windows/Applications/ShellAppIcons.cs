using System.Runtime.InteropServices;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Draws an app's icon with the shell's own <c>IShellItemImageFactory</c> on the item
/// <c>shell:AppsFolder\&lt;id&gt;</c> - the one the Start menu draws, for a desktop app and a Store app
/// alike. The shell wants a single-threaded apartment, which a thread of the thread pool isn't, so each
/// icon is drawn on a thread of its own that is waited for a while and no longer. The bitmap comes back
/// as pixels with the alpha multiplied in, and goes out as a PNG.
/// </summary>
public sealed class ShellAppIcons : IAppIcons
{
    private static readonly TimeSpan DrawWithin = TimeSpan.FromSeconds(10);

    private const uint IconOnly = 0x4;
    private const uint BiggerSizeOk = 0x1;
    private const int BitmapRgb = 0;
    private const uint DibRgbColors = 0;

    private readonly ILogger logger;

    public ShellAppIcons(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    /// <inheritdoc />
    public byte[]? RenderPng(string appId, int size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        byte[]? png = null;
        Thread thread = new(() => png = this.DrawOnThisThread(appId, size)) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(DrawWithin))
        {
            this.logger.Warn("App", "AppIconTimedOut", $"Windows took too long to draw the icon of {appId}.");
            return null;
        }

        return png;
    }

    private byte[]? DrawOnThisThread(string appId, int size)
    {
        nint bitmap = 0;
        try
        {
            Guid factoryId = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName($@"shell:AppsFolder\{appId}", 0, ref factoryId, out IShellItemImageFactory? factory) != 0 || factory is null)
            {
                return null;
            }

            try
            {
                if (factory.GetImage(new Size(size, size), IconOnly | BiggerSizeOk, out bitmap) != 0 || bitmap == 0)
                {
                    return null;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }

            return ToPng(bitmap);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or OverflowException)
        {
            this.logger.Warn("App", "AppIconNotDrawn", $"Windows couldn't draw the icon of {appId}.", ex);
            return null;
        }
        finally
        {
            if (bitmap != 0)
            {
                _ = DeleteObject(bitmap);
            }
        }
    }

    /// <summary>The pixels of <paramref name="bitmap"/> from the top, as a PNG; null when GDI won't give them.</summary>
    private static byte[]? ToPng(nint bitmap)
    {
        if (GetObjectW(bitmap, Marshal.SizeOf<BitmapInfo>(), out BitmapInfo info) == 0 || info.Width < 1 || info.Height < 1)
        {
            return null;
        }

        int width = info.Width;
        int height = info.Height;
        BitmapInfoHeader header = new()
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
            Compression = BitmapRgb
        };
        byte[] pixels = new byte[checked(width * height * 4)];
        nint screen = GetDC(0);
        try
        {
            if (GetDIBits(screen, bitmap, 0, (uint)height, pixels, ref header, DibRgbColors) == 0)
            {
                return null;
            }
        }
        finally
        {
            _ = ReleaseDC(0, screen);
        }

        PngEncoder.Unpremultiply(pixels);
        return PngEncoder.FromBgra(width, height, pixels);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Size(int width, int height)
    {
        public readonly int Width = width;
        public readonly int Height = height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public nint Bits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(Size size, uint flags, out nint bitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, nint bindContext, ref Guid riid, out IShellItemImageFactory? factory);

    [DllImport("gdi32.dll")]
    private static extern int GetObjectW(nint handle, int count, out BitmapInfo info);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint context, nint bitmap, uint startScan, uint scanLines, byte[] bits, ref BitmapInfoHeader info, uint usage);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint handle);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint window, nint context);
}
