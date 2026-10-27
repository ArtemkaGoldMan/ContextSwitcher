using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Startup;

/// <summary>How a system finds each app the profiles name is written into the settings, and only where it is missing.</summary>
public sealed class AppsMapRecorderTests
{
    private static readonly AppIdentity Slack = new() { WindowsAppId = "com.squirrel.slack.slack", WindowsExe = @"C:\Slack\slack.exe" };
    private static readonly AppIdentity Code = new() { WindowsAppId = "Microsoft.VisualStudioCode" };

    private readonly ConfigPaths paths = new("/tmp/context-switcher-recorder-tests");
    private readonly InMemoryJsonStore json = new();
    private readonly TestLogger logger = new();
    private readonly AppState appState = new();
    private readonly FakeCatalog catalog = new();

    private AppsMapRecorder Recorder(AppConfiguration configuration)
    {
        ConfigurationValidator validator = new();
        this.appState.UpdateConfiguration(configuration, validator.Validate(configuration));
        this.json.Seed(this.paths.SettingsPath, configuration);
        ConfigurationStore store = new(this.json, this.paths, validator, this.appState, this.logger);
        return new AppsMapRecorder(this.appState, store, this.catalog, this.logger);
    }

    private static AppConfiguration Profiles(params string[] apps) => new()
    {
        Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work", LaunchApps = apps }]
    };

    [Fact]
    public async Task WhatTheSystemKnowsOfTheAppsIsSavedIntoTheMap()
    {
        this.catalog.Known["Slack"] = Slack;
        this.catalog.Known["Visual Studio Code"] = Code;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack", "Visual Studio Code"));

        await recorder.RecordAsync();

        Assert.Equal(Slack, this.appState.Configuration.Apps!["Slack"]);
        Assert.Equal(Code, this.appState.Configuration.Apps["Visual Studio Code"]);
        Assert.Equal(2, this.json.Get<AppConfiguration>(this.paths.SettingsPath)!.Apps!.Count);
    }

    [Fact]
    public async Task AnEntryThatIsThereIsNotAskedForNorReplaced()
    {
        AppIdentity edited = new() { WindowsExe = "my-slack.exe" };
        this.catalog.Known["Slack"] = Slack;
        this.catalog.Known["Visual Studio Code"] = Code;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack", "Visual Studio Code") with { Apps = new Dictionary<string, AppIdentity> { ["Slack"] = edited } });

        await recorder.RecordAsync();

        Assert.Equal(edited, this.appState.Configuration.Apps!["Slack"]);
        Assert.Equal(Code, this.appState.Configuration.Apps["Visual Studio Code"]);
        Assert.Equal(["Visual Studio Code"], this.catalog.Asked);
    }

    /// <summary>The Mac, and an app the system doesn't know: nothing to write, so nothing is saved.</summary>
    [Fact]
    public async Task WhenThereIsNothingToRecordNothingIsSaved()
    {
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack"));

        await recorder.RecordAsync();

        Assert.Null(this.appState.Configuration.Apps);
        Assert.Empty(this.json.BackedUpPaths);
        Assert.Equal(["Slack"], this.catalog.Asked);
    }

    [Fact]
    public async Task ASystemThatGoesByTheNameAloneWritesNothing()
    {
        ConfigurationValidator validator = new();
        AppConfiguration configuration = Profiles("Safari", "Slack");
        this.appState.UpdateConfiguration(configuration, validator.Validate(configuration));
        this.json.Seed(this.paths.SettingsPath, configuration);
        AppsMapRecorder recorder = new(this.appState, new ConfigurationStore(this.json, this.paths, validator, this.appState, this.logger), new NoAppIdentities(), this.logger);

        await recorder.RecordAsync();

        Assert.Same(configuration, this.appState.Configuration);
        Assert.Empty(this.json.BackedUpPaths);
    }

    [Fact]
    public async Task SettingsThatAreNotValidAreLeftAlone()
    {
        this.catalog.Known["Slack"] = Slack;
        AppConfiguration invalid = Profiles("Slack") with { SchemaVersion = 999 };
        AppsMapRecorder recorder = this.Recorder(invalid);

        await recorder.RecordAsync();

        Assert.Empty(this.catalog.Asked);
        Assert.Null(this.appState.Configuration.Apps);
    }

