using ContextSwitcher.Infrastructure.Windows;

namespace ContextSwitcher.Tests.Windows;

/// <summary>The Windows version for the first line of a log, as Settings → About shows it.</summary>
public sealed class WindowsSystemVersionTests
{
    /// <summary>Windows 11 still calls itself 10.0; its build tells them apart.</summary>
    [Theory]
    [InlineData("26200", 6584, "25H2", "11 25H2 (26200.6584)")]
    [InlineData("22000", 2538, "21H2", "11 21H2 (22000.2538)")]
    [InlineData("19045", 6332, "22H2", "10 22H2 (19045.6332)")]
    [InlineData("19041", null, null, "10 (19041)")]
    public void TheVersionIsTheOneAboutShows(string build, int? revision, string? displayVersion, string expected)
    {
        Assert.Equal(expected, WindowsSystemVersion.Describe(build, revision, displayVersion));
    }

    [Fact]
    public void WithoutABuildThereIsNothingToGoOn()
    {
        Assert.Null(WindowsSystemVersion.Describe(null, 1, "25H2"));
        Assert.Null(WindowsSystemVersion.Describe("not a build", 1, "25H2"));
    }

    [Fact]
    public void ThisPcHasAVersion()
    {
        WindowsSystemVersion version = new();

        Assert.Equal("Windows", version.Name);
        Assert.Matches(@"^1[01]( \S+)? \(\d+(\.\d+)?\)$", version.Read());
    }
}
