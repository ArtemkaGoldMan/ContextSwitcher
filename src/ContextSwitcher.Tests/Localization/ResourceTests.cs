using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ContextSwitcher.Tests.Localization;

/// <summary>
/// Keeps the translations honest as the app and its languages grow: every visible text comes from
/// Strings.resx, every translation fits the English it translates, and every counted phrase has the
/// forms the code asks for. A missing translation is not an error - .NET falls back to English - but
/// a broken one is.
/// </summary>
public sealed partial class ResourceTests
{
    private static readonly string Repository = FindRepository();
    private static readonly string ResourcesFolder = Path.Combine(Repository, "src", "ContextSwitcher.App", "Resources");

    /// <summary>Text typed straight into a view can't be translated. The product name is the one exception.</summary>
    [Fact]
    public void NoViewHasTextWrittenIntoIt()
    {
        string[] attributes = ["Text", "Content", "Title", "Description", "Header", "ToolTip.Tip", "Watermark", "PlaceholderText", "AutomationProperties.Name"];
        Regex literal = new($"(?<![\\w.:])({string.Join("|", attributes.Select(Regex.Escape))})=\"([^\"{{][^\"]*)\"");

        List<string> found = Directory.EnumerateFiles(Path.Combine(Repository, "src", "ContextSwitcher.App"), "*.axaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => literal.Matches(File.ReadAllText(path))
                .Where(m => Regex.IsMatch(m.Groups[2].Value, "[A-Za-z]") && m.Groups[2].Value != "Context Switcher")
                .Select(m => $"{Path.GetFileName(path)}: {m.Value}"))
            .ToList();

        Assert.True(found.Count == 0, "Move these into Resources/Strings.resx:\n" + string.Join("\n", found));
    }

    [Fact]
    public void EveryEnglishTextIsFilledIn()
    {
        Assert.DoesNotContain(Entries(Neutral()), entry => string.IsNullOrWhiteSpace(entry.Value));
    }

    /// <summary>
    /// A translation is only of keys the English has, and uses the same placeholders - "{0}" left out
    /// or "{1}" made up would drop a value or crash the screen that shows it.
    /// </summary>
    [Fact]
    public void EveryTranslationFitsTheEnglish()
    {
        Dictionary<string, string> english = Entries(Neutral());
        List<string> problems = [];

        foreach (string file in Translations())
        {
            string language = Path.GetFileNameWithoutExtension(file).Split('.')[^1];
            foreach ((string key, string value) in Entries(file))
            {
                string pluralBase = PluralForm().Replace(key, string.Empty);
                string? source = english.GetValueOrDefault(key)
                    ?? english.GetValueOrDefault(pluralBase + "_Other");
                if (source is null)
                {
                    problems.Add($"{language}: {key} is not a key in the English");
                }
                else if (!Placeholders(source).SetEquals(Placeholders(value)))
                {
                    problems.Add($"{language}: {key} uses {{{string.Join("}, {", Placeholders(value))}}} but the English uses {{{string.Join("}, {", Placeholders(source))}}}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Every phrase the code counts has the English "one" and "other" forms to fall back on.</summary>
    [Fact]
    public void EveryCountedPhraseHasItsEnglishForms()
    {
        Dictionary<string, string> english = Entries(Neutral());
        IEnumerable<string> counted = Directory.EnumerateFiles(Path.Combine(Repository, "src", "ContextSwitcher.App"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => PluralUse().Matches(File.ReadAllText(path)).Select(m => m.Groups[1].Value))
            .Distinct();

        Assert.All(counted, phrase =>
        {
            Assert.True(english.ContainsKey(phrase + "_One"), $"{phrase}_One is missing");
            Assert.True(english.ContainsKey(phrase + "_Other"), $"{phrase}_Other is missing");
        });
    }

    private static string Neutral() => Path.Combine(ResourcesFolder, "Strings.resx");

    private static IEnumerable<string> Translations() =>
        Directory.EnumerateFiles(ResourcesFolder, "Strings.*.resx");

    private static Dictionary<string, string> Entries(string file) =>
        XDocument.Load(file).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? string.Empty);

    private static HashSet<int> Placeholders(string pattern) =>
        Placeholder().Matches(pattern).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();

    private static string FindRepository()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "ContextSwitcher.sln")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("_(One|Few|Many|Other)$")]
    private static partial Regex PluralForm();

    [GeneratedRegex("Plurals\\.Format\\(\"(\\w+)\"")]
    private static partial Regex PluralUse();
}
