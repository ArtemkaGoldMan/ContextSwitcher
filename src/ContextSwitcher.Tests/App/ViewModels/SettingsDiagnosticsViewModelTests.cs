using ContextSwitcher.App.Services;
using ContextSwitcher.App.ViewModels;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Infrastructure.Files;
using ContextSwitcher.Infrastructure.Logging;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.App.ViewModels;

public sealed class SettingsDiagnosticsViewModelTests : IDisposable
{
    private readonly AppState appState = new();

    private static readonly DateTimeOffset Now = new(2026, 10, 14, 13, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cs-diag-" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryJsonStore jsonStore = new();
    private readonly FakeShell shell = new();
    private readonly ConfigPaths paths;
    private readonly ConfigurationStore store;

    public SettingsDiagnosticsViewModelTests()
    {
        Directory.CreateDirectory(this.directory);
        this.paths = new ConfigPaths(this.directory);
        this.store = new ConfigurationStore(this.jsonStore, this.paths, new ConfigurationValidator(), this.appState, new TestLogger());
        this.appState.UpdateConfiguration(
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
        this.appState.UpdateConfiguration(this.appState.Configuration with { DetailedLogUntil = Now.AddDays(-1) }, new ConfigurationValidationResult([]));

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

    /// <summary>The log is shown in the file manager - or, in a quiet week with none, its folder (MacShellTests).</summary>
    [Fact]
    public void ShowingTheLogShowsTheLogFile()
    {
        this.Create().ShowLogCommand.Execute(null);

        Assert.Equal([this.paths.AppLogPath], this.shell.ShownFiles);
    }

    /// <summary>
    /// "Write an email" opens a new message to the developer in the Mac's own mail app - the questions
    /// worth answering in it, the version in the subject - and copies the diagnostics to paste in.
    /// </summary>
    [Fact]
    public async Task WritingAnEmailOpensAMessageAndCopiesTheDiagnostics()
    {
        SettingsDiagnosticsViewModel viewModel = this.Create();
        string? copied = null;
        viewModel.ClipboardWriter = text =>
        {
            copied = text;
            return Task.FromResult(true);
        };

        await viewModel.WriteEmailCommand.ExecuteAsync(null);

        StartsWith(copied, "Context Switcher diagnostics");
        string link = Assert.Single(this.shell.Links);
        Assert.StartsWith("mailto:contextswitcher.app@gmail.com?subject=ContextSwitcher%200.6.1%3A%20feedback&body=", link, StringComparison.Ordinal);
        string body = Uri.UnescapeDataString(link[(link.IndexOf("&body=", StringComparison.Ordinal) + 6)..]);
        Assert.StartsWith("What happened:\r\n", body, StringComparison.Ordinal);
        Assert.EndsWith("ContextSwitcher 0.6.1, macOS 26.6", body, StringComparison.Ordinal);
        Assert.Equal("A new message is open in your mail app, and the diagnostics are copied: paste them into it with ⌘V.", viewModel.ContactMessage);
    }

    /// <summary>With no mail app to open the message, the address is given to write to from anywhere.</summary>
    [Fact]
    public async Task WithNoMailAppTheAddressIsGiven()
    {
        this.shell.LinksOpen = false;
        SettingsDiagnosticsViewModel viewModel = this.Create();

        await viewModel.WriteEmailCommand.ExecuteAsync(null);

        Assert.Equal("No mail app opened. Write to contextswitcher.app@gmail.com from any mail you use.", viewModel.ContactMessage);
        Assert.Equal("contextswitcher.app@gmail.com", viewModel.ContactEmail);
    }

    [Fact]
    public async Task AnIssueOpensGitHubsFormWithTheDiagnosticsCopied()
    {
        SettingsDiagnosticsViewModel viewModel = this.Create();
        bool copied = false;
        viewModel.ClipboardWriter = _ =>
        {
            copied = true;
            return Task.FromResult(true);
        };

        await viewModel.OpenIssueCommand.ExecuteAsync(null);

        Assert.True(copied);
        Assert.Equal(["https://github.com/ArtemkaGoldMan/ContextSwitcher/issues/new?template=bug_report.yml"], this.shell.Links);
        Assert.Equal("The form is open on GitHub, and the diagnostics are copied: paste them into its Diagnostics field.", viewModel.ContactMessage);
    }

    [Fact]
    public void WhatToWriteOpensTheReadmesContactSection()
    {
        this.Create().WhatToWriteCommand.Execute(null);

        Assert.Equal(["https://github.com/ArtemkaGoldMan/ContextSwitcher#contact"], this.shell.Links);
    }

    /// <summary>A mail link carries its text escaped, with the line breaks mail links expect.</summary>
    [Fact]
    public void AMailLinkEscapesItsText()
    {
        string link = SettingsDiagnosticsViewModel.MailTo("a@b.c", "Hi & bye?", "one\ntwo #3");

        Assert.Equal("mailto:a@b.c?subject=Hi%20%26%20bye%3F&body=one%0D%0Atwo%20%233", link);
    }

    private static void StartsWith(string? text, string start) =>
        Assert.True(text?.StartsWith(start, StringComparison.Ordinal) == true, $"expected it to start with \"{start}\": {text}");

    private SettingsDiagnosticsViewModel Create() => new(
        this.appState,
        this.store,
        this.shell,
        new DiagnosticsSources(
            this.paths,
            () => new RunInfo("ContextSwitcher", "0.6.1", "26.6", "arm64", "release", 1),
            () => DiagnosticsReport.ReadLog(this.paths.AppLogPath),
            () => [],
            new FakeClock { UtcNow = Now }),
        () => true);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
