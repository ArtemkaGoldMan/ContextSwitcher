using System.Globalization;
using ContextSwitcher.App.Resources;

namespace ContextSwitcher.Tests.Localization;

public sealed class AppLanguageCultureTests
{
    /// <summary>Ukrainian on a Mac set to Poland: Ukrainian words, Polish formats - "3,5 год".</summary>
    [Fact]
    public void AnotherLanguageKeepsTheMacsRegion()
    {
        Assert.Equal("uk-PL", AppLanguages.CultureFor("uk", CultureInfo.GetCultureInfo("en-PL")).Name);
    }

    /// <summary>A Mac already in the app's language keeps exactly its own formats.</summary>
    [Fact]
    public void TheMacsOwnLanguageKeepsItsCulture()
    {
        CultureInfo mac = CultureInfo.GetCultureInfo("uk-UA");

        Assert.Same(mac, AppLanguages.CultureFor("uk", mac));
    }

    [Fact]
    public void WithNoRegionTheLanguageAloneIsUsed()
    {
        Assert.Equal("uk", AppLanguages.CultureFor("uk", CultureInfo.GetCultureInfo("en")).Name);
    }
}
