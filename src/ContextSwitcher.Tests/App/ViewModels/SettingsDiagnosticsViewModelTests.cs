using ContextSwitcher.App.Services;
using ContextSwitcher.App.Startup;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

[Collection(AppHostTestCollection.Name)]
public sealed class SettingsDiagnosticsViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 13, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cs-diag-" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryJsonStore jsonStore = new();
    private readonly FakeProcessRunner processRunner = new();
    private readonly ConfigPaths paths;
    private readonly ConfigurationStore store;

    public SettingsDiagnosticsViewModelTests()
    {
        Directory.CreateDirectory(this.directory);
        this.paths = new ConfigPaths(this.directory);
        this.store = new ConfigurationStore(this.jsonStore, this.paths, new ConfigurationValidator());
        AppHost.UpdateConfiguration(
            new AppConfiguration { ActiveContextId = "work", Contexts = [new ContextDefinition { Id = "work", DisplayName = "Work" }] },
            new ConfigurationValidationResult([]));
    }

    public void Dispose() => Directory.Delete(this.directory, recursive: true);

    /// <summary>The detailed log is a week, then it is quiet again by itself - so the setting is a date, not a switch.</summary>
    [Fact]
    public async Task TurningTheDetailedLogOnSetsAWeekFromNowAndOffClearsIt()
    {
        SettingsDiagnosticsViewModel viewModel = this.Create();
        Assert.False(viewModel.DetailedLog);

        viewModel.DetailedLog = true;
        await WaitUntilAsync(() => viewModel.DetailedLog);

        Assert.Equal(Now.AddDays(7), this.jsonStore.Get<AppConfiguration>(this.paths.SettingsPath)!.DetailedLogUntil);
        Assert.True(viewModel.HasDetailedLogStatus);
        Assert.Contains("2026", viewModel.DetailedLogStatus, StringComparison.Ordinal);

        viewModel.DetailedLog = false;
        await WaitUntilAsync(() => !viewModel.DetailedLog);

        Assert.Null(this.jsonStore.Get<AppConfiguration>(this.paths.SettingsPath)!.DetailedLogUntil);
        Assert.False(viewModel.HasDetailedLogStatus);
    }

    [Fact]
    public void AnExpiredDetailedLogShowsAsOff()
    {
        AppHost.UpdateConfiguration(AppHost.Configuration with { DetailedLogUntil = Now.AddDays(-1) }, new ConfigurationValidationResult([]));

        Assert.False(this.Create().DetailedLog);
    }

    [Fact]
    public async Task CopyingPutsTheReportOnTheClipboard()
    {
        SettingsDiagnosticsViewModel viewModel = this.Create();
        string? copied = null;
        viewModel.ClipboardWriter = text =>
        {
            copied = text;
            return Task.FromResult(true);
        };

        await viewModel.CopyCommand.ExecuteAsync(null);

        Assert.StartsWith("Context Switcher diagnostics", copied, StringComparison.Ordinal);
        Assert.Equal("Copied. Paste it into your message.", viewModel.Message);
    }

    [Fact]
    public async Task ACopyThatCannotBeMadeSaysSo()
    {
        SettingsDiagnosticsViewModel viewModel = this.Create();

        await viewModel.CopyCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't copy to the clipboard.", viewModel.Message);
    }

    /// <summary>The report carries the log as it is on disk, the last lines of it.</summary>
    [Fact]
    public void TheReportHoldsTheLogFile()
    {
        File.WriteAllLines(this.paths.AppLogPath, ["{\"eventId\":\"SwitchFailed\"}", "{\"eventId\":\"StepCompleted\"}"]);

        string report = this.Create().BuildReport();

        Assert.Contains("{\"eventId\":\"SwitchFailed\"}", report, StringComparison.Ordinal);
        Assert.Contains("{\"eventId\":\"StepCompleted\"}", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowingTheLogRevealsTheFileAndOtherwiseTheFolder()
    {
        SettingsDiagnosticsViewModel viewModel = this.Create();

        viewModel.ShowLogCommand.Execute(null);
        Assert.Equal([this.paths.BaseDirectory], this.processRunner.Calls[0].Arguments);

        File.WriteAllText(this.paths.AppLogPath, "{}");
        viewModel.ShowLogCommand.Execute(null);
        Assert.Equal(["-R", this.paths.AppLogPath], this.processRunner.Calls[1].Arguments);
    }

    private SettingsDiagnosticsViewModel Create() => new(
        this.store,
        this.processRunner,
        new DiagnosticsSources(
            this.paths,
            () => new RunInfo("ContextSwitcher", "0.6.1", "26.6", "arm64", "release", 1),
            () => [],
            () => Now),
        () => true);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
