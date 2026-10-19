using Avalonia.Media.Imaging;

namespace ContextSwitcher.App.Services;

/// <summary>The icons the app keeps as PNG files - apps' and sites' - read for showing.</summary>
public static class IconFiles
{
    /// <summary>
    /// The icon at <paramref name="path"/>, or null when there is none there or it can't be read.
    /// </summary>
    public static Bitmap? Load(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            // Not logged: a truncated or unreadable cached PNG leaves the icon out and breaks nothing.
            return null;
        }
    }
}
