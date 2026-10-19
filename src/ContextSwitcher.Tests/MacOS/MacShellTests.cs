using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.MacOS;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.MacOS;

/// <summary>What the Mac is asked to open, through <c>open</c>.</summary>
public sealed class MacShellTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cs-shell-{Guid.NewGuid():N}");
    private readonly FakeProcessRunner processRunner = new();

    public MacShellTests() => Directory.CreateDirectory(this.directory);

    public void Dispose() => Directory.Delete(this.directory, recursive: true);

    [Fact]
    public async Task ALinkIsHandedToOpen()
    {
        bool opened = await new MacShell(this.processRunner).OpenLinkAsync("https://github.com/ArtemkaGoldMan/ContextSwitcher", CancellationToken.None);

        Assert.True(opened);
        ProcessStartOptions call = Assert.Single(this.processRunner.Calls);
        Assert.Equal(("open", "https://github.com/ArtemkaGoldMan/ContextSwitcher"), (call.FileName, Assert.Single(call.Arguments)));
    }

    /// <summary>With no mail app, <c>open</c> fails on a mail link, and the app says so.</summary>
    [Fact]
    public async Task ALinkNothingOpensSaysSo()
    {
        this.processRunner.DefaultResult = new ProcessResult(1, string.Empty, "No application knows how to open URL", false);

        Assert.False(await new MacShell(this.processRunner).OpenLinkAsync("mailto:someone@example.com", CancellationToken.None));
    }

    [Fact]
    public void AFileIsShownInFinderAndOneThatIsntThereIsItsFolder()
    {
        string log = Path.Combine(this.directory, "app.log.jsonl");
        MacShell shell = new(this.processRunner);

        shell.ShowFile(log);
        File.WriteAllText(log, "{}");
        shell.ShowFile(log);

        Assert.Equal([this.directory], this.processRunner.Calls[0].Arguments);
        Assert.Equal(["-R", log], this.processRunner.Calls[1].Arguments);
    }

    [Theory]
    [InlineData(SystemPage.AutomationPermission, "x-apple.systempreferences:com.apple.preference.security?Privacy_Automation")]
    [InlineData(SystemPage.FocusSettings, "x-apple.systempreferences:com.apple.Focus-Settings.extension")]
    [InlineData(SystemPage.Shortcuts, "-a Shortcuts")]
    public void EachSystemPageIsWhereSystemSettingsKeepsIt(SystemPage page, string arguments)
    {
        new MacShell(this.processRunner).OpenSystemPage(page);

        ProcessStartOptions call = Assert.Single(this.processRunner.Calls);
        Assert.Equal(("open", arguments), (call.FileName, string.Join(' ', call.Arguments)));
    }
}
