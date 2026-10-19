using ContextSwitcher.Infrastructure.MacOS;

namespace ContextSwitcher.Tests.MacOS;

public sealed class MacApplicationHiderTests
{
    /// <summary>
    /// A profile names an app as its bundle is named; some show another name in the menu bar - Visual
    /// Studio Code calls itself "Code". Either finds it.
    /// </summary>
    [Theory]
    [InlineData("Visual Studio Code", "Visual Studio Code", "Code", true)]
    [InlineData("Code", "Visual Studio Code", "Code", true)]
    [InlineData("slack", "Slack", "Slack", true)]
    [InlineData("Slack", "Discord", "Discord", false)]
    public void AnAppIsFoundByItsBundleNameOrItsOwn(string name, string bundleName, string localizedName, bool expected)
    {
        Assert.Equal(expected, MacApplicationHider.Matches(name, bundleName, localizedName));
    }

    /// <summary>Asked about apps that aren't running, it looks at the real running apps and hides nothing.</summary>
    [MacOSFact]
    public void AnAppThatIsntRunningIsLeftAlone()
    {
        Assert.Equal(0, new MacApplicationHider().Hide(["ContextSwitcher Test App That Does Not Exist"]));
    }
}
