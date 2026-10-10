using System.Globalization;
using Avalonia.Data.Converters;

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
            true => "Allowed",
            false => "Not allowed",
            _ => "Checking…"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
