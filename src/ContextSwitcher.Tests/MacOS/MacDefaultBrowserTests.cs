using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class MacDefaultBrowserTests
{
    [Theory]
    [InlineData("com.google.Chrome", BrowserKind.Chrome)]
    [InlineData("com.brave.Browser", BrowserKind.Brave)]
    [InlineData("com.apple.Safari", BrowserKind.Safari)]
    [InlineData("org.mozilla.firefox", BrowserKind.Default)]
    [InlineData("com.google.Chrome.canary", BrowserKind.Default)] // its own app name, which the scripts don't use
    [InlineData(null, BrowserKind.Default)]
    public void OnlyBrowsersThatCanBeScriptedAreRecognised(string? bundleIdentifier, BrowserKind expected)
    {
        Assert.Equal(expected, MacDefaultBrowser.KindOf(bundleIdentifier));
    }

    /// <summary>Every Mac has a browser for web links - asked for real, it is named.</summary>
    [MacOSFact]
    public void TheMacsDefaultBrowserIsFound()
    {
        DefaultBrowser? browser = new MacDefaultBrowser().Get();

        Assert.NotNull(browser);
        Assert.False(string.IsNullOrWhiteSpace(browser.Name));
    }
}
