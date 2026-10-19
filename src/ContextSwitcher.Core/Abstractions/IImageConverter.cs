namespace ContextSwitcher.Core.Abstractions;

/// <summary>
/// Turns an image file - an app's icon, a site's favicon - into a small PNG the app can show. How it
/// is drawn is the platform's: macOS has <c>sips</c> built in; Windows will draw it with SkiaSharp.
/// </summary>
public interface IImageConverter
{
    /// <summary>
    /// Writes <paramref name="source"/> as a PNG at <paramref name="destination"/>, scaled so its
    /// longer side is at most <paramref name="size"/> pixels.
    /// </summary>
    /// <returns>Whether the PNG was written.</returns>
    Task<bool> ToPngAsync(string source, string destination, int size, CancellationToken cancellationToken);
}
