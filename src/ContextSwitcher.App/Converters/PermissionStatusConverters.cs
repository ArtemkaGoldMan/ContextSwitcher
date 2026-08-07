using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ContextSwitcher.App.Converters;

/// <summary>
/// Converts a nullable permission-granted flag (<see langword="null"/> while still checking) into
/// the small status pill's label on the Settings page.
/// </summary>
public sealed class PermissionStatusTextConverter : IValueConverter
{
    public static readonly PermissionStatusTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => "Granted",
            false => "Not granted",
            _ => "Checking…"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts the same flag into the status pill's background brush.
/// </summary>
public sealed class PermissionStatusBrushConverter : IValueConverter
{
    public static readonly PermissionStatusBrushConverter Instance = new();

    private static readonly IBrush GrantedBrush = new SolidColorBrush(Color.Parse("#2630D158"));
    private static readonly IBrush NotGrantedBrush = new SolidColorBrush(Color.Parse("#26FF453A"));
    private static readonly IBrush UnknownBrush = new SolidColorBrush(Color.Parse("#268E8E93"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => GrantedBrush,
            false => NotGrantedBrush,
            _ => UnknownBrush
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
