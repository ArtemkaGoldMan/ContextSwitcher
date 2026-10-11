using System.Globalization;

namespace ContextSwitcher.App.Resources;

/// <summary>
/// Counted phrases - "1 day", "2 days" - in the right form for the language. English has two forms,
/// but Ukrainian and Polish have three ("1 день", "2 дні", "5 днів"), which a single pattern can't
/// express. Each phrase is a set of keys in <see cref="Strings"/>, <c>Base_One</c>, <c>Base_Few</c>,
/// <c>Base_Many</c> and <c>Base_Other</c>; a language provides only the forms it has, and anything
/// missing falls back to <c>_Other</c>. The categories are the CLDR plural rules for whole numbers.
/// </summary>
public static class Plurals
{
    public static string Format(string baseKey, int count)
    {
        string? pattern = Strings.ResourceManager.GetString($"{baseKey}_{Category(count, CultureInfo.CurrentUICulture)}", Strings.Culture)
            ?? Strings.ResourceManager.GetString($"{baseKey}_Other", Strings.Culture)
            ?? throw new InvalidOperationException($"No plural forms for '{baseKey}'.");
        return Localized.Format(pattern, count);
    }

    /// <summary>"One", "Few", "Many" or "Other" for <paramref name="count"/> in <paramref name="culture"/>'s language.</summary>
    public static string Category(int count, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        int n = Math.Abs(count), lastDigit = n % 10, lastTwo = n % 100;
        return culture.TwoLetterISOLanguageName switch
        {
            "uk" or "ru" => lastDigit == 1 && lastTwo != 11 ? "One"
                : lastDigit is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "Few"
                : "Many",
            "pl" => n == 1 ? "One"
                : lastDigit is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "Few"
                : "Many",
            _ => n == 1 ? "One" : "Other"
        };
    }
}
