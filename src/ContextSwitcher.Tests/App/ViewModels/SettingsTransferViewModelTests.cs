using System.Text.Json;
using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Platform;
using ContextSwitcher.Core.Serialization;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class SettingsTransferViewModelTests
{
    private readonly AppState appState = new();

    private readonly InMemoryJsonStore jsonStore = new();
    private readonly ConfigPaths configPaths = new("/tmp/context-switcher-tests");
    private readonly FakeSettingsFilePicker picker = new();

    public SettingsTransferViewModelTests()
    {
        AppConfiguration current = new()
        {
            ActiveContextId = "work",
            Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }]
        };
        this.appState.UpdateConfiguration(current, new ConfigurationValidator().Validate(current));
    }

    [Fact]
    public async Task ExportWritesTheSettingsAsTheyAre()
    {
        SettingsTransferViewModel transfer = this.Create();

        await transfer.ExportCommand.ExecuteAsync(null);

        Assert.Equal("Context Switcher settings.json", this.picker.SuggestedName);
        AppConfiguration? exported = JsonSerializer.Deserialize<AppConfiguration>(this.picker.SavedText!, ContextSwitcherJson.Options);
        Assert.Equal(["work"], exported?.Contexts.Select(context => context.Id));
        Assert.Equal("Saved to “settings backup.json”.", transfer.Message);
    }

    /// <summary>
    /// An import is checked, then asked about - it replaces every profile - and only then saved, with
    /// the settings it replaced backed up. The wizard isn't brought back by where the file came from.
    /// </summary>
    [Fact]
    public async Task AnImportIsAskedAboutAndThenReplacesEverything()
    {
        this.picker.FileToOpen = Settings(""" "showDockIcon": false, "onboardingCompleted": false, "activeContextId": "deep", "contexts": [ { "id": "deep", "displayName": "Deep work" } ] """);
        SettingsTransferViewModel transfer = this.Create();
        bool imported = false;
        transfer.Imported += (_, _) => imported = true;

        await transfer.ImportCommand.ExecuteAsync(null);
        Assert.True(transfer.IsConfirmingImport);
        Assert.Null(this.jsonStore.Get<AppConfiguration>(this.configPaths.SettingsPath));
        Assert.Equal(
            "Replace all your profiles and settings with the ones in “settings backup.json”? Your current ones are kept as a backup.",
            transfer.ConfirmText);

        await transfer.ConfirmImportCommand.ExecuteAsync(null);

        AppConfiguration saved = this.jsonStore.Get<AppConfiguration>(this.configPaths.SettingsPath)!;
        Assert.Equal(["deep"], saved.Contexts.Select(context => context.Id));
        Assert.False(saved.ShowDockIcon);
        Assert.True(saved.OnboardingCompleted);
        Assert.Same(saved, this.appState.Configuration);
        Assert.True(imported);
        Assert.False(transfer.IsConfirmingImport);
    }

    [Fact]
    public async Task CancellingAnImportChangesNothing()
    {
        this.picker.FileToOpen = Settings(""" "activeContextId": "deep", "contexts": [ { "id": "deep", "displayName": "Deep work" } ] """);
        SettingsTransferViewModel transfer = this.Create();

        await transfer.ImportCommand.ExecuteAsync(null);
        transfer.CancelImportCommand.Execute(null);

        Assert.False(transfer.IsConfirmingImport);
        Assert.Null(this.jsonStore.Get<AppConfiguration>(this.configPaths.SettingsPath));
        Assert.Equal(["work"], this.appState.Configuration.Contexts.Select(context => context.Id));
    }

    /// <summary>A file that isn't ours, or that wouldn't pass the check, is turned down with the reason.</summary>
    [Theory]
    [InlineData("not json at all", "“settings backup.json” isn't a Context Switcher settings file.")]
    [InlineData("""{ "name": "something else" }""", "“settings backup.json” isn't a Context Switcher settings file.")]
    [InlineData("""{ "activeContextId": "missing", "contexts": [ { "id": "work", "displayName": "Work" } ] }""", "“settings backup.json” can't be used: activeContextId: The active profile 'missing' doesn't exist.")]
    public async Task AFileThatCantBeUsedIsTurnedDown(string content, string problem)
    {
        this.picker.FileToOpen = content;
        SettingsTransferViewModel transfer = this.Create();

        await transfer.ImportCommand.ExecuteAsync(null);

        Assert.False(transfer.IsConfirmingImport);
        Assert.Equal(problem, transfer.Problem);
    }

    [Fact]
    public async Task CancellingThePanelsDoesNothing()
    {
        this.picker.CancelSave = true;
        SettingsTransferViewModel transfer = this.Create();

        await transfer.ExportCommand.ExecuteAsync(null);
        await transfer.ImportCommand.ExecuteAsync(null);

        Assert.False(transfer.HasMessage);
        Assert.False(transfer.HasProblem);
        Assert.False(transfer.IsConfirmingImport);
    }

    /// <summary>
    /// Settings from a computer that can do more are imported whole, and what this one can't use is
    /// said - not dropped, so a computer that can still has it.
    /// </summary>
    [Fact]
    public async Task WhatThisComputerCantUseIsSaidAndKept()
    {
        this.picker.FileToOpen = Settings(""" "activeContextId": "deep", "contexts": [ { "id": "deep", "displayName": "Deep work", "focus": { "enabled": true, "modeName": "Work" } } ] """);
        SettingsTransferViewModel transfer = this.Create(Platform.PlatformCapabilitiesTests.Lesser);

        await transfer.ImportCommand.ExecuteAsync(null);
        await transfer.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal(
            "Imported: your profiles and settings now come from “settings backup.json”. Some of them this computer can't use, and they stay in the settings as they are: Deep work turns on a Focus, which this computer doesn't have.",
            transfer.Message);
        Assert.Equal("Work", this.jsonStore.Get<AppConfiguration>(this.configPaths.SettingsPath)!.Contexts[0].Focus.ModeName);
    }

    private static string Settings(string body) => "{ \"schemaVersion\": 1, " + body + " }";

    private SettingsTransferViewModel Create(PlatformCapabilities? capabilities = null) =>
        new(this.appState, new ConfigurationStore(this.jsonStore, this.configPaths, new ConfigurationValidator(), this.appState, new TestLogger()), capabilities ?? MacPlatform.Capabilities)
        {
            FilePicker = this.picker
        };
}
