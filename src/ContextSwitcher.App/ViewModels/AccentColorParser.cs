using Avalonia.Media;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Parses a context's accent color hex string into a brush for row dots and preview swatches,
/// falling back to gray for invalid or in-progress input.
/// </summary>
public static class AccentColorParser
{
    public static IBrush ToBrush(string accentColorHex)
    {
        return Color.TryParse(accentColorHex, out Color color)
            ? new SolidColorBrush(color)
            : Brushes.Gray;
    }
}
