using ContextSwitcher.App.Services;

namespace ContextSwitcher.Tests.App.Services;

public sealed class AppInfoTests
{
    /// <summary>The version is the project's own - the number the bundle and the release carry.</summary>
    [Fact]
    public void TheVersionIsTheAppsOwn()
    {
        Assert.Equal(typeof(AppInfo).Assembly.GetName().Version!.ToString(3), new AppInfo().Version.ToString(3));
    }

    [Fact]
    public void ADevelopmentBuildSaysSoInItsName()
    {
        AppInfo app = new();

        Assert.Equal(app.IsDevelopment ? "Context Switcher DEV" : "Context Switcher", app.DisplayName);
    }
}
