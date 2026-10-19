using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Infrastructure.Browser;
using ContextSwitcher.Infrastructure.MacOS.Browser;

namespace ContextSwitcher.Tests.Browser;

/// <summary>Against the real process list: it reads, and never starts or touches, a browser.</summary>
public sealed class ProcessBrowserRunsTests
{
    [Fact]
    public void OnlyChromeAndBraveHaveRunsToTellApart()
    {
        ProcessBrowserRuns runs = new();

        Assert.Null(runs.Current(BrowserKind.Safari));
        Assert.Null(runs.Current(BrowserKind.Default));
    }

    /// <summary>
    /// While Chrome runs it is the same run from one moment to the next - and the run names its
    /// main process and when it started, so a restart is a different run.
    /// </summary>
    [Fact]
    public void ARunningChromeIsTheSameRunEachTimeItIsAsked()
    {
        ProcessBrowserRuns runs = new();
        string? first = runs.Current(BrowserKind.Chrome);
        if (first is null)
        {
            return;
        }

        Assert.Matches(@"^\d+@\d{4}-\d\d-\d\dT", first);
        Assert.Equal(first, runs.Current(BrowserKind.Chrome));
    }
}
