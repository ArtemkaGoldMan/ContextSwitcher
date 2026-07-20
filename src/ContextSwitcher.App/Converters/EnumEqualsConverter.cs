using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ContextSwitcher.App.Converters;

/// <summary>
/// Converts an enum value to <see langword="true"/> when it equals the converter parameter, for
/// RadioButton-based segmented controls where every option binds to the same enum property
/// (agent.md section 11.2's theme-mode segmented control). Checking an option sets the bound
/// property to that option's parameter.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null && parameter is not null && value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? parameter : BindingOperations.DoNothing;
    }
}
