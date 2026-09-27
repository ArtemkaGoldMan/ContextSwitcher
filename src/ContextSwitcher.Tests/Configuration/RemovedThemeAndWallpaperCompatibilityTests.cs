using ContextSwitcher.Core.Automation;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;

namespace ContextSwitcher.Tests.Configuration;

/// <summary>
/// Theme and wallpaper support was removed, but every settings.json written before that carries a
/// "theme" and a "wallpaper" block in each profile. Those files must keep loading: a file that fails
/// to parse is quarantined and replaced with a default, which would look to the user like every
/// profile had vanished. These go through the real JsonFileStore for exactly that reason.
/// </summary>
public sealed class RemovedThemeAndWallpaperCompatibilityTests
{
    private const string LegacySettings = """
        {
          "schemaVersion": 1,
          "activeContextId": "work",
          "contexts": [
            {
              "id": "work",
              "displayName": "Work",
              "launchApps": ["Calculator"],
              "theme": { "mode": "dark" },
              "wallpaper": { "path": "/Users/someone/Pictures/work.jpg", "allSpaces": true },
              "switchPolicy": { "continueOnNonCriticalFailure": true, "criticalSteps": ["SetTheme", "SetWallpaper", "LaunchApplications"] }
            },
            {
              "id": "personal",
              "displayName": "Personal",
              "theme": { "mode": "system" },
              "wallpaper": { "path": "", "allSpaces": true }
            }
          ]
        }
        """;

    [Fact]
    public async Task ASettingsFileWithThemeAndWallpaperBlocksStillLoadsWithEveryProfile()
    {
        string directory = CreateTempDirectory();
        try
        {
            ConfigPaths paths = new(directory);
            await File.WriteAllTextAsync(paths.SettingsPath, LegacySettings);

            AppConfiguration? loaded = await new JsonFileStore().ReadAsync<AppConfiguration>(paths.SettingsPath);

            Assert.NotNull(loaded);
            Assert.Equal(["work", "personal"], loaded!.Contexts.Select(c => c.Id));
            Assert.Equal(["Calculator"], loaded.Contexts[0].LaunchApps);
            Assert.Empty(Directory.EnumerateFiles(directory, "settings.json.corrupt.*"));
            Assert.True(new ConfigurationValidator().Validate(loaded).IsValid);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// criticalSteps is a free list of step names, so a profile that marked the theme or wallpaper
    /// step critical still names them. They now match nothing - which must not stop the steps that
    /// do still exist from being planned, or from staying critical.
    /// </summary>
    [Fact]
    public async Task CriticalStepsNamingRemovedStepsAreIgnoredWhileTheRestStillApply()
    {
        string directory = CreateTempDirectory();
        try
        {
            ConfigPaths paths = new(directory);
            await File.WriteAllTextAsync(paths.SettingsPath, LegacySettings);
            AppConfiguration loaded = (await new JsonFileStore().ReadAsync<AppConfiguration>(paths.SettingsPath))!;

            AutomationPlan plan = new AutomationPlanBuilder().Build(loaded.Contexts[1], loaded.Contexts[0]);

            AutomationStep launch = Assert.Single(plan.Steps, s => s.Type == AutomationStepType.LaunchApplications);
            Assert.True(launch.IsCritical);
            Assert.DoesNotContain(plan.Steps, s => s.Type.ToString() is "SetTheme" or "SetWallpaper");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The old blocks are dropped the next time the file is saved rather than carried forward -
    /// recorded here so that is a known behaviour rather than a surprise.
    /// </summary>
    [Fact]
    public async Task SavingDropsTheOldBlocks()
    {
        string directory = CreateTempDirectory();
        try
        {
            ConfigPaths paths = new(directory);
            JsonFileStore store = new();
            await File.WriteAllTextAsync(paths.SettingsPath, LegacySettings);
            AppConfiguration loaded = (await store.ReadAsync<AppConfiguration>(paths.SettingsPath))!;

            await store.WriteAsync(paths.SettingsPath, loaded);
            string rewritten = await File.ReadAllTextAsync(paths.SettingsPath);

            Assert.DoesNotContain("\"theme\"", rewritten, StringComparison.Ordinal);
            Assert.DoesNotContain("\"wallpaper\"", rewritten, StringComparison.Ordinal);
            Assert.Contains("\"work\"", rewritten, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cs-legacy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
