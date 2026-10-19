using ContextSwitcher.App.Resources;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.Localization;

public sealed class AppLanguagesTests
{
    [Theory]
    [InlineData("uk", new[] { "en-US" }, "uk")]
    [InlineData("en", new[] { "uk-UA" }, "en")]
    [InlineData("UK", new[] { "en-US" }, "uk")]
    public void AChosenLanguageWins(string setting, string[] preferred, string expected) =>
        Assert.Equal(expected, AppLanguages.Resolve(setting, preferred));

    /// <summary>
    /// "Match macOS" takes the first of the Mac's languages the app has, the way macOS picks an app's
    /// language - not just the first one, which is all .NET looks at.
    /// </summary>
    [Theory]
    [InlineData(new[] { "uk-UA", "en-US" }, "uk")]
    [InlineData(new[] { "pl-PL", "uk-PL", "en-US" }, "uk")]
    [InlineData(new[] { "en-US", "pl-PL", "uk-PL" }, "en")]
    [InlineData(new[] { "pl-PL", "de-DE" }, "en")]
    [InlineData(new string[0], "en")]
    public void MatchMacOSTakesTheFirstPreferredLanguageTheAppHas(string[] preferred, string expected) =>
        Assert.Equal(expected, AppLanguages.Resolve(AppLanguages.System, preferred));

    /// <summary>A language the app no longer has - an edited settings.json, a removed translation - follows macOS instead.</summary>
    [Fact]
    public void AnUnknownLanguageFollowsMacOS() =>
        Assert.Equal("uk", AppLanguages.Resolve("tlh", ["uk-UA"]));

    [Fact]
    public void TheMacsPreferredLanguagesCanBeRead()
    {
        IReadOnlyList<string> languages = new PreferredLanguages().Get();

        Assert.NotEmpty(languages);
        Assert.All(languages, language => Assert.Matches("^[a-z]{2,3}", language));
    }
}
