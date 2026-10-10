using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace ContextSwitcher.App.Converters;

/// <summary>
/// Looks up an icon geometry from Styles/Icons.axaml by its key, for a template shared by lists of
/// different things - one picker template draws a globe for open tabs and a box for containers.
/// The geometries are the application's own resources, created on the UI thread at startup.
/// </summary>
public sealed class GlyphResourceConverter : IValueConverter
{
    public static GlyphResourceConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key && Application.Current?.TryFindResource(key, out object? resource) == true ? resource : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
