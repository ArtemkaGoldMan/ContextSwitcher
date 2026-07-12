using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Tests.Files;

public sealed class ConfigPathsTests
{
    [Fact]
    public void DefaultBaseDirectoryUsesUserConfigContextSwitcherPath()
    {
        ConfigPaths paths = new();

        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "ContextSwitcher");

        Assert.Equal(expected, paths.BaseDirectory);
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
