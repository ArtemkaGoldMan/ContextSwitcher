using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.Windows;
using ContextSwitcher.Tests.TestDoubles;

namespace ContextSwitcher.Tests.Windows;

/// <summary>Links and files through File Explorer, the way Windows opens them.</summary>
public sealed class WindowsShellTests
{
    /// <summary>File Explorer ends with 1 whether or not the link opened, so that is no failure.</summary>
    [Fact]
    public async Task ALinkOpensThroughFileExplorer()
    {
        FakeProcessRunner runner = new() { DefaultResult = new ProcessResult(1, string.Empty, string.Empty, TimedOut: false) };

        bool opened = await new WindowsShell(runner).OpenLinkAsync("https://example.com/a?b=1&c=2", CancellationToken.None);

        Assert.True(opened);
        ProcessStartOptions call = Assert.Single(runner.Calls);
        Assert.Equal("explorer", call.FileName);
        Assert.Equal(["https://example.com/a?b=1&c=2"], call.Arguments);
    }

    [Fact]
    public async Task ALinkThatHangsDidNotOpen()
    {
        FakeProcessRunner runner = new() { DefaultResult = new ProcessResult(-1, string.Empty, string.Empty, TimedOut: true) };

        Assert.False(await new WindowsShell(runner).OpenLinkAsync("https://example.com/", CancellationToken.None));
    }

    /// <summary>A file is shown selected in its folder; one not there yet, by its folder.</summary>
    [Fact]
    public void AFileIsShownInItsFolder()
    {
        string file = Path.GetTempFileName();
        try
        {
            FakeProcessRunner runner = new();
            WindowsShell shell = new(runner);

            shell.ShowFile(file);
            shell.ShowFile(Path.Combine(Path.GetTempPath(), "not-there.log"));

            Assert.Equal([$"/select,{file}"], runner.Calls[0].Arguments);
            Assert.Equal([Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)], runner.Calls[1].Arguments);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Windows has none of the Mac's system pages, so nothing is run for one.</summary>
    [Fact]
    public void NoSystemPageIsOpened()
    {
        FakeProcessRunner runner = new();

        new WindowsShell(runner).OpenSystemPage(SystemPage.AutomationPermission);

        Assert.Empty(runner.Calls);
    }
}
