using System.Globalization;

namespace ContextSwitcher.App.Resources;

/// <summary>
/// Fills a translated pattern from <see cref="Strings"/> - "Couldn't switch to {0}" - with the
/// current language's number and date formats.
/// </summary>
public static class Localized
{
    public static string Format(string pattern, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, pattern, arguments);
}
