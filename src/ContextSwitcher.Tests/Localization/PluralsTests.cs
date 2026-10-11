using System.Globalization;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.Tests.Localization;

/// <summary>The CLDR plural categories for whole numbers, which a translation's _One/_Few/_Many keys follow.</summary>
public sealed class PluralsTests
{
    [Theory]
    [InlineData(0, "Other")]
    [InlineData(1, "One")]
    [InlineData(2, "Other")]
    [InlineData(21, "Other")]
    public void EnglishHasOneAndOther(int count, string category) =>
        Assert.Equal(category, Plurals.Category(count, CultureInfo.GetCultureInfo("en")));

    [Theory]
    [InlineData(1, "One")]    // 1 день
    [InlineData(21, "One")]   // 21 день
    [InlineData(2, "Few")]    // 2 дні
    [InlineData(24, "Few")]   // 24 дні
    [InlineData(5, "Many")]   // 5 днів
    [InlineData(11, "Many")]  // 11 днів
    [InlineData(12, "Many")]  // 12 днів
    [InlineData(14, "Many")]  // 14 днів
    [InlineData(0, "Many")]   // 0 днів
    [InlineData(111, "Many")] // 111 днів
    public void UkrainianHasOneFewAndMany(int count, string category) =>
        Assert.Equal(category, Plurals.Category(count, CultureInfo.GetCultureInfo("uk")));

    [Theory]
    [InlineData(1, "One")]    // 1 dzień
    [InlineData(21, "Many")]  // 21 dni - unlike Ukrainian, only 1 itself is "one"
    [InlineData(22, "Few")]   // 22 dni
    [InlineData(5, "Many")]   // 5 dni
    [InlineData(12, "Many")]  // 12 dni
    public void PolishHasOneFewAndMany(int count, string category) =>
        Assert.Equal(category, Plurals.Category(count, CultureInfo.GetCultureInfo("pl")));

    [Theory]
    [InlineData(1, "1 day")]
    [InlineData(30, "30 days")]
    public void FormatsWithTheLanguagesForm(int count, string expected) =>
        Assert.Equal(expected, Plurals.Format("Duration_Days", count));
}
