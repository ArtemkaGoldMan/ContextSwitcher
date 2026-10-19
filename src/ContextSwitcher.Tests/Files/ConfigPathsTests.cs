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

    /// <summary>Windows' settings root, %APPDATA%, as the app's composition root gives it there.</summary>
    private const string AppData = @"C:\Users\someone\AppData\Roaming";

    /// <summary>The default is under ~/.config - the release's folder, or its own for a development build.</summary>
    [Fact]
    public void DefaultBaseDirectoryUsesUserConfigContextSwitcherPath()
    {
        if (Environment.GetEnvironmentVariable(ConfigPaths.DirectoryVariable) is { Length: > 0 })
        {
            return;
        }

        string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        Assert.Equal(Path.Combine(config, "ContextSwitcher"), new ConfigPaths().BaseDirectory);
        Assert.Equal(Path.Combine(config, "ContextSwitcher-dev"), new ConfigPaths(development: true).BaseDirectory);
    }

    /// <summary>Given another settings root - %APPDATA% on Windows - this build's folder is in there.</summary>
    [Fact]
    public void ASettingsRootGivenTakesThePlaceOfDotConfig()
    {
        if (Environment.GetEnvironmentVariable(ConfigPaths.DirectoryVariable) is { Length: > 0 })
        {
            return;
        }

        ConfigPaths paths = new(settingsRoot: AppData);

        Assert.Equal(Path.Combine(AppData, "ContextSwitcher"), paths.BaseDirectory);
    }

    /// <summary>What the variable can name; a folder under the home one is put together with this system's separator.</summary>
    public static TheoryData<string, string> NamedFolders => new()
    {
        { "/tmp/cs-demo", "/tmp/cs-demo" },
        { "~/cs-demo", Path.Combine(Home, "cs-demo") },
        { "  /tmp/cs-demo  ", "/tmp/cs-demo" }
    };

    [Fact]
    public void AReleaseKeepsItsFilesInTheUsualFolder()
    {
        Assert.Equal(Path.Combine(Home, ".config", "ContextSwitcher"), ConfigPaths.ResolveBaseDirectory(null, null, Home, isDevelopment: false));
    }

    [Fact]
    public void ADevelopmentBuildKeepsItsOwnFolder()
    {
        Assert.Equal(Path.Combine(Home, ".config", "ContextSwitcher-dev"), ConfigPaths.ResolveBaseDirectory(null, null, Home, isDevelopment: true));
    }

    [Theory]
    [MemberData(nameof(NamedFolders))]
    public void TheEnvironmentVariableNamesAnyFolder(string variable, string expected)
    {
        Assert.Equal(expected, ConfigPaths.ResolveBaseDirectory(null, variable, Home, isDevelopment: true));
        Assert.Equal(expected, ConfigPaths.ResolveBaseDirectory(null, variable, Home, isDevelopment: false));
    }

    [Fact]
    public void OnWindowsAReleaseKeepsItsFilesInAppData()
    {
        Assert.Equal(Path.Combine(AppData, "ContextSwitcher"), ConfigPaths.ResolveBaseDirectory(null, null, Home, isDevelopment: false, settingsRoot: AppData));
    }

    [Fact]
    public void OnWindowsADevelopmentBuildKeepsItsOwnFolderInAppData()
    {
        Assert.Equal(Path.Combine(AppData, "ContextSwitcher-dev"), ConfigPaths.ResolveBaseDirectory(null, null, Home, isDevelopment: true, settingsRoot: AppData));
    }

    /// <summary>The variable, and a "~/" in it, mean the same on Windows: only the default moves.</summary>
    [Theory]
    [MemberData(nameof(NamedFolders))]
    public void OnWindowsTheEnvironmentVariableStillNamesAnyFolder(string variable, string expected)
    {
        Assert.Equal(expected, ConfigPaths.ResolveBaseDirectory(null, variable, Home, isDevelopment: false, settingsRoot: AppData));
    }

    [Fact]
    public void AFolderGivenInCodeComesFirst()
    {
        Assert.Equal("/tmp/tests", ConfigPaths.ResolveBaseDirectory("/tmp/tests", "/tmp/cs-demo", Home, isDevelopment: true));
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
