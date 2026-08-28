using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Tests.Files;

public sealed class JsonFileStoreTests
{
    [Fact]
    public async Task WriteAsyncCreatesAtomicJsonFileThatCanBeRead()
    {
        string tempDirectory = CreateTempDirectory();
        string settingsPath = Path.Combine(tempDirectory, "settings.json");
        JsonFileStore store = new();

        AppConfiguration configuration = new()
        {
            ActiveContextId = "personal",
            Contexts =
            [
                new ContextDefinition
                {
                    Id = "personal",
                    DisplayName = "Personal",
                    BrowserManagement = new BrowserManagementConfig
                    {
                        Mode = BrowserManagementMode.Urls,
                        Urls = ["https://youtube.com/"]
                    }
                }
            ]
        };

        try
        {
            await store.WriteAsync(settingsPath, configuration);
            AppConfiguration? roundTripped = await store.ReadAsync<AppConfiguration>(settingsPath);

            Assert.NotNull(roundTripped);
            Assert.Equal("personal", roundTripped.ActiveContextId);
            Assert.Equal(BrowserManagementMode.Urls, roundTripped.Contexts[0].BrowserManagement.Mode);
            Assert.Equal("https://youtube.com/", roundTripped.Contexts[0].BrowserManagement.Urls[0]);
            Assert.Empty(Directory.EnumerateFiles(tempDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsyncReturnsNullWhenFileDoesNotExist()
    {
        JsonFileStore store = new();

        AppConfiguration? configuration = await store.ReadAsync<AppConfiguration>(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        Assert.Null(configuration);
    }

    [Fact]
    public async Task ReadAsyncQuarantinesMalformedJson()
    {
        string tempDirectory = CreateTempDirectory();
        string settingsPath = Path.Combine(tempDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ invalid json");
        JsonFileStore store = new();

        try
        {
            AppConfiguration? configuration = await store.ReadAsync<AppConfiguration>(settingsPath);

            Assert.Null(configuration);
            Assert.False(File.Exists(settingsPath));
            Assert.Single(Directory.EnumerateFiles(tempDirectory, "settings.json.corrupt.*"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BackupAsyncCopiesExistingFileIntoBackupDirectory()
    {
        string tempDirectory = CreateTempDirectory();
        string settingsPath = Path.Combine(tempDirectory, "settings.json");
        string backupDirectory = Path.Combine(tempDirectory, "backups");
        JsonFileStore store = new();
        await store.WriteAsync(settingsPath, new AppConfiguration { ActiveContextId = "work" });

        try
        {
            await store.BackupAsync(settingsPath, backupDirectory);

            string[] backups = Directory.GetFiles(backupDirectory, "settings.*.json");
            Assert.Single(backups);

            AppConfiguration? backedUp = await store.ReadAsync<AppConfiguration>(backups[0]);
            Assert.NotNull(backedUp);
            Assert.Equal("work", backedUp.ActiveContextId);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BackupAsyncDoesNothingWhenSourceFileDoesNotExist()
    {
        string tempDirectory = CreateTempDirectory();
        string settingsPath = Path.Combine(tempDirectory, "settings.json");
        string backupDirectory = Path.Combine(tempDirectory, "backups");
        JsonFileStore store = new();

        try
        {
            await store.BackupAsync(settingsPath, backupDirectory);

            Assert.False(Directory.Exists(backupDirectory));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    /// <summary>
    /// Nothing pruned backups before, so the directory grew by one file per save, forever - five
    /// had already accumulated on a real machine from a couple of sessions of editing.
    /// </summary>
    [Fact]
    public async Task BackupAsyncKeepsOnlyTheMostRecentBackups()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cs-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        string backups = Path.Combine(directory, "backups");

        try
        {
            JsonFileStore store = new();
            await store.WriteAsync(settings, new AppConfiguration { ActiveContextId = "work" });

            for (int i = 0; i < 25; i++)
            {
                await store.BackupAsync(settings, backups);
            }

            string[] kept = Directory.GetFiles(backups, "settings.*.json");
            Assert.Equal(10, kept.Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The filename used second resolution, so two saves inside one second overwrote each other.
    /// </summary>
    [Fact]
    public async Task BackupAsyncDoesNotOverwriteWhenCalledTwiceWithinASecond()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cs-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        string backups = Path.Combine(directory, "backups");

        try
        {
            JsonFileStore store = new();
            await store.WriteAsync(settings, new AppConfiguration { ActiveContextId = "work" });

            await store.BackupAsync(settings, backups);
            await store.BackupAsync(settings, backups);

            Assert.Equal(2, Directory.GetFiles(backups, "settings.*.json").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Backups of one file must not prune another file's.</summary>
    [Fact]
    public async Task BackupAsyncPrunesEachFileIndependently()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cs-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        string state = Path.Combine(directory, "state.json");
        string backups = Path.Combine(directory, "backups");

        try
        {
            JsonFileStore store = new();
            await store.WriteAsync(settings, new AppConfiguration { ActiveContextId = "work" });
            await store.WriteAsync(state, new AppConfiguration { ActiveContextId = "personal" });

            for (int i = 0; i < 15; i++)
            {
                await store.BackupAsync(settings, backups);
            }

            await store.BackupAsync(state, backups);

            Assert.Equal(10, Directory.GetFiles(backups, "settings.*.json").Length);
            Assert.Single(Directory.GetFiles(backups, "state.*.json"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
