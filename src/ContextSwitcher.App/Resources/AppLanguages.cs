using System.Globalization;

namespace ContextSwitcher.App.Resources;

/// <summary>A language the app is translated into, named in that language: "Українська".</summary>
public sealed record AppLanguage(string Code, string NativeName);

/// <summary>
/// Which language the app shows, chosen once at startup - before any window, so every text is read
/// in it. Adding a language: its Strings.&lt;code&gt;.resx, and its entry in <see cref="Supported"/>;
/// the Settings list, the macOS language list (scripts/build-app.sh) and the tests take it from there.
/// </summary>
public static class AppLanguages
{
    /// <summary>The setting that follows macOS.</summary>
    public const string System = "system";

    /// <summary>The translations the app ships, English first - the fallback for everything.</summary>
    public static IReadOnlyList<AppLanguage> Supported { get; } =
    [
        new("en", "English"),
        new("uk", "Українська")
    ];

    /// <summary>The language the app is showing, as applied at startup.</summary>
    public static string Current { get; private set; } = "en";

    /// <summary>
    /// The language to show: the one set in Settings if it is translated, and otherwise the first of
    /// the Mac's preferred languages that is - the way macOS itself picks an app's language - with
    /// English as the last resort.
    /// </summary>
    public static string Resolve(string? setting, IReadOnlyList<string> preferred)
    {
        ArgumentNullException.ThrowIfNull(preferred);

        if (Find(setting) is { } chosen)
        {
            return chosen.Code;
        }

        return preferred.Select(Find).FirstOrDefault(language => language is not null)?.Code ?? Supported[0].Code;
    }

    /// <summary>
    /// Shows the app in <paramref name="code"/>. Formats keep the Mac's region - Ukrainian on a Mac set
    /// to Poland reads "uk-PL" - and only change language when the app's does, so a Mac whose language
    /// is already the app's keeps exactly its own formats.
    /// </summary>
    public static void Apply(string code)
    {
        CultureInfo formats = CultureFor(code, CultureInfo.CurrentCulture);
        CultureInfo.DefaultThreadCurrentCulture = formats;
        CultureInfo.CurrentCulture = formats;
        CultureInfo.DefaultThreadCurrentUICulture = formats;
        CultureInfo.CurrentUICulture = formats;
        Current = code;
    }

    private static AppLanguage? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, System, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string language = name.Split('-', '_')[0];
        return Supported.FirstOrDefault(supported => string.Equals(supported.Code, language, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The culture <see cref="Apply"/> shows <paramref name="code"/> in, on a Mac set to
    /// <paramref name="current"/>: that same culture when its language is already
    /// <paramref name="code"/>, and otherwise <paramref name="code"/> with its region.
    /// </summary>
    public static CultureInfo CultureFor(string code, CultureInfo current)
    {
        ArgumentNullException.ThrowIfNull(current);

        return string.Equals(current.TwoLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase)
            ? current
            : WithRegion(code, current);
    }

    /// <summary>The language with the region of <paramref name="current"/>, or the language alone if that pair doesn't exist.</summary>
    private static CultureInfo WithRegion(string code, CultureInfo current)
    {
        string region = current.Name.Contains('-', StringComparison.Ordinal) ? current.Name[(current.Name.LastIndexOf('-') + 1)..] : string.Empty;
        try
        {
            return region.Length > 0 ? CultureInfo.GetCultureInfo($"{code}-{region}") : CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(code);
        }
    }
}
