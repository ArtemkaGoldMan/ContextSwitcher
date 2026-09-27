using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ContextSwitcher.App.ViewModels;

namespace ContextSwitcher.App.Converters;

/// <summary>
/// Turns a stored profile icon name into the geometry to draw, falling back to a circle for names
/// this build does not know.
///
/// Deliberately parses on every conversion instead of caching: a Geometry belongs to the thread that
/// created it, so a shared cache is one stray call away from the same "different thread owns it"
/// crash that brushes caused. Bindings only convert on the UI thread, and the icons are tiny.
/// </summary>
public sealed class ProfileIconConverter : IValueConverter
{
    public static ProfileIconConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Geometry.Parse(ProfileIcons.Find(value as string).PathData);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
