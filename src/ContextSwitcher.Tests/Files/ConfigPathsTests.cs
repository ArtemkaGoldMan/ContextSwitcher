using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Tests.Files;

/// <summary>
/// Where everything is kept. The app run from the code and the installed app used to share one
/// folder, so installing it showed the profiles being worked on, and both changed the same time
/// records.
/// </summary>
public sealed class ConfigPathsTests
{
    private const string Home = "/Users/someone";

    /// <summary>The default is under ~/.config - this build's folder: its own for a development build.</summary>
    [Fact]
    public void DefaultBaseDirectoryUsesUserConfigContextSwitcherPath()
    {
        if (Environment.GetEnvironmentVariable(ConfigPaths.DirectoryVariable) is { Length: > 0 })
        {
            return;
        }

        ConfigPaths paths = new();

        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            AppBuild.IsDevelopment ? "ContextSwitcher-dev" : "ContextSwitcher");

        Assert.Equal(expected, paths.BaseDirectory);
    }

    [Fact]
    public void AReleaseKeepsItsFilesInTheUsualFolder()
    {
        Assert.Equal("/Users/someone/.config/ContextSwitcher", ConfigPaths.ResolveBaseDirectory(null, null, Home, isDevelopment: false));
    }

    [Fact]
    public void ADevelopmentBuildKeepsItsOwnFolder()
    {
        Assert.Equal("/Users/someone/.config/ContextSwitcher-dev", ConfigPaths.ResolveBaseDirectory(null, null, Home, isDevelopment: true));
    }

    [Theory]
    [InlineData("/tmp/cs-demo", "/tmp/cs-demo")]
    [InlineData("~/cs-demo", "/Users/someone/cs-demo")]
    [InlineData("  /tmp/cs-demo  ", "/tmp/cs-demo")]
    public void TheEnvironmentVariableNamesAnyFolder(string variable, string expected)
    {
        Assert.Equal(expected, ConfigPaths.ResolveBaseDirectory(null, variable, Home, isDevelopment: true));
        Assert.Equal(expected, ConfigPaths.ResolveBaseDirectory(null, variable, Home, isDevelopment: false));
    }

    [Fact]
    public void AFolderGivenInCodeComesFirst()
    {
        Assert.Equal("/tmp/tests", ConfigPaths.ResolveBaseDirectory("/tmp/tests", "/tmp/cs-demo", Home, isDevelopment: true));
    }

    [Fact]
    public void ADevelopmentBuildSaysSoInItsName()
    {
        Assert.Equal(AppBuild.IsDevelopment ? "Context Switcher DEV" : "Context Switcher", AppBuild.DisplayName);
    }

    [Fact]
    public void EnsureCreatedCreatesBaseAndBackupDirectories()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
        ConfigPaths paths = new(tempDirectory);

        try
        {
            paths.EnsureCreated();

            Assert.True(Directory.Exists(paths.BaseDirectory));
            Assert.True(Directory.Exists(paths.BackupsDirectory));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
