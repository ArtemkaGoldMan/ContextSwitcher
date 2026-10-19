using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.Startup;

/// <summary>
/// What happens before the first window: the settings and where the app stood are loaded - or made -
/// and published, and the time history is put right. In a folder of its own, with the real files.
/// </summary>
public sealed class AppStartupTests : IDisposable
{
    private static readonly AppConfiguration TwoProfiles = new()
    {
        ActiveContextId = "work",
        Contexts =
        [
            new ContextDefinition { Id = "work", DisplayName = "Work" },
            new ContextDefinition { Id = "personal", DisplayName = "Personal" }
        ]
    };

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cs-startup-{Guid.NewGuid():N}");
    private readonly ConfigPaths paths;
    private readonly TestLogger logger = new();
    private readonly JsonFileStore jsonStore;
    private readonly AppState appState = new();
    private readonly FakeClock clock = new() { UtcNow = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero) };
    private readonly InMemoryAnalyticsSessionStore sessions = new();

    public AppStartupTests()
    {
        this.paths = new ConfigPaths(this.directory);
        this.jsonStore = new JsonFileStore(FileReplacer.Once, this.logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }
    }

    /// <summary>A first start writes one Default profile, with the setup wizard still to come, and publishes it.</summary>
    [Fact]
    public async Task AFirstStartWritesADefaultAndLeavesTheWizardToCome()
    {
        StartupOutcome outcome = await this.Create().RunAsync(interactive: true);

        Assert.False(outcome.ConfigurationWasQuarantined);
        AppConfiguration? saved = await this.jsonStore.ReadAsync<AppConfiguration>(this.paths.SettingsPath);
        Assert.Equal("default", saved?.ActiveContextId);
        Assert.False(saved!.OnboardingCompleted);
        Assert.Equal("default", this.appState.Configuration.ActiveContextId);
        Assert.True(this.appState.ConfigurationValidation.IsValid);
        Assert.True(Directory.Exists(this.paths.BackupsDirectory));
    }

    /// <summary>A settings file that can't be read is moved aside, not lost, and said so; the app starts on a default.</summary>
    [Fact]
    public async Task AnUnreadableSettingsFileIsMovedAsideAndSaidSo()
    {
        Directory.CreateDirectory(this.directory);
        await File.WriteAllTextAsync(this.paths.SettingsPath, "{ this isn't json");

        StartupOutcome outcome = await this.Create().RunAsync(interactive: false);

        Assert.True(outcome.ConfigurationWasQuarantined);
        Assert.Single(Directory.GetFiles(this.directory, "settings.json.corrupt.*"));
        Assert.Contains(this.logger.Entries, entry => entry.EventId == "ConfigurationQuarantined");
        Assert.Equal("default", this.appState.Configuration.ActiveContextId);
    }

    /// <summary>Settings with problems still load - the dashboard lists them - and the log says which settings, not what was typed.</summary>
    [Fact]
    public async Task SettingsWithProblemsArePublishedWithThemAndLogged()
    {
        Directory.CreateDirectory(this.directory);
        await this.jsonStore.WriteAsync(this.paths.SettingsPath, new AppConfiguration
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Secret project", AccentColor = "not a colour" }]
        });

        StartupOutcome outcome = await this.Create().RunAsync(interactive: false);

        Assert.False(outcome.ConfigurationWasQuarantined);
        Assert.False(this.appState.ConfigurationValidation.IsValid);
        LogEntry entry = Assert.Single(this.logger.Entries, e => e.EventId == "ConfigurationInvalid");
        Assert.Equal("The settings have 1 problem(s): contexts[].accentColor (Config_AccentColorInvalid)", entry.Message);
    }

    /// <summary>Where the app stood is loaded and published: the profile that was on, and the last switch.</summary>
    [Fact]
    public async Task WhereTheAppStoodIsPublished()
    {
        await this.SeedAsync(new CurrentContextState { CurrentContextId = "personal", LastSwitchCompletedAt = this.clock.UtcNow.AddHours(-1) });

        await this.Create().RunAsync(interactive: false);

        Assert.Equal("personal", this.appState.State.CurrentContextId);
        Assert.Equal(this.clock.UtcNow.AddHours(-1), this.appState.State.LastSwitchCompletedAt);
    }

    /// <summary>The app closes the session a crash left open, then counts time for the profile that is on.</summary>
    [Fact]
    public async Task TheAppRecoversWhatACrashLeftAndStartsCountingForTheActiveProfile()
    {
        await this.SeedAsync(new CurrentContextState { CurrentContextId = "work", LastSwitchCompletedAt = this.clock.UtcNow.AddHours(-3) });
        await this.sessions.WriteActiveMarkerAsync(this.OpenSession("personal"), CancellationToken.None);

        await this.Create().RunAsync(interactive: true);

        ContextSession recovered = Assert.Single(this.sessions.Sessions);
        Assert.Equal(("personal", SessionEndReason.RecoveredAfterCrash), (recovered.ContextId, recovered.EndReason));
        Assert.Equal("work", (await this.sessions.ReadActiveMarkerAsync(CancellationToken.None))?.ContextId);
    }

    /// <summary>A command is over in a moment: it leaves the time history as it found it.</summary>
    [Fact]
    public async Task ACommandLeavesTheTimeHistoryAlone()
    {
        await this.SeedAsync(new CurrentContextState { CurrentContextId = "work", LastSwitchCompletedAt = this.clock.UtcNow.AddHours(-3) });
        await this.sessions.WriteActiveMarkerAsync(this.OpenSession("personal"), CancellationToken.None);

        await this.Create().RunAsync(interactive: false);

        Assert.Empty(this.sessions.Sessions);
        Assert.Equal("personal", (await this.sessions.ReadActiveMarkerAsync(CancellationToken.None))?.ContextId);
    }

    /// <summary>Settings with problems count no time, even for a profile that is there.</summary>
    [Fact]
    public async Task SettingsWithProblemsStartNoSession()
    {
        Directory.CreateDirectory(this.directory);
        await this.jsonStore.WriteAsync(this.paths.SettingsPath, TwoProfiles with
        {
            Contexts = [TwoProfiles.Contexts[0] with { AccentColor = "not a colour" }, TwoProfiles.Contexts[1]]
        });

        await this.Create().RunAsync(interactive: true);

        Assert.Null(await this.sessions.ReadActiveMarkerAsync(CancellationToken.None));
    }

    private async Task SeedAsync(CurrentContextState state)
    {
        Directory.CreateDirectory(this.directory);
        await this.jsonStore.WriteAsync(this.paths.SettingsPath, TwoProfiles);
        await this.jsonStore.WriteAsync(this.paths.StatePath, state);
    }

    private ContextSession OpenSession(string contextId) => new()
    {
        SessionId = Guid.NewGuid().ToString("N"),
        ContextId = contextId,
        StartedAt = this.clock.UtcNow.AddHours(-5)
    };

    private AppStartup Create() => new(
        this.appState,
        this.paths,
        this.jsonStore,
        new ConfigurationValidator(),
        new AnalyticsService(this.clock, this.sessions),
        new LocalJsonLogger(this.paths, FileReplacer.Once, this.clock),
        this.logger,
        this.clock);
}