    [Fact]
    public async Task AnAppAddedLaterIsRecordedWhenTheSettingsChange()
    {
        this.catalog.Known["Slack"] = Slack;
        this.catalog.Known["Visual Studio Code"] = Code;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack"));
        recorder.Start();
        await WaitForAsync(() => this.appState.Configuration.Apps?.ContainsKey("Slack") == true);

        AppConfiguration next = this.appState.Configuration with
        {
            Contexts = [this.appState.Configuration.Contexts[0] with { LaunchApps = ["Slack", "Visual Studio Code"] }]
        };
        this.appState.UpdateConfiguration(next, new ConfigurationValidator().Validate(next));
        await WaitForAsync(() => this.appState.Configuration.Apps?.ContainsKey("Visual Studio Code") == true);

        Assert.Equal(Code, this.appState.Configuration.Apps!["Visual Studio Code"]);
    }

    /// <summary>Saving raises the change the recorder listens to; it must not go round and round.</summary>
    [Fact]
    public async Task ItsOwnSaveDoesNotStartItAgainWithoutEnd()
    {
        this.catalog.Known["Slack"] = Slack;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack", "Unknown App"));
        recorder.Start();
        await WaitForAsync(() => this.appState.Configuration.Apps?.ContainsKey("Slack") == true);
        await Task.Delay(200);

        Assert.True(this.catalog.Asked.Count <= 5, $"asked {this.catalog.Asked.Count} times");
        Assert.Single(this.json.BackedUpPaths);
    }

    /// <summary>The settings may be saved - by the person - while the catalog is being asked: both are kept.</summary>
    [Fact]
    public async Task SettingsSavedWhileTheCatalogWasBeingAskedAreNotLost()
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        this.catalog.Known["Slack"] = Slack;
        this.catalog.Gate = gate.Task;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack"));

        Task recording = recorder.RecordAsync();
        AppConfiguration edited = this.appState.Configuration with { OnboardingCompleted = false };
        this.appState.UpdateConfiguration(edited, new ConfigurationValidator().Validate(edited));
        gate.SetResult();
        await recording;

        Assert.False(this.appState.Configuration.OnboardingCompleted);
        Assert.Equal(Slack, this.appState.Configuration.Apps!["Slack"]);
    }

    [Fact]
    public async Task ASaveThatFailsIsLoggedAndThatIsAll()
    {
        this.catalog.Known["Slack"] = Slack;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack"));
        this.json.WriteFailurePaths.Add(this.paths.SettingsPath);

        await recorder.RecordAsync();

        Assert.Null(this.appState.Configuration.Apps);
        Assert.Contains(this.logger.Entries, entry => entry.EventId == "AppsMapNotSaved");
    }

    /// <summary>It runs on its own, so one failed save must not stop the next from being tried.</summary>
    [Fact]
    public async Task AfterASaveThatFailedTheNextChangeTriesAgain()
    {
        this.catalog.Known["Slack"] = Slack;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack"));
        this.json.WriteFailurePaths.Add(this.paths.SettingsPath);
        await recorder.RecordAsync();

        this.json.WriteFailurePaths.Clear();
        await recorder.RecordAsync();

        Assert.Equal(Slack, this.appState.Configuration.Apps!["Slack"]);
    }

    [Fact]
    public async Task TheMapIsReadFromTheSettingsAsTheyAreNow()
    {
        this.catalog.Known["Slack"] = Slack;
        AppsMapRecorder recorder = this.Recorder(Profiles("Slack"));
        ConfigurationAppsMap map = new(this.appState);

        Assert.Null(map.Of("Slack"));

        await recorder.RecordAsync();

        Assert.Equal(Slack, map.Of("slack"));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition(), "never happened");
    }

    private sealed class FakeCatalog : IAppIdentityCatalog
    {
        public Dictionary<string, AppIdentity> Known { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Asked { get; } = [];

        public Task? Gate { get; set; }

        public async Task<AppIdentity?> IdentifyAsync(string appName, CancellationToken cancellationToken)
        {
            this.Asked.Add(appName);
            if (this.Gate is { } gate)
            {
                await gate.ConfigureAwait(true);
            }

            return this.Known.GetValueOrDefault(appName);
        }
    }
}
