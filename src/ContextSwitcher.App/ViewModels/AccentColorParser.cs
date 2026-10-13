using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Parses a context's accent color hex string into a brush for row dots and preview swatches,
/// falling back to gray for invalid or in-progress input.
/// </summary>
public static class AccentColorParser
{
    /// <summary>
    /// Returns an <b>immutable</b> brush deliberately. A plain SolidColorBrush is an AvaloniaObject
    /// and belongs to the thread that created it, so one built while a view model was rebuilt off
    /// the UI thread crashed the next render that drew it. Immutable brushes have no such affinity,
    /// which keeps these safe whichever thread happens to construct the view model.
    /// </summary>
    public static IBrush ToBrush(string accentColorHex)
    {
        return Color.TryParse(accentColorHex, out Color color)
            ? new ImmutableSolidColorBrush(color)
            : Brushes.Gray;
    }

    /// <summary>
    /// The accent, faint enough to lay over a card as a wash of the profile's colour - immutable, for
    /// the same reason as <see cref="ToBrush"/>.
    /// </summary>
    public static IBrush ToTint(string accentColorHex, byte alpha)
    {
        Color color = Color.TryParse(accentColorHex, out Color parsed) ? parsed : Colors.Gray;
        return new ImmutableSolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }
}
