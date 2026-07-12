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

    private static string CreateTempDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ContextSwitcherTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}
